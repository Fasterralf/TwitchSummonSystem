using System.Text.Json;
using System.Text;

namespace TwitchSummonSystem.Services
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        public async Task<string> GetAppAccessTokenAsync()
        {
            try
            {
                var clientId = _configuration["Twitch:ClientId"];
                var clientSecret = _configuration["Twitch:ClientSecret"];

                Console.WriteLine($"🔑 Verwende Client ID: {clientId}");
                Console.WriteLine($"🔐 Client Secret vorhanden: {!string.IsNullOrEmpty(clientSecret)}");

                // Als Form-Data senden (nicht JSON!)
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("client_id", clientId),
                    new("client_secret", clientSecret),
                    new("grant_type", "client_credentials")
                };

                var content = new FormUrlEncodedContent(formData);

                Console.WriteLine("📤 Sende Token Request...");

                var response = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📡 Token Response: {response.StatusCode}");
                Console.WriteLine($"📄 Response Body: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var accessToken = tokenResponse.GetProperty("access_token").GetString();

                    Console.WriteLine("✅ App Access Token erfolgreich erhalten");
                    Console.WriteLine($"🎫 Token: {accessToken[..10]}...");
                    return accessToken;
                }
                else
                {
                    Console.WriteLine($"❌ Token Fehler: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Token Service Fehler: {ex.Message}");
                return null;
            }
        }
    }
}
