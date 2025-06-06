using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Client.Helpers;
using Client.Models;
using Newtonsoft.Json;
using System.ComponentModel;

namespace Client.Services
{
    public class SessionService : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly string baseUrl = AppSettings.BaseApiUri + "/api";
        private readonly SignalRService _signalRService;
        private string _connectionStatus;
        private string _connectionId;
        private DateTime? _connectedSince;

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

        public string ConnectionId
        {
            get => _connectionId;
            set
            {
                if (_connectionId != value)
                {
                    _connectionId = value;
                    OnPropertyChanged(nameof(ConnectionId));
                }
            }
        }

        public DateTime? ConnectedSince
        {
            get => _connectedSince;
            set
            {
                if (_connectedSince != value)
                {
                    _connectedSince = value;
                    OnPropertyChanged(nameof(ConnectedSince));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SessionService(SignalRService signalRService)
        {
            _signalRService = signalRService;
            SubscribeToSignalREvents();

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _httpClient = new HttpClient(handler);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                var token = TokenStorage.LoadToken();
                SetAuthToken(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Please login/register first", ex);
            }
        }

        public void SetAuthToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<StartSessionResponse> StartSessionAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{baseUrl}/session/start", null);
                var responseString = await response.Content.ReadAsStringAsync();
                var sessionResponse = JsonConvert.DeserializeObject<StartSessionResponse>(responseString);

                if (!sessionResponse.Success && sessionResponse.Code != "SESSION_EXISTS")
                {
                    throw new HttpRequestException($"Error while starting session: {responseString}");
                }

                if (sessionResponse.Success)
                {
                    Console.WriteLine($"Response when starting sessions: {responseString}");
                    SessionStorage.SaveSession(sessionResponse.Data.SessionId);
                    await _signalRService.ConnectToHubAsync(sessionResponse.Data.SessionId);
                }

                return sessionResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Unable to connect to the server. Please check the URL or if the server is running.", ex);
            }
        }

        public async Task<ApiResponse> JoinSessionAsync(string sessionId)
        {
            sessionId = sessionId.Trim();
            Console.WriteLine($"Joining session ID: {sessionId}");

            var token = TokenStorage.LoadToken();
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Not logged in");
            }

            if (_httpClient.DefaultRequestHeaders.Authorization == null)
            {
                SetAuthToken(token);
            }

            try
            {
                var response = await _httpClient.PostAsync($"{baseUrl}/session/join/{sessionId}", null);
                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                
                Console.WriteLine($"Results join: {responseString}");
                
                if (result == null)
                {
                    throw new HttpRequestException("URL not found while joining");
                }
                
                if (!result.Success)
                {
                    throw new HttpRequestException($"Error while joining session: {responseString}");
                }

                await _signalRService.ConnectToHubAsync(sessionId);
                SessionStorage.SaveSession(sessionId);
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Unable to connect to the server. Please check the URL or if the server is running.", ex);
            }
        }

        public async Task<ApiResponse> LeaveSessionAsync(string sessionId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{baseUrl}/session/stop/{sessionId}", null);
                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);

                if (result == null)
                {
                    return null;
                }

                Console.WriteLine($"[Debug]Response when leaving: {responseString}");
                await _signalRService.DisconnectAsync();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Error while leaving session: {ex.Message}");
                throw new HttpRequestException(
                    "Unable to connect to the server or there's an error while leaving the session", ex);
            }
        }

        public async Task<SessionResponse> GetActiveSessionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{baseUrl}/session/active");
                var responseString = await response.Content.ReadAsStringAsync();
                var sessionResponse = JsonConvert.DeserializeObject<SessionResponse>(responseString);

                if (!sessionResponse.Success)
                {
                    throw new HttpRequestException($"Error while getting active session: {responseString}");
                }

                if (sessionResponse.Code == "NO_SESSIONS")
                {
                    Console.WriteLine("No active session found, please start a new session!");
                }

                Console.WriteLine($"Response get active session: {responseString}");
                return sessionResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Unable to connect to the server or there's an error while getting active session", ex);
            }
        }

        public async Task<ApiResponse> ConnectToSessionAsync(string sessionId, string connectionId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/session/connect/{sessionId}");
                request.Headers.Add("X-SignalR-Connection-Id", connectionId);
                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"[Debug] Connecting response: {responseString}");
                var sessionResponse = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error connecting to session: {responseString}");
                }
                
                return sessionResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting: {ex.Message}");
                throw new HttpRequestException(
                    "Unable to connect to the server or there's an error while connecting to the session", ex);
            }
        }

        public async Task<ApiResponse> DisconnectFromSessionAsync(string sessionId, string connectionId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/session/disconnect/{sessionId}");
                request.Headers.Add("X-SignalR-Connection-Id", connectionId);
                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"[Debug] Disconnecting response: {responseString}");
                var sessionResponse = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error disconnecting from session: {responseString}");
                }
                
                return sessionResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting: {ex.Message}");
                throw new HttpRequestException(
                    "Unable to connect to the server or there's an error while disconnecting from the session", ex);
            }
        }

        private void SubscribeToSignalREvents()
        {
            _signalRService.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(SignalRService.ConnectionStatus):
                        ConnectionStatus = _signalRService.ConnectionStatus;
                        break;
                    case nameof(SignalRService.ConnectionId):
                        ConnectionId = _signalRService.ConnectionId;
                        if (!string.IsNullOrEmpty(_signalRService.ConnectionId))
                        {
                            ConnectedSince = DateTime.UtcNow;
                        }
                        break;
                    case nameof(SignalRService.IsConnected):
                        if (!_signalRService.IsConnected)
                        {
                            ConnectedSince = null;
                        }
                        break;
                }
            };
        }
    }
}
