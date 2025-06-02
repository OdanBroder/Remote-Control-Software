using Newtonsoft.Json;

namespace Client.Models
{
    public class AuthResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("data")]
        public AuthData Data { get; set; }
    }

    public class AuthData
    {
        [JsonProperty("token")]
        public string Token { get; set; }
    }
}
