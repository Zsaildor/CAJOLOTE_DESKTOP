using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Cajolote.Models;
using Cajolote.Services;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;

namespace Cajolote
{
    public partial class LoginWindow : Window
    {
        private readonly FirebaseAuthService _authService;
        private bool _isRegisterMode = false;

        public LoginWindow()
        {
            InitializeComponent();
            _authService = App.Current.Services.GetRequiredService<FirebaseAuthService>();

            // Registrarse para recibir mensajes de diálogo modales
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<Cajolote.Messages.ShowModalMessage>(this, (r, m) =>
            {
                this.Dispatcher.Invoke(() => ShowMessageOverlay(m));
            });
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ToggleMode_Click(object sender, RoutedEventArgs e)
        {
            _isRegisterMode = !_isRegisterMode;
            ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
            RecoveryFormContainer.Visibility = Visibility.Collapsed;
            LoginFormContainer.Visibility = Visibility.Visible;

            if (_isRegisterMode)
            {
                FormTitleTextBlock.Text = "Crear Cuenta";
                SubmitButton.Content = "Registrarse";
                ToggleModePrefixRun.Text = "¿Ya tienes cuenta? ";
                ToggleModeLinkRun.Text = "Iniciar sesión";
                NombreInputContainer.Visibility = Visibility.Visible;
            }
            else
            {
                FormTitleTextBlock.Text = "Iniciar Sesión";
                SubmitButton.Content = "Iniciar Sesión";
                ToggleModePrefixRun.Text = "¿No tienes cuenta? ";
                ToggleModeLinkRun.Text = "Crear cuenta";
                NombreInputContainer.Visibility = Visibility.Collapsed;
            }
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string fullName = NombreTextBox.Text.Trim();

            if (_isRegisterMode && string.IsNullOrEmpty(fullName))
            {
                ShowError("Por favor ingrese su nombre completo.");
                return;
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ShowError("Por favor ingrese el correo y la contraseña.");
                return;
            }

            // Validaciones básicas de formato
            if (!email.Contains("@") || !email.Contains("."))
            {
                ShowError("Por favor ingrese un correo electrónico válido.");
                return;
            }

            if (password.Length < 6)
            {
                ShowError("La contraseña debe tener al menos 6 caracteres.");
                return;
            }

            SetLoadingState(true);
            ErrorMessageTextBlock.Visibility = Visibility.Collapsed;

            try
            {
                if (_isRegisterMode)
                {
                    var result = await _authService.RegisterWithEmailAndPasswordAsync(email, password, fullName);
                    if (result != null && result.User != null)
                    {
                        // Guardar el nombre completo localmente en SQLite
                        try
                        {
                            using (var context = new Cajolote.Data.CajoloteDbContext())
                            {
                                var profile = context.StoreProfiles.FirstOrDefault();
                                if (profile == null)
                                {
                                    profile = new StoreProfile { OwnerName = fullName, LastUpdated = DateTime.UtcNow, IsDirty = true };
                                    context.StoreProfiles.Add(profile);
                                }
                                else
                                {
                                    profile.OwnerName = fullName;
                                    profile.LastUpdated = DateTime.UtcNow;
                                    profile.IsDirty = true;
                                    context.StoreProfiles.Update(profile);
                                }
                                context.SaveChanges();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error al guardar nombre localmente: {ex.Message}");
                        }

                        // Intentar forzar la sincronización a Firestore
                        try
                        {
                            var syncService = App.Current.Services.GetRequiredService<SyncService>();
                            _ = syncService.SyncStoreProfileAsync();
                        }
                        catch {}

                        // Enviar correo electrónico de verificación en segundo plano
                        try
                        {
                            await _authService.SendEmailVerificationAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error al enviar correo de verificación: {ex.Message}");
                        }

                        await App.ShowMessageAsync("¡Cuenta creada exitosamente! Se ha enviado un correo electrónico de verificación a tu dirección. Por favor verifícalo. Iniciando sesión...", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        await CheckStoreProfileAndNavigateAsync(fullName);
                    }
                    else
                    {
                        ShowError("No se pudo crear la cuenta. Intente de nuevo.");
                    }
                }
                else
                {
                    var result = await _authService.LoginWithEmailAndPasswordAsync(email, password);
                    if (result != null && result.User != null)
                    {
                        string fallbackOwnerName = result.User.Info.DisplayName ?? result.User.Info.Email ?? "";
                        await CheckStoreProfileAndNavigateAsync(fallbackOwnerName);
                    }
                    else
                    {
                        ShowError("Correo o contraseña incorrectos.");
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error de autenticación: {ex.Message}");
                // Firebase suele dar mensajes detallados o genéricos según la excepción
                string userFriendlyError = "Error al conectar con Firebase. Verifique su conexión o las credenciales.";
                if (ex.Message.Contains("API key not valid"))
                {
                    userFriendlyError = "La clave API de Firebase configurada no es válida.";
                }
                else if (ex.Message.Contains("EMAIL_EXISTS"))
                {
                    userFriendlyError = "El correo electrónico ya está registrado.";
                }
                else if (ex.Message.Contains("EMAIL_NOT_FOUND") || ex.Message.Contains("INVALID_PASSWORD"))
                {
                    userFriendlyError = "Correo electrónico o contraseña incorrectos.";
                }
                else if (ex.Message.Contains("USER_DISABLED"))
                {
                    userFriendlyError = "Esta cuenta de usuario ha sido deshabilitada.";
                }
                
                ShowError(userFriendlyError);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void ShowError(string message)
        {
            ErrorMessageTextBlock.Text = message;
            ErrorMessageTextBlock.Visibility = Visibility.Visible;
        }

        private void SetLoadingState(bool isLoading)
        {
            SubmitButton.IsEnabled = !isLoading;
            EmailTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
            ToggleModeTextBlock.IsEnabled = !isLoading;
            
            if (isLoading)
            {
                SubmitButton.Content = _isRegisterMode ? "Registrando..." : "Iniciando sesión...";
            }
            else
            {
                SubmitButton.Content = _isRegisterMode ? "Registrarse" : "Iniciar Sesión";
            }
        }

        private void OpenMainWindowAndClose()
        {
            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            this.Close();
        }

        private async Task CheckStoreProfileAndNavigateAsync(string fallbackOwnerName)
        {
            SetLoadingState(true);
            try
            {
                // 1. Sincronizar desde Firestore (si existe)
                var syncService = App.Current.Services.GetRequiredService<SyncService>();
                await syncService.SyncStoreProfileAsync();

                // 2. Consultar el perfil local en SQLite
                StoreProfile? profile;
                using (var context = new Cajolote.Data.CajoloteDbContext())
                {
                    profile = context.StoreProfiles.FirstOrDefault();
                }

                // 3. Si no existe perfil local, o el Nombre de la Tienda está vacío, mostrar pantalla de configuración inicial
                if (profile == null || string.IsNullOrWhiteSpace(profile.StoreName))
                {
                    // Si el perfil no existe, crearlo localmente con el nombre del propietario
                    if (profile == null)
                    {
                        profile = new StoreProfile 
                        { 
                            OwnerName = fallbackOwnerName,
                            LastUpdated = DateTime.UtcNow,
                            IsDirty = true
                        };
                        using (var context = new Cajolote.Data.CajoloteDbContext())
                        {
                            context.StoreProfiles.Add(profile);
                            context.SaveChanges();
                        }
                    }
                    else if (string.IsNullOrEmpty(profile.OwnerName))
                    {
                        profile.OwnerName = fallbackOwnerName;
                        profile.LastUpdated = DateTime.UtcNow;
                        profile.IsDirty = true;
                        using (var context = new Cajolote.Data.CajoloteDbContext())
                        {
                            context.StoreProfiles.Update(profile);
                            context.SaveChanges();
                        }
                    }

                    // Mostrar el formulario de configuración inicial
                    ShowInitialSetupForm();
                }
                else
                {
                    // Perfil completo, abrir ventana principal
                    OpenMainWindowAndClose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al validar perfil de tienda: {ex.Message}");
                // Si falla la verificación (por ejemplo, por red), abrir la principal de todos modos para que sigan offline
                OpenMainWindowAndClose();
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void ShowInitialSetupForm()
        {
            LoginFormContainer.Visibility = Visibility.Collapsed;
            StoreSetupFormContainer.Visibility = Visibility.Visible;
            FormTitleTextBlock.Text = "Configurar Negocio";
            FormSubTitleTextBlock.Text = "Ingresa los datos iniciales de tu tienda";
            ErrorMessageTextBlock.Visibility = Visibility.Collapsed;

            SetupStoreNameTextBox.Text = "";
            SetupRfcTextBox.Text = "";
            SetupPhoneTextBox.Text = "";
            SetupAddressTextBox.Text = "";
        }

        private void SetupSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string storeName = SetupStoreNameTextBox.Text.Trim();
            string rfc = SetupRfcTextBox.Text.Trim();
            string phone = SetupPhoneTextBox.Text.Trim();
            string address = SetupAddressTextBox.Text.Trim();

            if (string.IsNullOrEmpty(storeName))
            {
                ShowError("El nombre de la tienda es obligatorio.");
                return;
            }

            SetupSubmitButton.IsEnabled = false;
            SetupSubmitButton.Content = "Guardando...";

            try
            {
                using (var context = new Cajolote.Data.CajoloteDbContext())
                {
                    var profile = context.StoreProfiles.FirstOrDefault();
                    if (profile == null)
                    {
                        profile = new StoreProfile();
                        context.StoreProfiles.Add(profile);
                    }

                    profile.StoreName = storeName;
                    profile.Rfc = rfc;
                    profile.Phone = phone;
                    profile.Address = address;
                    profile.LastUpdated = DateTime.UtcNow;
                    profile.IsDirty = true;

                    context.StoreProfiles.Update(profile);
                    context.SaveChanges();
                }

                // Sincronizar a Firestore en segundo plano
                var syncService = App.Current.Services.GetRequiredService<SyncService>();
                _ = syncService.SyncStoreProfileAsync();

                OpenMainWindowAndClose();
            }
            catch (Exception ex)
            {
                ShowError($"Error al guardar el perfil: {ex.Message}");
                SetupSubmitButton.IsEnabled = true;
                SetupSubmitButton.Content = "Guardar y Terminar";
            }
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            LoginFormContainer.Visibility = Visibility.Collapsed;
            RecoveryFormContainer.Visibility = Visibility.Visible;
            FormTitleTextBlock.Text = "Recuperar Contraseña";
            FormSubTitleTextBlock.Text = "Restablece tu acceso mediante correo";
            ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
            RecoveryEmailTextBox.Text = EmailTextBox.Text;
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            RecoveryFormContainer.Visibility = Visibility.Collapsed;
            LoginFormContainer.Visibility = Visibility.Visible;
            FormTitleTextBlock.Text = _isRegisterMode ? "Crear Cuenta" : "Iniciar Sesión";
            FormSubTitleTextBlock.Text = "Punto de Venta - Cajolote";
            ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
        }

        private async void RecoverySubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string email = RecoveryEmailTextBox.Text.Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains("@") || !email.Contains("."))
            {
                ShowError("Por favor ingrese un correo electrónico válido.");
                return;
            }

            RecoverySubmitButton.IsEnabled = false;
            RecoverySubmitButton.Content = "Enviando...";
            ErrorMessageTextBlock.Visibility = Visibility.Collapsed;

            try
            {
                await _authService.SendPasswordResetEmailAsync(email);
                await App.ShowMessageAsync("Se ha enviado un enlace para restablecer tu contraseña a tu correo electrónico.", "Recuperación de Contraseña", MessageBoxButton.OK, MessageBoxImage.Information);
                BackToLogin_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al enviar correo de recuperación: {ex.Message}");
                string errorMsg = "Error al enviar el correo. Verifique que la dirección sea correcta o intente más tarde.";
                if (ex.Message.Contains("USER_NOT_FOUND"))
                {
                    errorMsg = "No existe ningún usuario registrado con este correo electrónico.";
                }
                ShowError(errorMsg);
            }
            finally
            {
                RecoverySubmitButton.IsEnabled = true;
                RecoverySubmitButton.Content = "Enviar Enlace";
            }
        }

        private Cajolote.Messages.ShowModalMessage? _currentModalMessage;

        private void ShowMessageOverlay(Cajolote.Messages.ShowModalMessage msg)
        {
            _currentModalMessage = msg;
            OverlayTitle.Text = msg.Title;
            OverlayMessage.Text = msg.Message;

            // Configure Icon
            switch (msg.Icon)
            {
                case MessageBoxImage.Error:
                    OverlayIcon.Text = "❌";
                    break;
                case MessageBoxImage.Warning:
                    OverlayIcon.Text = "⚠️";
                    break;
                case MessageBoxImage.Question:
                    OverlayIcon.Text = "❓";
                    break;
                case MessageBoxImage.Information:
                default:
                    OverlayIcon.Text = "🏪";
                    break;
            }

            // Configure Buttons
            switch (msg.Button)
            {
                case MessageBoxButton.OKCancel:
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnCancel.Content = "Cancelar";
                    BtnOk.Content = "Aceptar";
                    break;
                case MessageBoxButton.YesNo:
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnCancel.Content = "No";
                    BtnOk.Content = "Sí";
                    break;
                case MessageBoxButton.OK:
                default:
                    BtnCancel.Visibility = Visibility.Collapsed;
                    BtnOk.Content = "Aceptar";
                    break;
            }

            NotificationOverlay.Visibility = Visibility.Visible;
            BtnOk.Focus();
        }

        private void OverlayOk_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModalMessage != null)
            {
                var result = BtnOk.Content.ToString() == "Sí" ? MessageBoxResult.Yes : MessageBoxResult.OK;
                _currentModalMessage.Tcs.SetResult(result);
                _currentModalMessage = null;
            }
            NotificationOverlay.Visibility = Visibility.Collapsed;
        }

        private void OverlayCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentModalMessage != null)
            {
                var result = BtnCancel.Content.ToString() == "No" ? MessageBoxResult.No : MessageBoxResult.Cancel;
                _currentModalMessage.Tcs.SetResult(result);
                _currentModalMessage = null;
            }
            NotificationOverlay.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Unregister<Cajolote.Messages.ShowModalMessage>(this);
            base.OnClosed(e);
        }
    }
}
