using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Client.Helpers;
using Client.Models;
using Newtonsoft.Json;

namespace Client.Services
{
    public class SessionService
    {
        private readonly HttpClient _httpClient;
        private readonly string baseUrl = AppSettings.BaseApiUri + "/api";

        public SessionService()
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

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Lỗi khi gọi API: {responseString}");

            Console.WriteLine($"Response: {responseString}");
            var sessionResponse = JsonConvert.DeserializeObject<StartSessionResponse>(responseString);

            return sessionResponse;
        }

        public async Task<SessionResponse> LeaveSessionAsync(string sessionId)
        {
            HttpResponseMessage response;

            try
            {
                response = await _httpClient.PostAsync($"{baseUrl}/session/stop/{sessionId}", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Không thể kết nối đến máy chủ. Vui lòng kiểm tra URL hoặc server đang chạy.", ex);
            }

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Lỗi khi gọi API: {responseString}");

            Console.WriteLine($"Response: {responseString}");
            var sessionResponse = JsonConvert.DeserializeObject<SessionResponse>(responseString);

            return sessionResponse;
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

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Lỗi khi gọi API: {responseString}");

            Console.WriteLine($"Response: {responseString}");
            var sessionResponse = JsonConvert.DeserializeObject<SessionResponse>(responseString);

            return sessionResponse;
        }
    }
}
