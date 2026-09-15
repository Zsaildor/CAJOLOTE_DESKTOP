using CommunityToolkit.Mvvm.ComponentModel;
using Cajolote.Services;
using System;
using System.Printing;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cajolote.ViewModels
{
    public partial class ConfiguracionViewModel : ObservableObject
    {
        private readonly FirebaseAuthService _authService;
        private readonly SettingsService _settingsService;

        public ObservableCollection<string> InstalledPrinters { get; } = new();

        public ConfiguracionViewModel(FirebaseAuthService authService, SettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
            _selectedPrintModelIndex = _settingsService.Settings.AlwaysAskBeforePrinting ? 1 : 0;
            _selectedPrinter = _settingsService.Settings.PrinterName;
            _selectedBulkUnitIndex = _settingsService.Settings.BulkUnit == "g" ? 1 : 0;
            _selectedRapidCardsLayoutIndex = _settingsService.Settings.RapidCardsLayout == "Con divisiones" ? 1 : 0;

            LoadInstalledPrinters();
            UpdateVerificationStatus();
        }

        private void LoadInstalledPrinters()
        {
            try
            {
                using (var printServer = new LocalPrintServer())
                {
                    var printQueues = printServer.GetPrintQueues(new[] { 
                        EnumeratedPrintQueueTypes.Local, 
                        EnumeratedPrintQueueTypes.Connections 
                    });
                    
                    InstalledPrinters.Clear();
                    foreach (var queue in printQueues)
                    {
                        InstalledPrinters.Add(queue.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al listar impresoras del sistema: {ex.Message}");
            }
        }

        public string DeviceName => System.Environment.MachineName;

        public bool IsUserAuthenticated => _authService.IsUserAuthenticated;

        public string SessionStatusColor => IsUserAuthenticated ? "#7ACB8A" : "#BDC3C7";

        public string LastSessionRefreshText
        {
            get
            {
                if (!IsUserAuthenticated) return "Sin sesión activa";

                DateTime refreshTime = _authService.LastSessionRefreshTime;
                if (refreshTime == DateTime.MinValue)
                {
                    return "No disponible";
                }

                return refreshTime.ToLocalTime().ToString("dd/MM/yyyy hh:mm:ss tt");
            }
        }

        [ObservableProperty]
        private int _selectedPrintModelIndex;

        partial void OnSelectedPrintModelIndexChanged(int value)
        {
            _settingsService.Settings.AlwaysAskBeforePrinting = (value == 1);
            _settingsService.SaveSettings();
        }

        [ObservableProperty]
        private string _selectedPrinter;

        partial void OnSelectedPrinterChanged(string value)
        {
            _settingsService.Settings.PrinterName = value ?? string.Empty;
            _settingsService.SaveSettings();
        }

        [ObservableProperty]
        private int _selectedBulkUnitIndex;

        partial void OnSelectedBulkUnitIndexChanged(int value)
        {
            _settingsService.Settings.BulkUnit = (value == 1) ? "g" : "kg";
            _settingsService.SaveSettings();
        }

        [ObservableProperty]
        private int _selectedRapidCardsLayoutIndex;

        partial void OnSelectedRapidCardsLayoutIndexChanged(int value)
        {
            _settingsService.Settings.RapidCardsLayout = (value == 1) ? "Con divisiones" : "Sin divisiones";
            _settingsService.SaveSettings();
        }

        [ObservableProperty]
        private string _verificationStatusText = string.Empty;

        [ObservableProperty]
        private bool _canResendVerification;

        [ObservableProperty]
        private string _resendStatusMessage = string.Empty;

        [ObservableProperty]
        private string _resendStatusColor = "#E74C3C";

        private void UpdateVerificationStatus()
        {
            if (!IsUserAuthenticated)
            {
                VerificationStatusText = "Sin sesión activa";
                CanResendVerification = false;
                return;
            }

            if (_authService.IsEmailVerified)
            {
                VerificationStatusText = "Correo verificado ✓";
                CanResendVerification = false;
                ResendStatusColor = "#7ACB8A";
            }
            else
            {
                VerificationStatusText = "Correo sin verificar ⚠";
                CanResendVerification = true;
                ResendStatusColor = "#E67E22";
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private async System.Threading.Tasks.Task ResendVerificationEmailAsync()
        {
            if (!CanResendVerification) return;

            ResendStatusMessage = "Enviando...";
            try
            {
                bool success = await _authService.SendEmailVerificationAsync();
                if (success)
                {
                    ResendStatusMessage = "¡Correo enviado! Revisa tu bandeja.";
                }
                else
                {
                    ResendStatusMessage = "Error al enviar el correo.";
                }
            }
            catch (Exception ex)
            {
                ResendStatusMessage = $"Error: {ex.Message}";
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private async System.Threading.Tasks.Task RefreshVerificationStatusAsync()
        {
            if (!IsUserAuthenticated) return;
            
            ResendStatusMessage = "Verificando...";
            bool verified = await _authService.RefreshUserStatusAsync();
            UpdateVerificationStatus();
            OnPropertyChanged(nameof(LastSessionRefreshText));
            
            if (verified)
            {
                ResendStatusMessage = "¡Tu correo ya está verificado!";
            }
            else
            {
                ResendStatusMessage = "El correo sigue sin estar verificado.";
            }
        }

        public string LastBackupText
        {
            get
            {
                if (_settingsService.Settings.LastDatabaseBackupDate.HasValue)
                {
                    return _settingsService.Settings.LastDatabaseBackupDate.Value.ToLocalTime().ToString("dd/MM/yyyy hh:mm tt");
                }
                return "Ninguno registrado";
            }
        }
    }
}
