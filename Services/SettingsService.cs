using System;
using System.IO;
using System.Text.Json;

namespace Cajolote.Services
{
    public class UserSettings
    {
        public bool AlwaysAskBeforePrinting { get; set; } = false;
        public string PrinterName { get; set; } = string.Empty;
        public string BulkUnit { get; set; } = "kg";
        public string LastSyncedDeviceName { get; set; } = string.Empty;
        public bool DeviceSyncPending { get; set; } = false;
        public string RapidCardsLayout { get; set; } = "Sin divisiones";
        public DateTime? LastDatabaseBackupDate { get; set; } = null;
    }

    public class SettingsService
    {
        private readonly string _filePath;
        private UserSettings _settings;

        public SettingsService()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cajolote");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                _filePath = Path.Combine(folder, "settings.json");
                _settings = LoadSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al inicializar SettingsService: {ex.Message}");
                _settings = new UserSettings();
                _filePath = string.Empty;
            }
        }

        public UserSettings Settings => _settings;

        private UserSettings LoadSettings()
        {
            try
            {
                if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar configuración: {ex.Message}");
            }
            return new UserSettings();
        }

        public bool ShouldPerformMonthlyBackup()
        {
            if (!_settings.LastDatabaseBackupDate.HasValue) return true;

            var last = _settings.LastDatabaseBackupDate.Value;
            var now = DateTime.UtcNow;

            // Evaluación simple y ultraligera en memoria: ¿han transcurrido 30 días o más?
            return (now - last).TotalDays >= 30;
        }

        public void SaveSettings()
        {
            try
            {
                if (!string.IsNullOrEmpty(_filePath))
                {
                    string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_filePath, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar configuración: {ex.Message}");
            }
        }
    }
}
