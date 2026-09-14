using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Cajolote.Data;
using Cajolote.Models;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.Messaging;
using Firebase.Auth;

namespace Cajolote.Services
{
    public class SyncService
    {
        private readonly NetworkService _networkService;
        private readonly FirebaseAuthService _authService;
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;
        private bool _deviceSyncedThisSession = false;

        public SyncService(NetworkService networkService, FirebaseAuthService authService, SettingsService settingsService)
        {
            _networkService = networkService;
            _authService = authService;
            _settingsService = settingsService;
            _httpClient = new HttpClient();

            // Suscribirse al evento de cambio de conectividad
            _networkService.ConnectivityChanged += async (sender, isOnline) =>
            {
                if (isOnline)
                {
                    System.Diagnostics.Debug.WriteLine("Conexión a internet restablecida. Iniciando sincronización...");
                    await SyncStoreProfileAsync();
                }
            };
        }

        private string GetProjectId()
        {
            // Extraer el project ID directamente del dominio expuesto por el servicio de autenticación
            string authDomain = _authService.AuthDomain;
            int idx = authDomain.IndexOf('.');
            return idx > 0 ? authDomain.Substring(0, idx) : authDomain;
        }

        public async Task<bool> SyncStoreProfileAsync()
        {
            // Validar conexión a internet y estado de autenticación de Firebase
            if (!_networkService.IsOnline)
            {
                System.Diagnostics.Debug.WriteLine("No se puede sincronizar: sin conexión a internet.");
                return false;
            }

            if (!_authService.IsUserAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("No se puede sincronizar: usuario no autenticado.");
                return false;
            }

            // Sincronizar dispositivo en segundo plano si es necesario
            _ = SyncDeviceIfNeededAsync();

            // Obtener el cliente y usuario actual
            var client = _authService.Client;
            if (client?.User == null)
            {
                System.Diagnostics.Debug.WriteLine("No se puede sincronizar: la sesión del usuario no está cargada.");
                return false;
            }

            string uid = client.User.Info.Uid;
            string projectId = GetProjectId();
            string apiKey = _authService.ApiKey;

            // Cargar el perfil de la tienda almacenado localmente en SQLite
            StoreProfile? profile;
            using (var dbContext = new CajoloteDbContext())
            {
                profile = dbContext.StoreProfiles.FirstOrDefault();
            }

            try
            {
                // Obtener el JWT ID Token actual (refrescado automáticamente si es necesario)
                string idToken = await client.User.GetIdTokenAsync(forceRefresh: true);
                string docUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/stores/{uid}?key={apiKey}";

                // GET para consultar el documento remoto en Firestore
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, docUrl);
                getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var getResponse = await _httpClient.SendAsync(getRequest);
                
                bool isNewRemoteDocument = false;
                DateTime remoteLastUpdated = DateTime.MinValue;
                string remoteStoreName = "";
                string remoteOwnerName = "";
                string remotePhone = "";
                string remoteAddress = "";
                string remoteRfc = "";

                if (getResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    isNewRemoteDocument = true;
                }
                else if (getResponse.IsSuccessStatusCode)
                {
                    string getBody = await getResponse.Content.ReadAsStringAsync();
                    dynamic? remoteDoc = JsonConvert.DeserializeObject(getBody);
                    
                    if (remoteDoc?.fields != null)
                    {
                        remoteStoreName = remoteDoc.fields.storeName?.stringValue ?? "";
                        remoteOwnerName = remoteDoc.fields.ownerName?.stringValue ?? "";
                        remotePhone = remoteDoc.fields.phone?.stringValue ?? "";
                        remoteAddress = remoteDoc.fields.address?.stringValue ?? "";
                        remoteRfc = remoteDoc.fields.rfc?.stringValue ?? "";
                    }

                    // updateTime se encuentra en la raíz del documento JSON de Firestore
                    DateTime? remoteUpdateTime = (DateTime?)remoteDoc?.updateTime;
                    if (remoteUpdateTime.HasValue)
                    {
                        remoteLastUpdated = remoteUpdateTime.Value.ToUniversalTime();
                    }
                }
                else
                {
                    string errContent = await getResponse.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error de lectura en Firestore GET ({getResponse.StatusCode}): {errContent}");
                    isNewRemoteDocument = true;
                }

                // Determinar marcas de tiempo de modificación para comparación
                DateTime localLastUpdated = DateTime.MinValue;
                if (profile != null)
                {
                    localLastUpdated = profile.LastUpdated.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(profile.LastUpdated, DateTimeKind.Utc)
                        : profile.LastUpdated.ToUniversalTime();
                }

                bool shouldSyncUp = false;
                bool shouldSyncDown = false;

                if (profile != null && profile.IsDirty)
                {
                    // Si el perfil local tiene cambios pendientes, se suben prioritariamente
                    shouldSyncUp = true;
                }
                else if (isNewRemoteDocument)
                {
                    // Si no hay datos remotos pero sí locales, subimos locales
                    if (profile != null)
                    {
                        shouldSyncUp = true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No hay perfil local ni remoto para sincronizar.");
                        return true;
                    }
                }
                else
                {
                    if (profile == null)
                    {
                        // No hay datos locales pero sí remotos, descargamos remotos
                        shouldSyncDown = true;
                    }
                    else
                    {
                        // Ambos existen, comparar marcas de tiempo del servidor
                        // Si la fecha de actualización remota difiere del registro local
                        if (localLastUpdated != remoteLastUpdated)
                        {
                            shouldSyncDown = true;
                        }
                        else
                        {
                            // Son iguales, ya están sincronizados
                            System.Diagnostics.Debug.WriteLine("El perfil local y remoto están completamente en sincronía.");
                            return true;
                        }
                    }
                }

                if (shouldSyncDown)
                {
                    System.Diagnostics.Debug.WriteLine("Sincronizando datos de Firestore hacia SQLite local (Sync Down)...");
                    using (var dbContext = new CajoloteDbContext())
                    {
                        var localProfile = dbContext.StoreProfiles.FirstOrDefault();
                        if (localProfile == null)
                        {
                            localProfile = new StoreProfile();
                            dbContext.StoreProfiles.Add(localProfile);
                        }

                        localProfile.StoreName = remoteStoreName;
                        localProfile.OwnerName = remoteOwnerName;
                        localProfile.Phone = remotePhone;
                        localProfile.Address = remoteAddress;
                        localProfile.Rfc = remoteRfc;
                        localProfile.LastUpdated = remoteLastUpdated; // Conservar la marca de tiempo exacta de Firestore
                        localProfile.IsDirty = false; // Descarga limpia

                        dbContext.SaveChanges();
                    }

                    // Enviar mensaje en tiempo real para actualizar la interfaz gráfica
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Cajolote.Messages.ProfileSyncedMessage());

                    System.Diagnostics.Debug.WriteLine("Sincronización hacia SQLite local finalizada con éxito.");
                    return true;
                }

                if (shouldSyncUp && profile != null)
                {
                    System.Diagnostics.Debug.WriteLine("Sincronizando datos locales hacia Firestore (Sync Up)...");
                    
                    // Estructura compatible con la API de documentos de Cloud Firestore REST API
                    var payload = new
                    {
                        fields = new
                        {
                            storeName = new { stringValue = profile.StoreName ?? "" },
                            ownerName = new { stringValue = profile.OwnerName ?? "" },
                            phone = new { stringValue = profile.Phone ?? "" },
                            address = new { stringValue = profile.Address ?? "" },
                            rfc = new { stringValue = profile.Rfc ?? "" }
                        }
                    };

                    string jsonContent = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    // PATCH realiza upsert
                    using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, docUrl)
                    {
                        Content = content
                    };
                    patchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                    var patchResponse = await _httpClient.SendAsync(patchRequest);
                    if (patchResponse.IsSuccessStatusCode)
                    {
                        string patchBody = await patchResponse.Content.ReadAsStringAsync();
                        dynamic? patchDoc = JsonConvert.DeserializeObject(patchBody);
                        
                        DateTime serverUpdateTime = DateTime.UtcNow;
                        DateTime? serverUpdateTimeParsed = (DateTime?)patchDoc?.updateTime;
                        if (serverUpdateTimeParsed.HasValue)
                        {
                            serverUpdateTime = serverUpdateTimeParsed.Value.ToUniversalTime();
                        }

                        using (var dbContext = new CajoloteDbContext())
                        {
                            var localProfile = dbContext.StoreProfiles.FirstOrDefault();
                            if (localProfile != null)
                            {
                                localProfile.LastUpdated = serverUpdateTime; // Guardar la marca de tiempo oficial del servidor
                                localProfile.IsDirty = false; // Ya no hay cambios pendientes
                                dbContext.SaveChanges();
                            }
                        }

                        // Enviar mensaje en tiempo real para actualizar la interfaz gráfica
                        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Cajolote.Messages.ProfileSyncedMessage());

                        System.Diagnostics.Debug.WriteLine($"Sincronización exitosa con Firestore (Sync Up) para: {uid}");
                        return true;
                    }
                    else
                    {
                        string errorResponse = await patchResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Error de Firestore REST API PATCH ({patchResponse.StatusCode}): {errorResponse}");
                        await App.ShowMessageAsync(
                            $"Error al sincronizar con Firestore ({patchResponse.StatusCode}):\n{errorResponse}", 
                            "Error de Sincronización", 
                            System.Windows.MessageBoxButton.OK, 
                            System.Windows.MessageBoxImage.Warning
                        );
                        return false;
                    }
                }

                return false;
            }
            catch (Firebase.Auth.FirebaseAuthException authEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error de autenticación crítico en sincronización: {authEx.Message} (Razon: {authEx.Reason})");
                
                await _authService.NetworkService.CheckConnectivityAsync();
                bool isNetworkError = !_authService.NetworkService.IsOnline;
                
                bool isExplicitAuthError = 
                    authEx.Reason == AuthErrorReason.UserNotFound || 
                    authEx.Reason == AuthErrorReason.UserDisabled ||
                    authEx.Message.Contains("INVALID_GRANT") || 
                    authEx.Message.Contains("INVALID_REFRESH_TOKEN") ||
                    authEx.Message.Contains("USER_NOT_FOUND") || 
                    authEx.Message.Contains("USER_DISABLED");

                if (!isNetworkError && isExplicitAuthError)
                {
                    await _authService.HandleSessionInvalidation("Tu sesión de Firebase no es válida, la cuenta ha sido eliminada o modificada en el servidor. Se cerrará la sesión por seguridad.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Sincronización abortada por error de red o error transitorio de Firebase.");
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción durante la sincronización a Firestore: {ex.Message}");
                await App.ShowMessageAsync(
                    $"Excepción durante la sincronización:\n{ex.Message}", 
                    "Error de Sincronización", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error
                );
                return false;
            }
        }

        public async Task<bool> SyncDeviceIfNeededAsync()
        {
            if (!_networkService.IsOnline)
            {
                System.Diagnostics.Debug.WriteLine("No se puede sincronizar dispositivo: sin conexión a internet.");
                return false;
            }

            if (!_authService.IsUserAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("No se puede sincronizar dispositivo: usuario no autenticado.");
                return false;
            }

            var client = _authService.Client;
            if (client?.User == null)
            {
                System.Diagnostics.Debug.WriteLine("No se puede sincronizar dispositivo: sesión no cargada.");
                return false;
            }

            string uid = client.User.Info.Uid;
            string projectId = GetProjectId();
            string apiKey = _authService.ApiKey;
            string currentDeviceName = Environment.MachineName;

            // 1. Si el dispositivo en la configuración es diferente al actual y no está vacío, desvincular el anterior en Firestore
            string lastSynced = _settingsService.Settings.LastSyncedDeviceName;
            if (!string.IsNullOrEmpty(lastSynced) && lastSynced != currentDeviceName)
            {
                System.Diagnostics.Debug.WriteLine($"Nombre del dispositivo cambió de '{lastSynced}' a '{currentDeviceName}'. Intentando eliminar anterior de Firestore...");
                try
                {
                    string idTokenForDelete = await client.User.GetIdTokenAsync(forceRefresh: true);
                    string escapedLastSynced = Uri.EscapeDataString(lastSynced);
                    string deleteUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/stores/{uid}/devices/{escapedLastSynced}?key={apiKey}";
                    
                    using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                    deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idTokenForDelete);
                    var deleteResponse = await _httpClient.SendAsync(deleteRequest);
                    
                    if (deleteResponse.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"Dispositivo anterior '{lastSynced}' eliminado con éxito de Firestore.");
                    }
                    else
                    {
                        string errContent = await deleteResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Error al eliminar dispositivo anterior ({deleteResponse.StatusCode}): {errContent}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Excepción al intentar eliminar dispositivo anterior: {ex.Message}");
                }

                _settingsService.Settings.LastSyncedDeviceName = string.Empty;
                _settingsService.Settings.DeviceSyncPending = true;
                _settingsService.SaveSettings();
            }

            // 2. Si hay una sincronización pendiente o no se ha sincronizado en esta sesión, sincronizar
            if (_settingsService.Settings.DeviceSyncPending || !_deviceSyncedThisSession)
            {
                System.Diagnostics.Debug.WriteLine($"Sincronizando información del dispositivo actual '{currentDeviceName}' con Firestore...");
                try
                {
                    string idToken = await client.User.GetIdTokenAsync(forceRefresh: true);
                    string escapedDeviceName = Uri.EscapeDataString(currentDeviceName);
                    string docUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/stores/{uid}/devices/{escapedDeviceName}?key={apiKey}";

                    var payload = new
                    {
                        fields = new
                        {
                            deviceName = new { stringValue = currentDeviceName },
                            lastActive = new { stringValue = DateTime.UtcNow.ToString("o") }
                        }
                    };

                    string jsonContent = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, docUrl)
                    {
                        Content = content
                    };
                    patchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                    var patchResponse = await _httpClient.SendAsync(patchRequest);
                    if (patchResponse.IsSuccessStatusCode)
                    {
                        _settingsService.Settings.LastSyncedDeviceName = currentDeviceName;
                        _settingsService.Settings.DeviceSyncPending = false;
                        _settingsService.SaveSettings();

                        _deviceSyncedThisSession = true;
                        System.Diagnostics.Debug.WriteLine("Dispositivo sincronizado con éxito en Firestore.");
                        return true;
                    }
                    else
                    {
                        string errorResponse = await patchResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Error de Firestore REST API al sincronizar dispositivo ({patchResponse.StatusCode}): {errorResponse}");
                        
                        _settingsService.Settings.DeviceSyncPending = true;
                        _settingsService.SaveSettings();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Excepción al sincronizar dispositivo: {ex.Message}");
                    _settingsService.Settings.DeviceSyncPending = true;
                    _settingsService.SaveSettings();
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> UnregisterDeviceAsync()
        {
            string lastSynced = _settingsService.Settings.LastSyncedDeviceName;
            if (string.IsNullOrEmpty(lastSynced))
            {
                System.Diagnostics.Debug.WriteLine("No hay dispositivo registrado localmente para desvincular.");
                return true;
            }

            if (!_networkService.IsOnline)
            {
                System.Diagnostics.Debug.WriteLine("Sin conexión a internet. No se puede eliminar el dispositivo de Firestore.");
                return false;
            }

            if (!_authService.IsUserAuthenticated)
            {
                System.Diagnostics.Debug.WriteLine("Usuario no autenticado. No se puede desvincular el dispositivo.");
                return false;
            }

            var client = _authService.Client;
            if (client?.User == null)
            {
                return false;
            }

            string uid = client.User.Info.Uid;
            string projectId = GetProjectId();
            string apiKey = _authService.ApiKey;

            System.Diagnostics.Debug.WriteLine($"Desvinculando dispositivo '{lastSynced}' de Firestore antes de cerrar sesión...");
            try
            {
                string idToken = await client.User.GetIdTokenAsync(forceRefresh: true);
                string escapedLastSynced = Uri.EscapeDataString(lastSynced);
                string deleteUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/stores/{uid}/devices/{escapedLastSynced}?key={apiKey}";

                using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var deleteResponse = await _httpClient.SendAsync(deleteRequest);
                if (deleteResponse.IsSuccessStatusCode)
                {
                    _settingsService.Settings.LastSyncedDeviceName = string.Empty;
                    _settingsService.Settings.DeviceSyncPending = false;
                    _settingsService.SaveSettings();

                    _deviceSyncedThisSession = false;
                    System.Diagnostics.Debug.WriteLine("Dispositivo desvinculado con éxito en Firestore.");
                    return true;
                }
                else
                {
                    string errorResponse = await deleteResponse.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error al eliminar dispositivo de Firestore ({deleteResponse.StatusCode}): {errorResponse}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción al eliminar dispositivo: {ex.Message}");
                return false;
            }
        }
    }
}
