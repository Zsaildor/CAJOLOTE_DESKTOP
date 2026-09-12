using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Cajolote.Services
{
    public class NetworkService
    {
        private readonly HttpClient _httpClient;
        private bool _isOnline;
        
        public bool IsOnline
        {
            get => _isOnline;
            private set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    ConnectivityChanged?.Invoke(this, _isOnline);
                }
            }
        }

        public event EventHandler<bool>? ConnectivityChanged;

        public NetworkService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            
            // Suscribirse a cambios en las interfaces de red de Windows
            NetworkChange.NetworkAddressChanged += (s, e) => _ = CheckConnectivityAsync();
            
            // Primer chequeo inmediato
            _ = CheckConnectivityAsync();
            
            // Chequeo periódico en segundo plano para corroborar conectividad
            _ = StartPeriodicCheckAsync();
        }

        public async Task CheckConnectivityAsync()
        {
            try
            {
                // Hacemos una petición rápida al dominio de Firestore para verificar internet estable
                var request = new HttpRequestMessage(HttpMethod.Head, "https://firestore.googleapis.com/");
                var response = await _httpClient.SendAsync(request);
                IsOnline = response.IsSuccessStatusCode || (int)response.StatusCode < 500;
            }
            catch
            {
                IsOnline = false;
            }
        }

        private async Task StartPeriodicCheckAsync()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                await CheckConnectivityAsync();
            }
        }
    }
}
