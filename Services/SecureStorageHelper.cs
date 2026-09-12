using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cajolote.Models;

namespace Cajolote.Services
{
    public static class SecureStorageHelper
    {
        private static readonly string SessionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cajolote",
            "session.dat"
        );

        public static void SaveSession(LocalSession session)
        {
            try
            {
                var directory = Path.GetDirectoryName(SessionFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(session);
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(json);
                
                // Cifrar usando DPAPI (alcance del usuario actual de Windows)
                byte[] ciphertextBytes = ProtectedData.Protect(plaintextBytes, null, DataProtectionScope.CurrentUser);
                
                File.WriteAllBytes(SessionFilePath, ciphertextBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar sesión segura: {ex.Message}");
            }
        }

        public static LocalSession? LoadSession()
        {
            try
            {
                if (!File.Exists(SessionFilePath)) return null;

                byte[] ciphertextBytes = File.ReadAllBytes(SessionFilePath);
                
                // Descifrar usando DPAPI
                byte[] plaintextBytes = ProtectedData.Unprotect(ciphertextBytes, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plaintextBytes);
                
                return JsonSerializer.Deserialize<LocalSession>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar sesión segura: {ex.Message}");
                // Si la sesión se corrompe o no se puede descifrar (p.ej. cambio de credenciales de Windows), la eliminamos
                ClearSession();
                return null;
            }
        }

        public static void ClearSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    File.Delete(SessionFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al eliminar archivo de sesión: {ex.Message}");
            }
        }
    }
}
