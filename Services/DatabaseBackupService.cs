using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Cajolote.Data;

namespace Cajolote.Services
{
    public class DatabaseBackupService
    {
        private readonly NetworkService _networkService;
        private readonly FirebaseAuthService _authService;
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;

        private bool _isBackupInProgress = false;

        public DatabaseBackupService(
            NetworkService networkService,
            FirebaseAuthService authService,
            SettingsService settingsService)
        {
            _networkService = networkService;
            _authService = authService;
            _settingsService = settingsService;
            _httpClient = new HttpClient();
        }

        public bool IsBackupInProgress => _isBackupInProgress;

        private string GetStorageBucket()
        {
            string? envBucket = Environment.GetEnvironmentVariable("FIREBASE_STORAGE_BUCKET");
            if (!string.IsNullOrWhiteSpace(envBucket))
            {
                return envBucket;
            }

            string authDomain = _authService.AuthDomain;
            int idx = authDomain.IndexOf('.');
            string projectId = idx > 0 ? authDomain.Substring(0, idx) : authDomain;
            return $"{projectId}.firebasestorage.app";
        }

        /// <summary>
        /// Comprueba si corresponde realizar el respaldo mensual (evaluando primero en memoria si han pasado 30 días o más)
        /// y en caso afirmativo, verifica conexión a internet y realiza el respaldo en segundo plano.
        /// </summary>
        public async Task<bool> CheckAndPerformMonthlyBackupAsync()
        {
            // 1. Evaluación ultraligera en memoria: solo continuar si han transcurrido 30 días o más
            if (!_settingsService.ShouldPerformMonthlyBackup())
            {
                return false;
            }

            // 2. Si no hay conexión a internet, cancelar inmediatamente para evitar errores repentinos
            if (!_networkService.IsOnline)
            {
                System.Diagnostics.Debug.WriteLine("[DatabaseBackupService] Respaldo omitido: Sin conexión a internet.");
                return false;
            }

            // 3. Si no hay sesión autenticada, omitir
            if (!_authService.IsUserAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("[DatabaseBackupService] Respaldo omitido: Usuario no autenticado.");
                return false;
            }

            if (_isBackupInProgress)
            {
                return false;
            }

            return await PerformBackupAsync(isManual: false);
        }

        /// <summary>
        /// Ejecuta el proceso de respaldo de la base de datos a Firebase Storage.
        /// </summary>
        public async Task<bool> PerformBackupAsync(bool isManual = true)
        {
            if (_isBackupInProgress)
            {
                System.Diagnostics.Debug.WriteLine("Ya hay un respaldo en curso.");
                return false;
            }

            if (!_networkService.IsOnline)
            {
                System.Diagnostics.Debug.WriteLine("No se puede realizar el respaldo: sin conexión a internet.");
                return false;
            }

            if (!_authService.IsUserAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("No se puede realizar el respaldo: usuario no autenticado.");
                return false;
            }

            var client = _authService.Client;
            if (client?.User == null)
            {
                System.Diagnostics.Debug.WriteLine("No se puede realizar el respaldo: la sesión de usuario no está activa.");
                return false;
            }

            _isBackupInProgress = true;
            string tempDbSnapshot = Path.Combine(Path.GetTempPath(), $"cajolote_backup_{Guid.NewGuid():N}.db");
            string tempGzFile = tempDbSnapshot + ".gz";

            try
            {
                // 1. Crear un snapshot seguro de SQLite usando VACUUM INTO para no bloquear ni corromper la BD activa
                using (var dbContext = new CajoloteDbContext())
                {
                    if (File.Exists(tempDbSnapshot))
                    {
                        File.Delete(tempDbSnapshot);
                    }

                    // SQLite VACUUM INTO genera una copia completa y consistente de la base de datos
                    string escapedPath = tempDbSnapshot.Replace("'", "''");
                    string vacuumSql = $"VACUUM INTO '{escapedPath}'";
                    await dbContext.Database.ExecuteSqlRawAsync(vacuumSql);
                }

                if (!File.Exists(tempDbSnapshot))
                {
                    throw new FileNotFoundException("No se pudo generar el snapshot de la base de datos.");
                }

                // 2. Comprimir el archivo en GZip para minimizar transferencia y cuota de Firebase Storage
                using (var originalFileStream = File.OpenRead(tempDbSnapshot))
                using (var compressedFileStream = File.Create(tempGzFile))
                using (var compressionStream = new GZipStream(compressedFileStream, CompressionLevel.Optimal))
                {
                    await originalFileStream.CopyToAsync(compressionStream);
                }

                // 3. Preparar subida a Firebase Storage
                string uid = client.User.Info.Uid;
                string bucket = GetStorageBucket();
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string objectName = $"backups/{uid}/{timestamp}_cajolote.db.gz";
                string encodedObjectName = Uri.EscapeDataString(objectName);

                string uploadUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o?uploadType=media&name={encodedObjectName}";

                string idToken = await client.User.GetIdTokenAsync(forceRefresh: true);

                using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                byte[] fileBytes = await File.ReadAllBytesAsync(tempGzFile);
                var content = new ByteArrayContent(fileBytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _settingsService.Settings.LastDatabaseBackupDate = DateTime.UtcNow;
                    _settingsService.SaveSettings();
                    System.Diagnostics.Debug.WriteLine($"Respaldo completado exitosamente: {objectName}");
                    return true;
                }
                else
                {
                    string errContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error al subir respaldo a Firebase Storage: {response.StatusCode} - {errContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción durante el respaldo de base de datos: {ex.Message}");
                return false;
            }
            finally
            {
                // Limpieza de archivos temporales
                try
                {
                    if (File.Exists(tempDbSnapshot)) File.Delete(tempDbSnapshot);
                    if (File.Exists(tempGzFile)) File.Delete(tempGzFile);
                }
                catch { }

                _isBackupInProgress = false;
            }
        }
    }
}
