using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Client.Helpers;
using Client.Models;
using Newtonsoft.Json;

namespace Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string baseUrl = AppSettings.BaseApiUri + "/api";

        public AuthService()
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
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync($"{baseUrl}/auth/login", content);
            }
            catch (Exception ex)
            {
                Console.Write(baseUrl);
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Không thể kết nối đến máy chủ. Vui lòng kiểm tra URL hoặc server đang chạy.", ex);
            }

            var responseString = await response.Content.ReadAsStringAsync();
                        
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    dynamic err = JsonConvert.DeserializeObject(responseString);
                    string msg = err?.message ?? $"Lỗi {response.StatusCode}";
                    throw new HttpRequestException(msg);
                }
                catch
                {
                    throw new HttpRequestException(
                        $"({response.StatusCode}): {responseString}");
                }
            }

            var loginResponse = JsonConvert.DeserializeObject<AuthResponse>(responseString);
            return loginResponse;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync($"{baseUrl}/auth/register", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine(baseUrl);
                Console.WriteLine(ex.Message);
                throw new HttpRequestException(
                    "Không thể kết nối đến máy chủ. Vui lòng kiểm tra URL hoặc server đang chạy.", ex);
            }

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    dynamic err = JsonConvert.DeserializeObject(responseString);
                    string msg = err?.message ?? $"Lỗi {response.StatusCode}";
                    throw new HttpRequestException(msg);
                }
                catch
                {
                    throw new HttpRequestException(
                        $"({response.StatusCode}): {responseString}");
                }
            }

            return JsonConvert.DeserializeObject<AuthResponse>(responseString);
        }
        
        public void SetAuthToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
