using Firebase.Auth;
using Firebase.Auth.Providers;
using System;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;

namespace Cajolote.Services
{
    public class FirebaseAuthService
    {
        private FirebaseAuthClient? _client;
        private readonly EncryptedUserRepository _userRepository;
        private readonly NetworkService _networkService;
        public NetworkService NetworkService => _networkService;

        public FirebaseAuthService(NetworkService networkService)
        {
            _networkService = networkService;
            _userRepository = new EncryptedUserRepository("Cajolote");
            InitializeClient();
        }

        private void InitializeClient()
        {
            string apiKey = GetApiKey();
            string authDomain = GetAuthDomain();

            // Evitamos inicializar si no hay una clave de API válida para no generar excepciones de Firebase
            if (string.IsNullOrEmpty(apiKey) || apiKey == "PLACEHOLDER_API_KEY")
            {
                return;
            }

            try
            {
                var config = new FirebaseAuthConfig
                {
                    ApiKey = apiKey,
                    AuthDomain = authDomain,
                    Providers = new FirebaseAuthProvider[]
                    {
                        new EmailProvider()
                    },
                    UserRepository = _userRepository
                };

                _client = new FirebaseAuthClient(config);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al inicializar FirebaseAuthClient: {ex.Message}");
            }
        }

        private string GetApiKey()
        {
            // Retorna la clave literal directamente, o de la variable de entorno si está configurada.
            return Environment.GetEnvironmentVariable("FIREBASE_API_KEY") ?? "AIzaSyADZ2-jIBj1qEOfPdrdpWbXg8SL_ZZOiAA";
        }

        private string GetAuthDomain()
        {
            // Reemplaza "cajolote-pos.firebaseapp.com" por tu dominio de Firebase (generalmente [nombre-proyecto].firebaseapp.com)
            return Environment.GetEnvironmentVariable("FIREBASE_AUTH_DOMAIN") ?? "cajolote-cf123.firebaseapp.com";
        }

        public string ApiKey => GetApiKey();
        public string AuthDomain => GetAuthDomain();

        public FirebaseAuthClient Client
        {
            get
            {
                if (_client == null)
                {
                    InitializeClient();
                    if (_client == null)
                    {
                        throw new InvalidOperationException("Firebase Auth no está configurado (API Key no establecida o es inválida).");
                    }
                }
                return _client;
            }
        }

        // Nos indica si el usuario tiene sesión guardada de manera local
        public bool IsUserAuthenticated
        {
            get
            {
                // Si la sesión local existe en disco, se considera autenticado localmente
                if (_userRepository.UserExists())
                {
                    return true;
                }
                
                // Si ya está cargado en el cliente de Firebase
                return _client?.User != null;
            }
        }

        // Obtiene el correo del usuario actual (desde el cliente de Firebase o deserializando la sesión local)
        public string? CurrentUserEmail
        {
            get
            {
                if (_client?.User != null)
                {
                    return _client.User.Info.Email;
                }
                
                if (_userRepository.UserExists())
                {
                    var (info, _) = _userRepository.ReadUser();
                    return info?.Email;
                }

                return null;
            }
        }

        // Obtiene el UID del usuario actual
        public string? CurrentUserId
        {
            get
            {
                if (_client?.User != null)
                {
                    return _client.User.Info.Uid;
                }

                if (_userRepository.UserExists())
                {
                    var (info, _) = _userRepository.ReadUser();
                    return info?.Uid;
                }

                return null;
            }
        }

        // Iniciar sesión
        public async Task<UserCredential> LoginWithEmailAndPasswordAsync(string email, string password)
        {
            return await Client.SignInWithEmailAndPasswordAsync(email, password);
        }

        // Registro / Creación de cuenta
        public async Task<UserCredential> RegisterWithEmailAndPasswordAsync(string email, string password, string displayName = "")
        {
            return await Client.CreateUserWithEmailAndPasswordAsync(email, password, displayName);
        }

        public DateTime LastSessionRefreshTime => _userRepository.GetLastRefreshed();

        // Nos indica si el correo del usuario actual está verificado
        public bool IsEmailVerified
        {
            get
            {
                if (_client?.User != null)
                {
                    return _client.User.Info.IsEmailVerified;
                }
                
                if (_userRepository.UserExists())
                {
                    var (info, _) = _userRepository.ReadUser();
                    return info?.IsEmailVerified ?? false;
                }

                return false;
            }
        }

        // Enviar correo de verificación de cuenta
        public async Task<bool> SendEmailVerificationAsync()
        {
            if (_client?.User == null)
            {
                throw new InvalidOperationException("No hay un usuario autenticado para enviar el correo de verificación.");
            }

            if (!_networkService.IsOnline)
            {
                await App.ShowMessageAsync(
                    "No se pudo enviar el correo de verificación porque no hay conexión a internet. Por favor, conéctese a internet e intente de nuevo.",
                    "Sin conexión a internet",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return false;
            }

            try
            {
                string idToken = await _client.User.GetIdTokenAsync(forceRefresh: true);
                string apiKey = GetApiKey();
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={apiKey}";

                using var httpClient = new System.Net.Http.HttpClient();
                var payload = new
                {
                    requestType = "VERIFY_EMAIL",
                    idToken = idToken
                };

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch (Firebase.Auth.FirebaseAuthException authEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error de autenticación al enviar correo de verificación: {authEx.Message}");
                
                await _networkService.CheckConnectivityAsync();
                bool isNetworkError = !_networkService.IsOnline;
                
                bool isExplicitAuthError = 
                    authEx.Reason == AuthErrorReason.UserNotFound || 
                    authEx.Reason == AuthErrorReason.UserDisabled ||
                    authEx.Message.Contains("INVALID_GRANT") || 
                    authEx.Message.Contains("INVALID_REFRESH_TOKEN") ||
                    authEx.Message.Contains("USER_NOT_FOUND") || 
                    authEx.Message.Contains("USER_DISABLED");

                if (!isNetworkError && isExplicitAuthError)
                {
                    await HandleSessionInvalidation("La sesión ha expirado o la cuenta de usuario ha sido eliminada/modificada. Se cerrará la sesión.");
                }
                else
                {
                    await App.ShowMessageAsync(
                        "No se pudo enviar el correo de verificación. Verifique su conexión a internet o intente más tarde.",
                        "Error al enviar correo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
                return false;
            }
        }

        // Enviar correo de recuperación de contraseña
        public async Task SendPasswordResetEmailAsync(string email)
        {
            await Client.ResetEmailPasswordAsync(email);
        }

        // Refrescar el token del usuario para actualizar el estado de verificación
        public async Task<bool> RefreshUserStatusAsync()
        {
            if (_client?.User == null) return false;

            if (!_networkService.IsOnline)
            {
                await App.ShowMessageAsync(
                    "No se pudo verificar el estado del correo porque no hay conexión a internet. Por favor, conéctese a internet e intente de nuevo.",
                    "Sin conexión a internet",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return false;
            }
            
            try
            {
                string idToken = await _client.User.GetIdTokenAsync(forceRefresh: true);
                
                // Obtener estado de verificación de email en vivo desde la API REST
                bool emailVerified = await FetchEmailVerificationStatusAsync(idToken);
                
                // Actualizar el cliente de Firebase y el repositorio local
                _client.User.Info.IsEmailVerified = emailVerified;
                _userRepository.SaveUser(_client.User);
                
                return emailVerified;
            }
            catch (Firebase.Auth.FirebaseAuthException authEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error de autenticación al refrescar usuario: {authEx.Message}");
                
                await _networkService.CheckConnectivityAsync();
                bool isNetworkError = !_networkService.IsOnline;
                
                bool isExplicitAuthError = 
                    authEx.Reason == AuthErrorReason.UserNotFound || 
                    authEx.Reason == AuthErrorReason.UserDisabled ||
                    authEx.Message.Contains("INVALID_GRANT") || 
                    authEx.Message.Contains("INVALID_REFRESH_TOKEN") ||
                    authEx.Message.Contains("USER_NOT_FOUND") || 
                    authEx.Message.Contains("USER_DISABLED");

                if (!isNetworkError && isExplicitAuthError)
                {
                    await HandleSessionInvalidation("La cuenta de usuario asociada ha sido eliminada o modificada en el servidor. Se cerrará la sesión actual.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Error de refresco de token ignorado por posible falla de red o error transitorio.");
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al refrescar estado del usuario: {ex.Message}");
                return false;
            }
        }

        // Cierra la sesión local y redirige a la ventana de inicio de sesión
        public async Task HandleSessionInvalidation(string message = "La sesión ha expirado o la cuenta de usuario ha sido modificada/eliminada en el servidor. Se cerrará la sesión por seguridad.")
        {
            await App.ShowMessageAsync(
                message, 
                "Sesión Inválida o Expirada", 
                MessageBoxButton.OK, 
                MessageBoxImage.Warning
            );
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                
                Logout(); // Borra el token local y limpia la sesión
                
                var loginWindow = new LoginWindow();
                System.Windows.Application.Current.MainWindow = loginWindow;
                loginWindow.Show();
                
                // Cerrar todas las demás ventanas (MainWindow, etc.)
                var otherWindows = System.Windows.Application.Current.Windows.Cast<System.Windows.Window>().Where(w => w != loginWindow).ToList();
                foreach (var window in otherWindows)
                {
                    try { window.Close(); } catch {}
                }
            });
        }

        // Cerrar sesión
        public void Logout()
        {
            try
            {
                if (_client != null && _client.User != null)
                {
                    _client.SignOut();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción silenciosa en SignOut: {ex.Message}");
            }
            
            try
            {
                _userRepository.DeleteUser();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al eliminar sesión local: {ex.Message}");
            }
        }

        private async Task<bool> FetchEmailVerificationStatusAsync(string idToken)
        {
            try
            {
                string apiKey = GetApiKey();
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={apiKey}";

                using var httpClient = new System.Net.Http.HttpClient();
                var payload = new { idToken = idToken };
                string json = JsonConvert.SerializeObject(payload);
                using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var lookupResult = JsonConvert.DeserializeObject<FirebaseLookupResult>(responseBody);
                    if (lookupResult?.Users != null && lookupResult.Users.Length > 0)
                    {
                        return lookupResult.Users[0].EmailVerified;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener estado de verificación desde la API REST: {ex.Message}");
            }
            return false;
        }

        private class FirebaseLookupResult
        {
            [JsonProperty("users")]
            public FirebaseUserLookupInfo[]? Users { get; set; }
        }

        private class FirebaseUserLookupInfo
        {
            [JsonProperty("emailVerified")]
            public bool EmailVerified { get; set; }
        }
    }
}
