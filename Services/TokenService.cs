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

        // Logging Helper
        private static void LogInfo(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ℹ️ {message}");
        private static void LogSuccess(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ {message}");
        private static void LogWarning(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ {message}");
        private static void LogError(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ {message}");
        private static void LogDebug(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔍 {message}");

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
            LogInfo("TokenService initialisiert");
            _ = Task.Run(StartTokenMonitoringAsync);
        }

        public async Task<string> GetAppAccessTokenAsync()
        {
            // Prüfe Cache - App Token ist ~60 Tage gültig, erneuere 1 Tag vorher
            if (!string.IsNullOrEmpty(_cachedAppToken) && DateTime.UtcNow < _appTokenExpiry.AddDays(-1))
            {
                var remainingTime = _appTokenExpiry - DateTime.UtcNow;
                LogDebug($"App Token aus Cache verwendet (gültig für weitere {remainingTime.TotalDays:F1} Tage)");
                return _cachedAppToken;
            }

            await _rateLimitSemaphore.WaitAsync();
            try
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastTokenRequest;
                if (timeSinceLastRequest < _minRequestInterval)
                {
                    var delay = _minRequestInterval - timeSinceLastRequest;
                    LogDebug($"Rate Limit: Warte {delay.TotalMilliseconds}ms");
                    await Task.Delay(delay);
                }
                _lastTokenRequest = DateTime.UtcNow;

                LogInfo("Hole neuen App Access Token...");

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

                    _cachedAppToken = accessToken;
                    _appTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

                    var expiryDays = TimeSpan.FromSeconds(expiresIn).TotalDays;
                    LogSuccess($"App Token erhalten - gültig für {expiryDays:F1} Tage bis {_appTokenExpiry:dd.MM.yyyy HH:mm}");
                    LogDebug($"Token Preview: {accessToken[..Math.Min(10, accessToken.Length)]}...");

                    return accessToken;
                }
                else
                {
                    LogError($"App Token Request fehlgeschlagen: {response.StatusCode} - {responseContent}");
                    return null!;
                }
            }
            catch (Exception ex)
            {
                LogError($"App Token Service Fehler: {ex.Message}");
                return null!;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        public async Task<string> GetUserAccessTokenAsync()
        {
            // User Token ist ~4 Stunden gültig, erneuere 30 Minuten vorher
            if (!string.IsNullOrEmpty(_cachedUserToken) && DateTime.UtcNow < _userTokenExpiry.AddMinutes(-30))
            {
                var remainingTime = _userTokenExpiry - DateTime.UtcNow;
                LogDebug($"User Token aus Cache verwendet (gültig für weitere {remainingTime.TotalHours:F1}h)");
                return _cachedUserToken;
            }

            // Lade Token aus Datei
            var currentToken = await LoadUserTokenFromFileAsync();
            if (string.IsNullOrEmpty(currentToken))
            {
                LogWarning("Kein Token in Datei gefunden - verwende Config");
                currentToken = _configuration["Twitch:AccessToken"];
            }

            var isValid = await ValidateTokenAsync(currentToken!);
            if (isValid && _userTokenExpiry > DateTime.UtcNow.AddMinutes(30))
            {
                _cachedUserToken = currentToken;
                var remainingTime = _userTokenExpiry - DateTime.UtcNow;
                LogSuccess($"User Token validiert - gültig für weitere {remainingTime.TotalHours:F1}h");
                return currentToken!;
            }

            // Token abgelaufen oder läuft bald ab
            LogWarning($"User Token {(isValid ? "läuft bald ab" : "ist ungültig")} - erneuere mit Refresh Token");
            return await RefreshUserTokenAsync();
        }

        private async Task<string?> LoadUserTokenFromFileAsync()
        {
            try
            {
                var tokenFilePath = "/app/tokens.json";
                if (File.Exists(tokenFilePath))
                {
                    var json = await File.ReadAllTextAsync(tokenFilePath);
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

                    if (tokenData.TryGetProperty("AccessToken", out var tokenProp))
                    {
                        var accessToken = tokenProp.GetString();
                        if (!string.IsNullOrWhiteSpace(accessToken))
                        {
                            LogDebug("User Token aus Datei geladen");
                            return accessToken;
                        }
                    }

                    LogWarning("Token-Datei existiert, aber AccessToken ist leer");
                }
                else
                {
                    LogDebug("Token-Datei existiert nicht");
                }
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Laden der Token-Datei: {ex.Message}");
            }
            return null;
        }

        private async Task<string> RefreshUserTokenAsync()
        {
            try
            {
                LogInfo("Starte User Token Refresh...");

                var clientId = _configuration["Twitch:ClientId"];
                var clientSecret = _configuration["Twitch:ClientSecret"];
                var refreshToken = await LoadRefreshTokenFromFileAsync() ?? _configuration["Twitch:RefreshToken"];

                if (string.IsNullOrEmpty(refreshToken))
                {
                    LogError("Kein Refresh Token verfügbar!");
                    return null!;
                }

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

                LogDebug($"Refresh Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var newAccessToken = tokenResponse.GetProperty("access_token").GetString()!;
                    var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString()!;
                    var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

                    _cachedUserToken = newAccessToken;
                    _userTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

                    var expiryHours = TimeSpan.FromSeconds(expiresIn).TotalHours;
                    LogSuccess($"User Token erneuert - gültig für {expiryHours:F1}h bis {_userTokenExpiry:dd.MM.yyyy HH:mm}");

                    await UpdateConfigurationAsync(newAccessToken, newRefreshToken);
                    return newAccessToken;
                }
                else
                {
                    LogError($"Token Refresh fehlgeschlagen: {response.StatusCode}");
                    LogError($"Response: {responseContent}");
                    return null!;
                }
            }
            catch (Exception ex)
            {
                LogError($"Refresh Token Fehler: {ex.Message}");
                return null!;
            }
        }

        private async Task<string?> LoadRefreshTokenFromFileAsync()
        {
            try
            {
                var tokenFilePath = "/app/tokens.json";
                if (File.Exists(tokenFilePath))
                {
                    var json = await File.ReadAllTextAsync(tokenFilePath);
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

                    if (tokenData.TryGetProperty("RefreshToken", out var tokenProp))
                    {
                        var refreshToken = tokenProp.GetString();
                        if (!string.IsNullOrWhiteSpace(refreshToken))
                        {
                            LogDebug("Refresh Token aus Datei geladen");
                            return refreshToken;
                        }
                    }

                    LogWarning("Token-Datei existiert, aber RefreshToken ist leer");
                }
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Laden des Refresh Tokens: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    LogWarning("Token ist leer oder null");
                    _userTokenExpiry = DateTime.MinValue;
                    return false;
                }

                LogDebug("Validiere User Token...");

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

                    var remainingHours = TimeSpan.FromSeconds(expiresIn).TotalHours;
                    LogSuccess($"Token gültig für weitere {remainingHours:F1}h (bis {_userTokenExpiry:dd.MM.yyyy HH:mm})");

                    return true;
                }
                else
                {
                    LogWarning($"Token Validierung fehlgeschlagen: {response.StatusCode}");
                    _userTokenExpiry = DateTime.MinValue;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Token Validierung Fehler: {ex.Message}");
                _userTokenExpiry = DateTime.MinValue;
                return false;
            }
        }

        private async Task StartTokenMonitoringAsync()
        {
            LogInfo("Token-Überwachung gestartet");

            // Initialisiere App Token beim Start
            try
            {
                LogInfo("Initialisiere App Token beim Start...");
                await GetAppAccessTokenAsync();
            }
            catch (Exception ex)
            {
                LogError($"App Token Initialisierung fehlgeschlagen: {ex.Message}");
            }

            while (true)
            {
                try
                {
                    // Prüfe alle 45 Minuten (da User Token nur ~4h gültig ist)
                    await Task.Delay(TimeSpan.FromMinutes(45));

                    LogInfo("=== Automatische Token-Prüfung ===");

                    // Prüfe User Token - erneuere wenn weniger als 1h verbleibt
                    if (_userTokenExpiry != DateTime.MinValue)
                    {
                        var userTimeRemaining = _userTokenExpiry - DateTime.UtcNow;
                        LogDebug($"User Token läuft ab in: {userTimeRemaining.TotalHours:F1}h");

                        if (userTimeRemaining.TotalHours < 1)
                        {
                            LogWarning("User Token läuft in weniger als 1h ab - erneuere jetzt");
                            await RefreshUserTokenAsync();
                        }
                    }
                    else
                    {
                        LogWarning("User Token Status unbekannt - validiere");
                        var userToken = await LoadUserTokenFromFileAsync() ?? _configuration["Twitch:AccessToken"];
                        await ValidateTokenAsync(userToken!);
                    }

                    // Prüfe App Token - erneuere wenn weniger als 7 Tage verbleiben
                    if (_appTokenExpiry != DateTime.MinValue)
                    {
                        var appTimeRemaining = _appTokenExpiry - DateTime.UtcNow;
                        LogDebug($"App Token läuft ab in: {appTimeRemaining.TotalDays:F1} Tagen");

                        if (appTimeRemaining.TotalDays < 7)
                        {
                            LogWarning("App Token läuft in weniger als 7 Tagen ab - erneuere jetzt");
                            _cachedAppToken = null; // Cache leeren
                            await GetAppAccessTokenAsync();
                        }
                    }
                    else
                    {
                        LogWarning("App Token Status unbekannt - hole neuen Token");
                        await GetAppAccessTokenAsync();
                    }

                    LogSuccess("Automatische Token-Prüfung abgeschlossen");
                }
                catch (Exception ex)
                {
                    LogError($"Token-Monitoring Fehler: {ex.Message}");
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
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = _userTokenExpiry
                };

                var json = JsonSerializer.Serialize(tokenData, new JsonSerializerOptions { WriteIndented = true });
                var tokenFilePath = "/app/tokens.json";

                await File.WriteAllTextAsync(tokenFilePath, json);
                LogSuccess($"Token gespeichert: {tokenFilePath}");
                LogDebug($"Token gültig bis: {_userTokenExpiry:dd.MM.yyyy HH:mm}");

                // Update Configuration in Memory
                _configuration["Twitch:AccessToken"] = newAccessToken;
                _configuration["Twitch:RefreshToken"] = newRefreshToken;
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Speichern der Token: {ex.Message}");
            }
        }

        public async Task<object> GetTokenStatusAsync()
        {
            try
            {
                LogDebug("Erstelle Token Status Report...");

                // Stelle sicher, dass App Token initialisiert ist
                if (_appTokenExpiry == DateTime.MinValue || string.IsNullOrEmpty(_cachedAppToken))
                {
                    LogInfo("App Token nicht initialisiert - hole neuen Token");
                    await GetAppAccessTokenAsync();
                }

                var userToken = await LoadUserTokenFromFileAsync() ?? _configuration["Twitch:AccessToken"];
                var userValid = false;
                var userExpiresAt = DateTime.MinValue;
                var userHoursUntilExpiry = 0.0;

                if (!string.IsNullOrEmpty(userToken))
                {
                    userValid = await ValidateTokenAsync(userToken);
                    if (userValid && _userTokenExpiry > DateTime.MinValue)
                    {
                        userExpiresAt = _userTokenExpiry;
                        userHoursUntilExpiry = Math.Max(0, (_userTokenExpiry - DateTime.UtcNow).TotalHours);
                    }
                }

                var appDaysUntilExpiry = Math.Max(0, (_appTokenExpiry - DateTime.UtcNow).TotalDays);
                var appHoursUntilExpiry = Math.Max(0, (_appTokenExpiry - DateTime.UtcNow).TotalHours);

                var status = new
                {
                    userToken = new
                    {
                        valid = userValid,
                        expiresAt = userExpiresAt,
                        hoursUntilExpiry = userHoursUntilExpiry,
                        needsRefresh = userHoursUntilExpiry < 1,
                        status = userValid ?
                            (userHoursUntilExpiry < 1 ? "EXPIRES_SOON" : "VALID") :
                            "INVALID"
                    },
                    appToken = new
                    {
                        valid = !string.IsNullOrEmpty(_cachedAppToken) && _appTokenExpiry > DateTime.UtcNow,
                        expiresAt = _appTokenExpiry,
                        daysUntilExpiry = appDaysUntilExpiry,
                        hoursUntilExpiry = appHoursUntilExpiry,
                        needsRefresh = appDaysUntilExpiry < 7,
                        status = appDaysUntilExpiry < 7 ? "EXPIRES_SOON" : "VALID"
                    },
                    lastCheck = DateTime.UtcNow
                };

                LogDebug($"Token Status: User={status.userToken.status} ({userHoursUntilExpiry:F1}h), App={status.appToken.status} ({appDaysUntilExpiry:F1}d)");

                return status;
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Abrufen des Token Status: {ex.Message}");
                return new
                {
                    userToken = new
                    {
                        valid = false,
                        expiresAt = DateTime.MinValue,
                        hoursUntilExpiry = 0.0,
                        needsRefresh = true,
                        status = "ERROR"
                    },
                    appToken = new
                    {
                        valid = false,
                        expiresAt = DateTime.MinValue,
                        daysUntilExpiry = 0.0,
                        hoursUntilExpiry = 0.0,
                        needsRefresh = true,
                        status = "ERROR"
                    },
                    lastCheck = DateTime.UtcNow,
                    error = ex.Message
                };
            }
        }

        public async Task<bool> ForceRefreshTokensAsync()
        {
            try
            {
                LogInfo("=== Manueller Token-Refresh gestartet ===");

                // User Token erneuern
                LogInfo("Erneuere User Token...");
                var newUserToken = await RefreshUserTokenAsync();
                var userSuccess = !string.IsNullOrEmpty(newUserToken);

                // App Token erneuern
                LogInfo("Erneuere App Token...");
                _cachedAppToken = null; // Cache leeren
                var newAppToken = await GetAppAccessTokenAsync();
                var appSuccess = !string.IsNullOrEmpty(newAppToken);

                var success = userSuccess && appSuccess;

                if (success)
                {
                    LogSuccess("Alle Token erfolgreich erneuert");
                }
                else
                {
                    LogWarning($"Token-Refresh teilweise fehlgeschlagen: User={userSuccess}, App={appSuccess}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogError($"Manueller Token-Refresh Fehler: {ex.Message}");
                return false;
            }
        }

        // Neue Methode für detaillierte Token-Informationen
        public async Task<string> GetDetailedTokenInfoAsync()
        {
            try
            {
                var status = await GetTokenStatusAsync();
                var statusObj = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(status));

                var userToken = statusObj.GetProperty("userToken");
                var appToken = statusObj.GetProperty("appToken");

                var info = new StringBuilder();
                info.AppendLine("=== TOKEN STATUS REPORT ===");
                info.AppendLine($"Zeitpunkt: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                info.AppendLine();

                info.AppendLine("🔑 USER TOKEN:");
                info.AppendLine($"   Status: {userToken.GetProperty("status").GetString()}");
                info.AppendLine($"   Gültig: {userToken.GetProperty("valid").GetBoolean()}");
                if (userToken.GetProperty("expiresAt").GetDateTime() != DateTime.MinValue)
                {
                    info.AppendLine($"   Läuft ab: {userToken.GetProperty("expiresAt").GetDateTime():dd.MM.yyyy HH:mm}");
                    info.AppendLine($"   Verbleibend: {userToken.GetProperty("hoursUntilExpiry").GetDouble():F1} Stunden");
                }
                info.AppendLine($"   Refresh nötig: {userToken.GetProperty("needsRefresh").GetBoolean()}");
                info.AppendLine();

                info.AppendLine("🔑 APP TOKEN:");
                info.AppendLine($"   Status: {appToken.GetProperty("status").GetString()}");
                info.AppendLine($"   Gültig: {appToken.GetProperty("valid").GetBoolean()}");
                if (appToken.GetProperty("expiresAt").GetDateTime() != DateTime.MinValue)
                {
                    info.AppendLine($"   Läuft ab: {appToken.GetProperty("expiresAt").GetDateTime():dd.MM.yyyy HH:mm}");
                    info.AppendLine($"   Verbleibend: {appToken.GetProperty("daysUntilExpiry").GetDouble():F1} Tage");
                }
                info.AppendLine($"   Refresh nötig: {appToken.GetProperty("needsRefresh").GetBoolean()}");

                return info.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Fehler beim Erstellen des Token Reports: {ex.Message}";
            }
        }

        // Cleanup beim Dispose
        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimitSemaphore?.Dispose();
            LogInfo("TokenService disposed");
        }
    }
}

