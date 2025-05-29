using System.Text.Json;

namespace TwitchSummonSystem.Services
{
    public class ChatTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private string? _cachedChatToken;
        private DateTime _chatTokenExpiry = DateTime.MinValue;

        public ChatTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        public async Task<string> GetChatTokenAsync()
        {
            // Prüfe Cache
            if (!string.IsNullOrEmpty(_cachedChatToken) && DateTime.Now < _chatTokenExpiry.AddMinutes(-5))
            {
                return _cachedChatToken;
            }

            // Prüfe aktuellen Token
            var currentToken = _configuration["Twitch:ChatOAuthToken"]?.Replace("oauth:", "");
            var isValid = await ValidateChatTokenAsync(currentToken!);

            if (isValid)
            {
                _cachedChatToken = $"oauth:{currentToken}";
                return _cachedChatToken;
            }

            // Token abgelaufen - erneuern
            Console.WriteLine("🔄 Chat Token abgelaufen - erneuere...");
            return await RefreshChatTokenAsync();
        }

        private async Task<string> RefreshChatTokenAsync()
        {
            try
            {
                var clientId = _configuration["Twitch:BotClientId"];
                var clientSecret = _configuration["Twitch:BotClientSecret"];
                var refreshToken = _configuration["Twitch:ChatRefreshToken"];

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("client_id", clientId!),
                    new("client_secret", clientSecret!),
                    new("grant_type", "refresh_token"),
                    new("refresh_token", refreshToken!)
                };

                var content = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"🔄 Chat Token Refresh Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var newAccessToken = tokenResponse.GetProperty("access_token").GetString()!;
                    var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString()!;
                    var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

                    _cachedChatToken = $"oauth:{newAccessToken}";
                    _chatTokenExpiry = DateTime.Now.AddSeconds(expiresIn);

                    Console.WriteLine($"✅ Chat Token erneuert - gültig bis: {_chatTokenExpiry:dd.MM.yyyy HH:mm}");

                    // Speichere neue Token
                    await UpdateChatTokenConfigAsync(newAccessToken, newRefreshToken);

                    return _cachedChatToken;
                }
                else
                {
                    Console.WriteLine($"❌ Chat Token Refresh Fehler: {response.StatusCode} - {responseContent}");
                    return _configuration["Twitch:ChatOAuthToken"]!; // Fallback
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Chat Token Refresh Fehler: {ex.Message}");
                return _configuration["Twitch:ChatOAuthToken"]!; // Fallback
            }
        }

        private async Task<bool> ValidateChatTokenAsync(string token)
        {
            try
            {
                var clientId = _configuration["Twitch:BotClientId"];

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("Client-Id", clientId);

                var response = await client.GetAsync("https://id.twitch.tv/oauth2/validate");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var validation = JsonSerializer.Deserialize<JsonElement>(content);
                    var expiresIn = validation.GetProperty("expires_in").GetInt32();

                    _chatTokenExpiry = DateTime.Now.AddSeconds(expiresIn);

                    Console.WriteLine($"✅ Chat Token gültig für weitere {expiresIn} Sekunden ({TimeSpan.FromSeconds(expiresIn).TotalDays:F1} Tage)");
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ Chat Token ungültig: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Chat Token Validierung fehlgeschlagen: {ex.Message}");
                return false;
            }
        }

        private async Task UpdateChatTokenConfigAsync(string newAccessToken, string newRefreshToken)
        {
            try
            {
                var chatTokenData = new
                {
                    ChatOAuthToken = $"oauth:{newAccessToken}",
                    ChatRefreshToken = newRefreshToken,
                    UpdatedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(chatTokenData, new JsonSerializerOptions { WriteIndented = true });
                var chatTokenFilePath = "/app/chat-tokens.json"; // Ubuntu Server Pfad

                await File.WriteAllTextAsync(chatTokenFilePath, json);
                Console.WriteLine($"💾 Chat Token gespeichert in: {chatTokenFilePath}");

                // Update Configuration in Memory
                _configuration["Twitch:ChatOAuthToken"] = $"oauth:{newAccessToken}";
                _configuration["Twitch:ChatRefreshToken"] = newRefreshToken;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Speichern der Chat Token: {ex.Message}");
            }
        }
    }
}
