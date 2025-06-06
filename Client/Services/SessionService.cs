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
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync($"{baseUrl}/session/start", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Không thể kết nối đến máy chủ. Vui lòng kiểm tra URL hoặc server đang chạy.", ex);
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var sessionResponse = JsonConvert.DeserializeObject<StartSessionResponse>(responseString);

            if (!sessionResponse.Success & sessionResponse.Code != "SESSION_EXISTS")
                throw new HttpRequestException($"[Error]Error while starting API in SessionService: {responseString}");
            else if(sessionResponse.Code == "SESSION_EXISTS")
            {
                return sessionResponse;
            }
            Console.WriteLine($"Response when starting sessions: {responseString}");
            SessionStorage.SaveSession(sessionResponse.Data.SessionId);
            return sessionResponse;
        }

        public async Task<ApiResponse> LeaveSessionAsync(string sessionId)
        {
            HttpResponseMessage response;
            
            try
            {
                response = await _httpClient.PostAsync($"{baseUrl}/session/stop/{sessionId}", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "[Error]Unable to stop session: ");
                return null;
            }
            try
            {
                var responseString = await response.Content.ReadAsStringAsync();

                var Response = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                if(Response is null)
                {
                    return null;
                }    
                Console.WriteLine($"[Debug]Response when leaving: {responseString}");
                return Response;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Error] Error while leaving sessionId: ", ex.Message);
                throw new HttpRequestException(
                    "Unable to connect to the server or there's some error while leaving sessionId", ex);
            }
        }
        public async Task<SessionResponse> GetActiveSessionAsync()
        {
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync($"{baseUrl}/session/active");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Không thể kết nối đến máy chủ. Vui lòng kiểm tra URL hoặc server đang chạy.", ex);
            }
            try
            {
                var responseString = await response.Content.ReadAsStringAsync();

                var sessionResponse = JsonConvert.DeserializeObject<SessionResponse>(responseString);

                if (!sessionResponse.Success)
                    throw new HttpRequestException($"Error while getting SessionId in SessionService: {responseString}");
                else if(sessionResponse.Code == "NO_SESSIONS")
                {
                    Console.WriteLine($"No SessionId found, please start new session!");
                }
                Console.WriteLine($"Response get active sessionId: {responseString}");
                
                return sessionResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Unable to connect to the server or there's some error while activate sessionId", ex);
            }
        }
        public async Task<ApiResponse> ConnectToSessionAsync(string sessionId, string connectionId)
        {
            HttpResponseMessage response;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/session/connect/{sessionId}");
                request.Headers.Add("X-SignalR-Connection-Id", connectionId);
                response = await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Không thể kết nối đến máy chủ. Vui lòng kiểm tra URL hoặc server đang chạy.", ex);
            }
            try
            {
                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine("[Debug] Connecting response: ", responseString);
                var sessionResponse = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                if (!response.IsSuccessStatusCode)
                    Console.WriteLine($"Error connecting while getting SessionId : {responseString}");
                return sessionResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error connecting: ",ex.Message);
                throw new HttpRequestException(
                    "Unable to connect to the server or there's some error while connecting sessionId", ex);
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

        public async Task<ApiResponse> JoinToSessionAsync(string sessionId)
        {
            var token = TokenStorage.LoadToken();
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Chưa đăng nhập");

            if (_httpClient.DefaultRequestHeaders.Authorization == null)
                SetAuthToken(token);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync($"{baseUrl}/session/join/{sessionId}", null);
            }
            catch (Exception ex)
            {
                Console.Write(baseUrl);
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Không thể kết nối đến máy chủ. Vui lòng kiểm tra URL hoặc server đang chạy.", ex);
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);
            Console.WriteLine($"Results join: {responseString}");
            if (result is null)
            {
                throw new HttpRequestException($"Url not found while joining");
            }
            else if (!result.Success)
            {
                throw new HttpRequestException($"Error while joining session API: {responseString}");
            }
            return result;
        }
        public async Task JoinSessionAsync(string sessionId)
        {
            sessionId = sessionId.Trim();
            Console.WriteLine("jOINING ID: ", sessionId);
            var response = await JoinToSessionAsync(sessionId);
                            
            if (!response.Success)
            {
                throw new Exception(response.Message ?? "Failed to join session.");
            }
            await _signalRService.ConnectToHubAsync(sessionId);
            SessionStorage.SaveSession(sessionId);
        }
    }
}
