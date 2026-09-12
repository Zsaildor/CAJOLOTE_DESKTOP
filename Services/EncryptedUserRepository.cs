using Firebase.Auth;
using Firebase.Auth.Repository;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Cajolote.Services
{
    public class UserSerializedData
    {
        public UserInfo Info { get; set; } = null!;
        public FirebaseCredential Credential { get; set; } = null!;
        public DateTime LastRefreshed { get; set; } = DateTime.MinValue;
    }

    public class EncryptedUserRepository : IUserRepository
    {
        private readonly string _filePath;

        public EncryptedUserRepository(string appName)
        {
            _filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName,
                "session.dat"
            );
        }

        public bool UserExists()
        {
            return File.Exists(_filePath);
        }

        public (UserInfo, FirebaseCredential) ReadUser()
        {
            try
            {
                if (!File.Exists(_filePath)) return (null!, null!);

                byte[] ciphertextBytes = File.ReadAllBytes(_filePath);
                byte[] plaintextBytes = ProtectedData.Unprotect(ciphertextBytes, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plaintextBytes);
                
                var data = JsonConvert.DeserializeObject<UserSerializedData>(json);
                if (data != null && data.Info != null && data.Credential != null)
                {
                    return (data.Info, data.Credential);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al leer usuario cifrado: {ex.Message}");
                DeleteUser();
            }
            return (null!, null!);
        }

        public void SaveUser(User user)
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = new UserSerializedData
                {
                    Info = user.Info,
                    Credential = user.Credential,
                    LastRefreshed = DateTime.UtcNow
                };

                string json = JsonConvert.SerializeObject(data);
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(json);
                byte[] ciphertextBytes = ProtectedData.Protect(plaintextBytes, null, DataProtectionScope.CurrentUser);
                
                File.WriteAllBytes(_filePath, ciphertextBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar usuario cifrado: {ex.Message}");
            }
        }

        public DateTime GetLastRefreshed()
        {
            try
            {
                if (!File.Exists(_filePath)) return DateTime.MinValue;

                byte[] ciphertextBytes = File.ReadAllBytes(_filePath);
                byte[] plaintextBytes = ProtectedData.Unprotect(ciphertextBytes, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plaintextBytes);
                
                var data = JsonConvert.DeserializeObject<UserSerializedData>(json);
                if (data != null)
                {
                    return data.LastRefreshed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener fecha de actualización de sesión: {ex.Message}");
            }
            return DateTime.MinValue;
        }

        public void DeleteUser()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al eliminar usuario cifrado: {ex.Message}");
            }
        }
    }
}
