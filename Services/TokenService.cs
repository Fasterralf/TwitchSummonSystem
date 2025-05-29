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
            // ✅ FIX: Prüfe Cache richtig
            if (!string.IsNullOrEmpty(_cachedAppToken) && DateTime.UtcNow < _appTokenExpiry.AddMinutes(-5))
                return _cachedAppToken;

            await _rateLimitSemaphore.WaitAsync();
            try
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastTokenRequest;
                if (timeSinceLastRequest < _minRequestInterval)
                {
                    await Task.Delay(_minRequestInterval - timeSinceLastRequest);
                }
                _lastTokenRequest = DateTime.UtcNow;

                var clientId = _configuration["Twitch:ClientId"];
                var clientSecret = _configuration["Twitch:ClientSecret"];

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("client_id", clientId!),
                    new("client_secret", clientSecret!),
                    new("grant_type", "client_credentials")
                };

                var content = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var accessToken = tokenResponse.GetProperty("access_token").GetString()!;
                    var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

                    // ✅ FIX: Cache richtig setzen
                    _cachedAppToken = accessToken;
                    _appTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

                    Console.WriteLine("✅ App Access Token erfolgreich erhalten");
                    Console.WriteLine($"🎫 Token: {accessToken[..10]}... (gültig bis: {_appTokenExpiry:dd.MM.yyyy HH:mm})");
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
            if (!string.IsNullOrEmpty(_cachedUserToken) && DateTime.UtcNow < _userTokenExpiry.AddMinutes(-5))
            {
                return _cachedUserToken;
            }

            // Lade Token aus Datei (Ubuntu Server)
            var currentToken = await LoadUserTokenFromFileAsync();
            if (string.IsNullOrEmpty(currentToken))
            {
                currentToken = _configuration["Twitch:AccessToken"];
            }

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

        // ✅ NEU: Token aus Datei laden
        private async Task<string?> LoadUserTokenFromFileAsync()
        {
            try
            {
                var tokenFilePath = "/app/tokens.json";
                if (File.Exists(tokenFilePath))
                {
                    var json = await File.ReadAllTextAsync(tokenFilePath);
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
                    return tokenData.GetProperty("AccessToken").GetString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fehler beim Laden der Token-Datei: {ex.Message}");
            }
            return null;
        }

        private async Task<string> RefreshUserTokenAsync()
        {
            try
            {
                var clientId = _configuration["Twitch:ClientId"];
                var clientSecret = _configuration["Twitch:ClientSecret"];

                // Lade Refresh Token aus Datei oder Config
                var refreshToken = await LoadRefreshTokenFromFileAsync() ?? _configuration["Twitch:RefreshToken"];

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
                    _userTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

                    Console.WriteLine($"✅ User Token erneuert - gültig bis: {_userTokenExpiry:dd.MM.yyyy HH:mm}");

                    // Speichere neue Token
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

        // ✅ NEU: Refresh Token aus Datei laden
        private async Task<string?> LoadRefreshTokenFromFileAsync()
        {
            try
            {
                var tokenFilePath = "/app/tokens.json";
                if (File.Exists(tokenFilePath))
                {
                    var json = await File.ReadAllTextAsync(tokenFilePath);
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
                    return tokenData.GetProperty("RefreshToken").GetString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fehler beim Laden des Refresh Tokens: {ex.Message}");
            }
            return null;
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

                    _userTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
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

            // ✅ FIX: Initialisiere App Token beim Start
            try
            {
                Console.WriteLine("🔄 Initialisiere App Token beim Start...");
                await GetAppAccessTokenAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fehler bei App Token Initialisierung: {ex.Message}");
            }

            while (true)
            {
                try
                {
                    // ✅ Prüfe alle 6 Stunden
                    await Task.Delay(TimeSpan.FromHours(6));
                    Console.WriteLine("🔍 Automatische Token-Prüfung...");

                    // Prüfe User Token
                    var userToken = await LoadUserTokenFromFileAsync() ?? _configuration["Twitch:AccessToken"];
                    var isUserTokenValid = await ValidateTokenAsync(userToken!);

                    if (!isUserTokenValid || (_userTokenExpiry - DateTime.UtcNow).TotalDays < 7)
                    {
                        Console.WriteLine("⚠️ User Token läuft bald ab - erneuere...");
                        await RefreshUserTokenAsync();
                    }

                    // ✅ FIX: Prüfe auch App Token
                    if ((_appTokenExpiry - DateTime.UtcNow).TotalHours < 12)
                    {
                        Console.WriteLine("⚠️ App Token läuft bald ab - erneuere...");
                        _cachedAppToken = null; // Cache leeren
                        await GetAppAccessTokenAsync();
                    }

                    Console.WriteLine("✅ Automatische Token-Prüfung abgeschlossen");
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
                var tokenData = new
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    UpdatedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(tokenData, new JsonSerializerOptions { WriteIndented = true });
                var tokenFilePath = "/app/tokens.json";
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

        // ✅ FIX: Verbesserte Token Status API
        public async Task<object> GetTokenStatusAsync()
        {
            try
            {
                // Stelle sicher, dass App Token initialisiert ist
                if (_appTokenExpiry == DateTime.MinValue || string.IsNullOrEmpty(_cachedAppToken))
                {
                    Console.WriteLine("🔄 App Token nicht initialisiert - hole neuen Token...");
                    await GetAppAccessTokenAsync();
                }

                var userToken = await LoadUserTokenFromFileAsync() ?? _configuration["Twitch:AccessToken"];
                var userValid = false;
                var userDaysUntilExpiry = 0.0;

                if (!string.IsNullOrEmpty(userToken))
                {
                    userValid = await ValidateTokenAsync(userToken);
                    userDaysUntilExpiry = (_userTokenExpiry - DateTime.UtcNow).TotalDays;
                }

                var appDaysUntilExpiry = (_appTokenExpiry - DateTime.UtcNow).TotalDays;

                return new
                {
                    userToken = new
                    {
                        valid = userValid,
                        expiresAt = _userTokenExpiry,
                        daysUntilExpiry = Math.Max(0, userDaysUntilExpiry) // Negative Werte vermeiden
                    },
                    appToken = new
                    {
                        expiresAt = _appTokenExpiry,
                        daysUntilExpiry = Math.Max(0, appDaysUntilExpiry) // Negative Werte vermeiden
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Abrufen des Token Status: {ex.Message}");
                return new
                {
                    userToken = new
                    {
                        valid = false,
                        expiresAt = DateTime.MinValue,
                        daysUntilExpiry = 0.0
                    },
                    appToken = new
                    {
                        expiresAt = DateTime.MinValue,
                        daysUntilExpiry = 0.0
                    }
                };
            }
        }

        // ✅ NEU: Manueller Token Refresh für Admin Panel
        public async Task<bool> ForceRefreshTokensAsync()
        {
            try
            {
                Console.WriteLine("🔄 Manueller Token-Refresh gestartet...");

                // User Token erneuern
                var newUserToken = await RefreshUserTokenAsync();

                // App Token erneuern
                _cachedAppToken = null; // Cache leeren
                var newAppToken = await GetAppAccessTokenAsync();

                return !string.IsNullOrEmpty(newUserToken) && !string.IsNullOrEmpty(newAppToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Manueller Token-Refresh Fehler: {ex.Message}");
                return false;
            }
        }
    }
}
