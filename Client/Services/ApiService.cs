using System;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Client.Models;
using System.Text;

namespace Client.Services
{
    public class ApiService
    {
        private static ApiService _instance;
        public static ApiService Instance => _instance ??= new ApiService();

        private readonly HttpClient _http;

        private ApiService()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5000/api/") // Replace with your API base URL
            };
        }

        public async Task<AuthResponse?> LoginAsync(string username, string password)
        {
            var request = new LoginRequest { Username = username, Password = password };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsJsonAsync("auth/login", content);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<AuthResponse>(responseContent);
                return result;
                // return await response.Content.ReadFromJsonAsync<AuthResponse>(); //.Net 5+
            }

            return null;
        }
    }
}
