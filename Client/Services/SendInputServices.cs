using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Client.Models;
using Client.Helpers;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;

namespace Client.Services
{
    public class SendInputServices : IDisposable
    {
        private readonly SignalRService _signalRService;
        private readonly HttpClient _httpClient;
        private readonly string baseUrl = AppSettings.BaseApiUri + "/api";
        private readonly string _baseUrl;
        private bool _isDisposed;

        public SendInputServices(SignalRService signalRService)
        {
            _signalRService = signalRService;
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
        }

        public async Task<ApiResponse> SendInputAsync(InputAction inputAction)
        {
            // Console.WriteLine($"Attempting to send input: Action={inputAction.Action}, Button={inputAction.Button}");

            // Validate authentication
            var token = TokenStorage.LoadToken();
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("SendInputAsync called without authentication token");
                throw new UnauthorizedAccessException("User is not authenticated");
            }

            // Get and validate session
            string sessionId = SessionStorage.LoadSession();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Console.WriteLine("SendInputAsync called without active session");
                throw new InvalidOperationException("Session ID is required");
            }

            if (inputAction == null)
            {
                Console.WriteLine($"Failed to create input action for action: {inputAction?.Action}");
                throw new ArgumentException($"Unsupported input type: {inputAction.Type}");
            }

            try
            {
                // Console.WriteLine("Attempting to send input via SignalR");
                var serializedAction = JsonConvert.SerializeObject(inputAction);
                await _signalRService.SendInputActionAsync(sessionId, serializedAction);
                
                // Console.WriteLine("Successfully sent input via SignalR");
                return new ApiResponse { Success = true, Message = "Input sent successfully via SignalR" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR failed: {ex.Message}. Attempting HTTP fallback...");
                
                try
                {
                    // Prepare HTTP request
                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/remote-control/send-input");
                    httpRequest.Headers.Add("Authorization", $"Bearer {token}");
                    
                    var content = new StringContent(
                        JsonConvert.SerializeObject(new { SessionId = sessionId, InputAction = inputAction }),
                        Encoding.UTF8,
                        "application/json"
                    );
                    httpRequest.Content = content;

                    // Send HTTP request
                    var response = await _httpClient.SendAsync(httpRequest);
                    response.EnsureSuccessStatusCode();

                    Console.WriteLine("Successfully sent input via HTTP fallback");
                    return new ApiResponse { Success = true, Message = "Input sent successfully via HTTP fallback" };
                }
                catch (Exception httpEx)
                {
                    Console.WriteLine($"HTTP fallback also failed: {httpEx.Message}");
                    throw new Exception("Failed to send input through both SignalR and HTTP", httpEx);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        _httpClient?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during SendInputServices disposal: {ex.Message}");
                    }
                }
                _isDisposed = true;
            }
        }

        ~SendInputServices()
        {
            Dispose(false);
        }
    }
}
