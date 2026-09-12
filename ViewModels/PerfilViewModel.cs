using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Cajolote.Data;
using Cajolote.Models;
using Cajolote.Messages;
using System.Linq;

using System.IO;
using System.Windows;
using Cajolote.Services;

namespace Cajolote.ViewModels;

public partial class PerfilViewModel : ObservableObject
{
    private readonly FirebaseAuthService _authService;
    private readonly SyncService _syncService;

    public PerfilViewModel(FirebaseAuthService authService, SyncService syncService)
    {
        _authService = authService;
        _syncService = syncService;
        LoadProfile();

        // Registrarse al mensaje de sincronización para actualizar la UI sin capturas de cierre
        WeakReferenceMessenger.Default.Register<PerfilViewModel, ProfileSyncedMessage>(this, (r, m) =>
        {
            Application.Current.Dispatcher.Invoke(() => r.LoadProfile());
        });
    }

    [ObservableProperty]
    private StoreProfile _profile = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string SyncStatusText => Profile?.IsDirty == true ? "Cambios locales en espera" : "Sincronizado con la nube";
    public string SyncStatusColor => Profile?.IsDirty == true ? "#E67E22" : "#76C893";
    public string LastUpdatedText => (Profile == null || Profile.LastUpdated == System.DateTime.MinValue)
        ? "Nunca"
        : Profile.LastUpdated.ToLocalTime().ToString("dd/MM/yyyy hh:mm:ss tt");

    private string _originalStoreName = string.Empty;
    private string _originalOwnerName = string.Empty;
    private string _originalPhone = string.Empty;
    private string _originalAddress = string.Empty;
    private string _originalRfc = string.Empty;

    [ObservableProperty]
    private string _storeName = string.Empty;

    [ObservableProperty]
    private string _ownerName = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _rfc = string.Empty;

    partial void OnStoreNameChanged(string value) => NotifyImageProperties();
    partial void OnOwnerNameChanged(string value) => NotifyImageProperties();
    partial void OnPhoneChanged(string value) => NotifyImageProperties();
    partial void OnAddressChanged(string value) => NotifyImageProperties();
    partial void OnRfcChanged(string value) => NotifyImageProperties();

    public bool IsSavePending => 
        !string.IsNullOrEmpty(TempImagePath) || 
        _isImageRemoved || 
        (StoreName ?? string.Empty) != _originalStoreName ||
        (OwnerName ?? string.Empty) != _originalOwnerName ||
        (Phone ?? string.Empty) != _originalPhone ||
        (Address ?? string.Empty) != _originalAddress ||
        (Rfc ?? string.Empty) != _originalRfc;

    private bool _isImageRemoved;

    [ObservableProperty]
    private string? _tempImagePath;

    partial void OnTempImagePathChanged(string? value)
    {
        _isImageRemoved = false;
        NotifyImageProperties();
    }

    public string? DisplayImagePath
    {
        get
        {
            if (_isImageRemoved) return null;
            return !string.IsNullOrEmpty(TempImagePath) ? TempImagePath : Profile?.ImagePath;
        }
    }

    public bool HasProfileImage => !string.IsNullOrEmpty(DisplayImagePath) && System.IO.File.Exists(DisplayImagePath);
    public bool ShowPlaceholder => !HasProfileImage;

    public System.Windows.Media.ImageSource? ProfileImageSource
    {
        get
        {
            var path = DisplayImagePath;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return null;
            }

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar bitmap de perfil sin bloquearlo: {ex.Message}");
                return null;
            }
        }
    }

    private void NotifyImageProperties()
    {
        OnPropertyChanged(nameof(DisplayImagePath));
        OnPropertyChanged(nameof(HasProfileImage));
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(ProfileImageSource));
        OnPropertyChanged(nameof(IsSavePending));
    }

    partial void OnProfileChanged(StoreProfile value)
    {
        OnPropertyChanged(nameof(SyncStatusText));
        OnPropertyChanged(nameof(SyncStatusColor));
        OnPropertyChanged(nameof(LastUpdatedText));
        NotifyImageProperties();
    }

    private void LoadProfile()
    {
        using (var context = new CajoloteDbContext())
        {
            var existing = context.StoreProfiles.FirstOrDefault();
            if (existing != null)
            {
                Profile = existing;
            }
            else
            {
                Profile = new StoreProfile();
            }
        }

        // Load properties to ViewModel
        StoreName = Profile.StoreName ?? string.Empty;
        OwnerName = Profile.OwnerName ?? string.Empty;
        Phone = Profile.Phone ?? string.Empty;
        Address = Profile.Address ?? string.Empty;
        Rfc = Profile.Rfc ?? string.Empty;

        // Save original values for comparison
        _originalStoreName = StoreName;
        _originalOwnerName = OwnerName;
        _originalPhone = Phone;
        _originalAddress = Address;
        _originalRfc = Rfc;

        TempImagePath = null;
        _isImageRemoved = false;
        NotifyImageProperties();
    }

    [RelayCommand]
    private void SelectImage()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
            Title = "Seleccionar Imagen de Perfil"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var cropWindow = new Views.ImageCropWindow(openFileDialog.FileName);
            cropWindow.Owner = Application.Current.MainWindow;
            if (cropWindow.ShowDialog() == true)
            {
                TempImagePath = cropWindow.CroppedImagePath;
            }
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task RemoveImageAsync()
    {
        var message = new Cajolote.Messages.ShowModalMessage(
            "¿Estás seguro de que deseas eliminar la imagen de perfil?",
            "Eliminar Imagen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        WeakReferenceMessenger.Default.Send(message);
        var result = await message.Tcs.Task;
        if (result == MessageBoxResult.Yes)
        {
            TempImagePath = null;
            _isImageRemoved = true;
            NotifyImageProperties();
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SaveProfileAsync()
    {
        Profile.StoreName = StoreName ?? string.Empty;
        Profile.OwnerName = OwnerName ?? string.Empty;
        Profile.Phone = Phone ?? string.Empty;
        Profile.Address = Address ?? string.Empty;
        Profile.Rfc = Rfc ?? string.Empty;

        if (_isImageRemoved)
        {
            if (!string.IsNullOrEmpty(Profile.ImagePath) && System.IO.File.Exists(Profile.ImagePath))
            {
                try
                {
                    System.IO.File.Delete(Profile.ImagePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al eliminar imagen anterior: {ex.Message}");
                }
            }
            Profile.ImagePath = null;
        }
        else if (!string.IsNullOrEmpty(TempImagePath))
        {
            try
            {
                var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var targetDir = System.IO.Path.Combine(appDataFolder, "Cajolote", "profile_images");
                if (!System.IO.Directory.Exists(targetDir))
                {
                    System.IO.Directory.CreateDirectory(targetDir);
                }

                var ext = System.IO.Path.GetExtension(TempImagePath);
                var uniqueFileName = $"profile_{System.Guid.NewGuid()}{ext}";
                var targetPath = System.IO.Path.Combine(targetDir, uniqueFileName);

                System.IO.File.Copy(TempImagePath, targetPath, overwrite: true);

                if (!string.IsNullOrEmpty(Profile.ImagePath) && System.IO.File.Exists(Profile.ImagePath))
                {
                    try
                    {
                        System.IO.File.Delete(Profile.ImagePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al eliminar imagen anterior reemplazada: {ex.Message}");
                    }
                }

                Profile.ImagePath = targetPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al copiar la imagen de perfil: {ex.Message}");
            }
        }

        Profile.LastUpdated = System.DateTime.UtcNow;
        Profile.IsDirty = true;

        OnPropertyChanged(nameof(SyncStatusText));
        OnPropertyChanged(nameof(SyncStatusColor));
        OnPropertyChanged(nameof(LastUpdatedText));
        NotifyImageProperties();

        using (var context = new CajoloteDbContext())
        {
            if (Profile.Id == 0)
            {
                context.StoreProfiles.Add(Profile);
            }
            else
            {
                context.StoreProfiles.Update(Profile);
            }
            context.SaveChanges();
        }
        StatusMessage = "¡Perfil guardado correctamente!";

        _originalStoreName = StoreName ?? string.Empty;
        _originalOwnerName = OwnerName ?? string.Empty;
        _originalPhone = Phone ?? string.Empty;
        _originalAddress = Address ?? string.Empty;
        _originalRfc = Rfc ?? string.Empty;

        TempImagePath = null;
        _isImageRemoved = false;
        NotifyImageProperties();

        // Notificar a MainWindow y otras partes interesadas
        WeakReferenceMessenger.Default.Send(new Cajolote.Messages.ProfileSyncedMessage());

        // Disparar sincronización a Firestore
        await _syncService.SyncStoreProfileAsync();

        // Volver a cargar el perfil para actualizar el estado a sincronizado
        LoadProfile();

        // Esperar 3 segundos y limpiar el mensaje de éxito
        await System.Threading.Tasks.Task.Delay(3000);
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LogoutAsync()
    {
        var message = new Cajolote.Messages.ShowModalMessage(
            "¿Estás seguro de que deseas cerrar la sesión?",
            "Cerrar Sesión",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        WeakReferenceMessenger.Default.Send(message);
        var result = await message.Tcs.Task;
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _syncService.UnregisterDeviceAsync();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al desvincular el dispositivo de Firestore antes de cerrar sesión: {ex.Message}");
        }

        _authService.Logout();

        var loginWindow = new LoginWindow();
        Application.Current.MainWindow = loginWindow;
        loginWindow.Show();

        // Cerrar todas las demás ventanas abiertas (incluyendo MainWindow y diálogos secundarios)
        var otherWindows = Application.Current.Windows.Cast<Window>().Where(w => w != loginWindow).ToList();
        foreach (var window in otherWindows)
        {
            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cerrar ventana en logout: {ex.Message}");
            }
        }
    }
}
