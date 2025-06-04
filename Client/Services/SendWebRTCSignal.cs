using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Client.Models;
using Client.Helpers;
namespace Client.Services
{
    public class WebRTCSignal
    {
        public string SessionIdentifier { get; set; }
        public string ConnectionId { get; set; }
        public string SignalType { get; set; }  // "offer", "answer", "ice-candidate"
        public object SignalData { get; set; }
    }

    public class SendWebRTCSignal
    {
        private readonly HttpClient _httpClient;
        private readonly string baseUrl = AppSettings.BaseApiUri + "/remotecontrolhub";

        public SendWebRTCSignal()
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

        public async Task<ApiResponse> SendSignalAsync(WebRTCSignal signal)
        {
            Console.WriteLine($"Attempting to send WebRTC signal: Type={signal.SignalType}");

            // Validate authentication
            var token = TokenStorage.LoadToken();
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("SendSignalAsync called without authentication token");
                throw new UnauthorizedAccessException("User is not authenticated");
            }

            // Get and validate session
            string sessionId = SessionStorage.LoadSession();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Console.WriteLine("SendSignalAsync called without active session");
                throw new InvalidOperationException("Session ID is required");
            }

            try
            {
                Console.WriteLine("Attempting to send WebRTC signal via HTTP");
                var content = new StringContent(
                    JsonConvert.SerializeObject(signal),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{baseUrl}/remote/webrtc/signal", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"HTTP request failed: {responseString}");
                    throw new HttpRequestException($"API call failed: {responseString}");
                }

                Console.WriteLine("Successfully sent WebRTC signal via HTTP");
                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                result.Code = "HTTP";
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "Failed to send WebRTC signal");
                throw new HttpRequestException("Failed to send WebRTC signal", ex);
            }
        }
    }
}
