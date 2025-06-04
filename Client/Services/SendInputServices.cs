using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Client.Models;
using Client.Helpers;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Client.Services
{
    public class SendInputServices
    {
        private readonly HttpClient _httpClient;
        private readonly string baseUrl = AppSettings.BaseApiUri + "/api";

        public SendInputServices()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _httpClient = new HttpClient(handler);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            var token = TokenStorage.LoadToken();
            SetAuthToken(token);
        }

        public void SetAuthToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<ApiResponse> SendInputAsync(InputAction inputAction)
        {
            Console.WriteLine($"Attempting to send input: Action={inputAction.Action}, Button={inputAction.Button}");

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

            // Create input action based on type
            
            if (inputAction == null)
            {
                Console.WriteLine($"Failed to create input action for action: {inputAction.Action}");
                throw new ArgumentException($"Unsupported input type: {inputAction.Type}");
            }
            try
            {
                Console.WriteLine("Attempting to send input via HTTP");
                var payload = new
                {
                    sessionIdentifier = sessionId,
                    action = inputAction
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{baseUrl}/remote/send-input", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP request failed: {responseString}");
                    throw new HttpRequestException($"API call failed: {responseString}");
                }

                Console.WriteLine("Successfully sent input via HTTP");
                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} Both SignalR and HTTP methods failed to send input");
                throw new HttpRequestException("Failed to send input through both SignalR and HTTP", ex);
            }
        }
    }
}
