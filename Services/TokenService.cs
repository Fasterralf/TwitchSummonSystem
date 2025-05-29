using System.Text.Json;
using System.Text;

namespace TwitchSummonSystem.Services
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private DateTime _lastTokenRequest = DateTime.MinValue;
        private readonly TimeSpan _minRequestInterval = TimeSpan.FromSeconds(1);

        private string? _cachedAppToken;
        private DateTime _appTokenExpiry = DateTime.MinValue;
        private string? _cachedUserToken;
        private DateTime _userTokenExpiry = DateTime.MinValue;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();

            _ = Task.Run(StartTokenMonitoringAsync);
        }

        public async Task<string> GetAppAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedAppToken) && DateTime.Now < _appTokenExpiry.AddMinutes(-5))
                return _cachedAppToken;

            await _rateLimitSemaphore.WaitAsync();
            try
            {
                var timeSinceLastRequest = DateTime.Now - _lastTokenRequest;
                if (timeSinceLastRequest < _minRequestInterval)
                {
                    await Task.Delay(_minRequestInterval - timeSinceLastRequest);
                }
                _lastTokenRequest = DateTime.Now;

                var clientId = _configuration["Twitch:ClientId"];
                var clientSecret = _configuration["Twitch:ClientSecret"];

                Console.WriteLine($"🔑 Verwende Client ID: {clientId}");
                Console.WriteLine($"🔐 Client Secret vorhanden: {!string.IsNullOrEmpty(clientSecret)}");

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("client_id", clientId!),
                    new("client_secret", clientSecret!),
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
                    var accessToken = tokenResponse.GetProperty("access_token").GetString()!;

                    Console.WriteLine("✅ App Access Token erfolgreich erhalten");
                    Console.WriteLine($"🎫 Token: {accessToken[..10]}...");
                    return accessToken;
                }
                else
                {
                    Console.WriteLine($"❌ Token Fehler: {response.StatusCode}");
                    return null!;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Token Service Fehler: {ex.Message}");
                return null!;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        public async Task<string> GetUserAccessTokenAsync()
        {
            // Prüfe Cache
            if (!string.IsNullOrEmpty(_cachedUserToken) && DateTime.Now < _userTokenExpiry.AddMinutes(-5))
            {
                return _cachedUserToken;
            }

            // Prüfe aktuellen Token
            var currentToken = _configuration["Twitch:AccessToken"];
            var isValid = await ValidateTokenAsync(currentToken!);

            if (isValid)
            {
                _cachedUserToken = currentToken;
                return currentToken!;
            }

            // Token abgelaufen - erneuern
            Console.WriteLine("🔄 User Token abgelaufen - erneuere mit Refresh Token...");
            return await RefreshUserTokenAsync();
        }

        private async Task<string> RefreshUserTokenAsync()
        {
            try
            {
                var clientId = _configuration["Twitch:ClientId"];
                var clientSecret = _configuration["Twitch:ClientSecret"];
                var refreshToken = _configuration["Twitch:RefreshToken"];

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

                Console.WriteLine($"🔄 Refresh Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var newAccessToken = tokenResponse.GetProperty("access_token").GetString()!;
                    var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString()!;
                    var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

                    _cachedUserToken = newAccessToken;
                    _userTokenExpiry = DateTime.Now.AddSeconds(expiresIn);

                    Console.WriteLine($"✅ User Token erneuert - gültig bis: {_userTokenExpiry:dd.MM.yyyy HH:mm}");

                    // Speichere neue Token in appsettings (für Ubuntu Server)
                    await UpdateConfigurationAsync(newAccessToken, newRefreshToken);

                    return newAccessToken;
                }
                else
                {
                    Console.WriteLine($"❌ Refresh Token Fehler: {response.StatusCode} - {responseContent}");
                    return null!;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Refresh Token Fehler: {ex.Message}");
                return null!;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var clientId = _configuration["Twitch:ClientId"];

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("Client-Id", clientId);

                var response = await client.GetAsync("https://id.twitch.tv/oauth2/validate");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var validation = JsonSerializer.Deserialize<JsonElement>(content);
                    var expiresIn = validation.GetProperty("expires_in").GetInt32();

                    _userTokenExpiry = DateTime.Now.AddSeconds(expiresIn);

                    Console.WriteLine($"✅ Token gültig für weitere {expiresIn} Sekunden ({TimeSpan.FromSeconds(expiresIn).TotalDays:F1} Tage)");
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ Token ungültig: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Token Validierung fehlgeschlagen: {ex.Message}");
                return false;
            }
        }

        private async Task StartTokenMonitoringAsync()
        {
            Console.WriteLine("🔍 Token-Überwachung gestartet");

            while (true)
            {
                try
                {
                    // Prüfe alle 6 Stunden
                    await Task.Delay(TimeSpan.FromHours(6));

                    Console.WriteLine("🔍 Prüfe Token-Status...");

                    // Prüfe User Token
                    var userToken = _configuration["Twitch:AccessToken"];
                    var isUserTokenValid = await ValidateTokenAsync(userToken!);

                    if (!isUserTokenValid)
                    {
                        Console.WriteLine("⚠️ User Token läuft bald ab - erneuere...");
                        await RefreshUserTokenAsync();
                    }

                    // App Token wird automatisch erneuert, keine Aktion nötig
                    Console.WriteLine("✅ Token-Check abgeschlossen");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Token-Monitoring Fehler: {ex.Message}");
                }
            }
        }

        private async Task UpdateConfigurationAsync(string newAccessToken, string newRefreshToken)
        {
            try
            {
                // Für Ubuntu Server: Schreibe in separate Token-Datei
                var tokenData = new
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    UpdatedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(tokenData, new JsonSerializerOptions { WriteIndented = true });
                var tokenFilePath = "/app/tokens.json"; // Pfad für Ubuntu Server

                await File.WriteAllTextAsync(tokenFilePath, json);
                Console.WriteLine($"💾 Token gespeichert in: {tokenFilePath}");

                // Update Configuration in Memory
                _configuration["Twitch:AccessToken"] = newAccessToken;
                _configuration["Twitch:RefreshToken"] = newRefreshToken;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Speichern der Token: {ex.Message}");
            }
        }

        public async Task<TokenStatus> GetTokenStatusAsync()
        {
            var userToken = _configuration["Twitch:AccessToken"];
            var isUserValid = await ValidateTokenAsync(userToken!);

            return new TokenStatus
            {
                UserTokenValid = isUserValid,
                UserTokenExpiry = _userTokenExpiry,
                AppTokenExpiry = _appTokenExpiry,
                LastCheck = DateTime.Now
            };
        }
    }

    public class TokenStatus
    {
        public bool UserTokenValid { get; set; }
        public DateTime UserTokenExpiry { get; set; }
        public DateTime AppTokenExpiry { get; set; }
        public DateTime LastCheck { get; set; }
    }
}
