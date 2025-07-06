using System.Text.Json;
using System.Text;

namespace TwitchSummonSystem.Services
{
    public class ChatTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly DiscordService _discordService;
        private string? _cachedChatToken;
        private DateTime _chatTokenExpiry = DateTime.MinValue;
        private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);

        // Logging Helper
        private static void LogInfo(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? [CHAT] {message}");
        private static void LogSuccess(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? [CHAT] {message}");
        private static void LogWarning(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? [CHAT] {message}");
        private static void LogError(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? [CHAT] {message}");
        private static void LogDebug(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? [CHAT] {message}");
        private DateTime _lastChatStatusLogTime = DateTime.MinValue;
        private readonly TimeSpan _chatStatusLogInterval = TimeSpan.FromHours(1);

        public ChatTokenService(IConfiguration configuration, DiscordService discordService)
        {
            _configuration = configuration;
            _discordService = discordService;
            _httpClient = new HttpClient();
            LogInfo("ChatTokenService initialisiert");
            _ = Task.Run(StartChatTokenMonitoringAsync);
        }

        public async Task<string> GetChatTokenAsync()
        {
            
            if (!string.IsNullOrEmpty(_cachedChatToken) && DateTime.UtcNow < _chatTokenExpiry.AddMinutes(-30))
            {
                var remainingTime = _chatTokenExpiry - DateTime.UtcNow;
                LogDebug($"Chat Token aus Cache verwendet (g�ltig f�r weitere {remainingTime.TotalHours:F1}h)");
                return _cachedChatToken;
            }

            // Lade aktuellen Token aus Config
            var currentToken = _configuration["Twitch:ChatOAuthToken"]?.Replace("oauth:", "");

            if (string.IsNullOrWhiteSpace(currentToken))
            {
                LogError("Kein Chat Token in Konfiguration gefunden!");
                return string.Empty;
            }

            var isValid = await ValidateChatTokenAsync(currentToken);
            if (isValid && _chatTokenExpiry > DateTime.UtcNow.AddMinutes(30))
            {
                _cachedChatToken = $"oauth:{currentToken}";
                var remainingTime = _chatTokenExpiry - DateTime.UtcNow;
                LogSuccess($"Chat Token validiert - g�ltig f�r weitere {remainingTime.TotalHours:F1}h");
                return _cachedChatToken;
            }

            // Token abgelaufen oder l�uft bald ab
            LogWarning($"Chat Token {(isValid ? "l�uft bald ab" : "ist ung�ltig")} - erneuere mit Refresh Token");
            return await RefreshChatTokenAsync();
        }

        private async Task<string> RefreshChatTokenAsync()
        {
            await _rateLimitSemaphore.WaitAsync();
            try
            {
                LogInfo("Starte Chat Token Refresh...");

                var clientId = _configuration["Twitch:BotClientId"];
                var clientSecret = _configuration["Twitch:BotClientSecret"];
                var refreshToken = await LoadChatRefreshTokenFromFileAsync() ?? _configuration["Twitch:ChatRefreshToken"];

                if (string.IsNullOrEmpty(refreshToken))
                {
                    LogError("Kein Chat Refresh Token verf�gbar!");
                    return _configuration["Twitch:ChatOAuthToken"] ?? string.Empty;
                }

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    LogError("Bot ClientId oder ClientSecret fehlt in Konfiguration!");
                    return _configuration["Twitch:ChatOAuthToken"] ?? string.Empty;
                }

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("client_id", clientId),
                    new("client_secret", clientSecret),
                    new("grant_type", "refresh_token"),
                    new("refresh_token", refreshToken)
                };

                var content = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                LogDebug($"Chat Token Refresh Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var newAccessToken = tokenResponse.GetProperty("access_token").GetString()!;
                    var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString()!;
                    var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

                    _cachedChatToken = $"oauth:{newAccessToken}";
                    _chatTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

                    var expiryHours = TimeSpan.FromSeconds(expiresIn).TotalHours;
                    LogSuccess($"Chat Token erneuert - g�ltig f�r {expiryHours:F1}h bis {_chatTokenExpiry:dd.MM.yyyy HH:mm}");

                    await UpdateChatTokenConfigAsync(newAccessToken, newRefreshToken);
                    return _cachedChatToken;
                }
                else
                {
                    // Error handling
                    await _discordService.SendErrorNotificationAsync("Chat Token Refresh Error!", "ChatTokenService", null);
                    LogError($"Chat Token Refresh failed: {response.StatusCode}");
                    LogError($"Response: {responseContent}");

                    // Fallback to current token
                    var fallbackToken = _configuration["Twitch:ChatOAuthToken"];
                    LogWarning($"Using fallback token: {(!string.IsNullOrEmpty(fallbackToken) ? "available" : "not available")}");
                    return fallbackToken ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                await _discordService.SendErrorNotificationAsync("Chat Token Refresh Error!", "ChatTokenService", ex);
                LogError($"Chat Token Refresh Error: {ex.Message}");
                return _configuration["Twitch:ChatOAuthToken"] ?? string.Empty;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        private async Task<bool> ValidateChatTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    LogWarning("Chat Token ist leer oder null");
                    _chatTokenExpiry = DateTime.MinValue;
                    return false;
                }

                LogDebug("Validiere Chat Token...");

                var clientId = _configuration["Twitch:BotClientId"];
                if (string.IsNullOrEmpty(clientId))
                {
                    LogError("Bot ClientId fehlt in Konfiguration!");
                    return false;
                }

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("Client-Id", clientId);

                var response = await client.GetAsync("https://id.twitch.tv/oauth2/validate");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var validation = JsonSerializer.Deserialize<JsonElement>(content);
                    var expiresIn = validation.GetProperty("expires_in").GetInt32();

                    _chatTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

                    var remainingHours = TimeSpan.FromSeconds(expiresIn).TotalHours;
                    LogSuccess($"Chat Token g�ltig f�r weitere {remainingHours:F1}h (bis {_chatTokenExpiry:dd.MM.yyyy HH:mm})");

                    // Pr�fe Scopes
                    if (validation.TryGetProperty("scopes", out var scopesElement))
                    {
                        var scopes = scopesElement.EnumerateArray().Select(s => s.GetString()).ToList();
                        LogDebug($"Token Scopes: {string.Join(", ", scopes)}");
                    }

                    return true;
                }
                else
                {
                    LogWarning($"Chat Token Validierung fehlgeschlagen: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    LogDebug($"Validation Error Response: {errorContent}");
                    _chatTokenExpiry = DateTime.MinValue;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Chat Token Validation Error: {ex.Message}");
                _chatTokenExpiry = DateTime.MinValue;
                return false;
            }
        }

        private async Task<string?> LoadChatRefreshTokenFromFileAsync()
        {
            try
            {
                var chatTokenFilePath = "/app/chat-tokens.json";
                if (File.Exists(chatTokenFilePath))
                {
                    var json = await File.ReadAllTextAsync(chatTokenFilePath);
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

                    if (tokenData.TryGetProperty("ChatRefreshToken", out var tokenProp))
                    {
                        var refreshToken = tokenProp.GetString();
                        if (!string.IsNullOrWhiteSpace(refreshToken))
                        {
                            LogDebug("Chat Refresh Token aus Datei geladen");
                            return refreshToken;
                        }
                    }
                    LogWarning("Chat Token-Datei existiert, aber ChatRefreshToken ist leer");
                }
                else
                {
                    LogDebug("Chat Token-Datei existiert nicht");
                }
            }
            catch (Exception ex)
            {

                LogError($"Error loading Chat Refresh Token: {ex.Message}");
            }
            return null;
        }

        private async Task UpdateChatTokenConfigAsync(string newAccessToken, string newRefreshToken)
        {
            try
            {
                var chatTokenData = new
                {
                    ChatOAuthToken = $"oauth:{newAccessToken}",
                    ChatRefreshToken = newRefreshToken,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = _chatTokenExpiry
                };

                var json = JsonSerializer.Serialize(chatTokenData, new JsonSerializerOptions { WriteIndented = true });
                var chatTokenFilePath = "/app/chat-tokens.json";

                await File.WriteAllTextAsync(chatTokenFilePath, json);
                LogSuccess($"Chat Token gespeichert: {chatTokenFilePath}");
                LogDebug($"Chat Token g�ltig bis: {_chatTokenExpiry:dd.MM.yyyy HH:mm}");

                // Update Configuration in Memory
                _configuration["Twitch:ChatOAuthToken"] = $"oauth:{newAccessToken}";
                _configuration["Twitch:ChatRefreshToken"] = newRefreshToken;
            }
            catch (Exception ex)
            {
                await _discordService.SendErrorNotificationAsync("Error saving Chat Token!", "ChatTokenService", ex);
                LogError($"Error saving Chat Token: {ex.Message}");
            }
        }

        private async Task StartChatTokenMonitoringAsync()
        {
            LogInfo("Chat Token-�berwachung gestartet");

            // Initialer Token-Check
            try
            {
                LogInfo("Initialer Chat Token Check...");
                await GetChatTokenAsync();
            }
            catch (Exception ex)
            {

                LogError($"Initialer Chat Token Check fehlgeschlagen: {ex.Message}");
            }

            while (true)
            {
                try
                {
                    // Pr�fe alle 45 Minuten (da Chat Token nur ~4h g�ltig ist)
                    await Task.Delay(TimeSpan.FromMinutes(45));

                    LogInfo("=== Automatische Chat Token-Pr�fung ===");

                    if (_chatTokenExpiry != DateTime.MinValue)
                    {
                        var timeRemaining = _chatTokenExpiry - DateTime.UtcNow;
                        LogDebug($"Chat Token l�uft ab in: {timeRemaining.TotalHours:F1}h");

                        if (timeRemaining.TotalHours < 1)
                        {
                            LogWarning("Chat Token l�uft in weniger als 1h ab - erneuere jetzt");
                            await RefreshChatTokenAsync();
                        }
                    }
                    else
                    {
                        LogWarning("Chat Token Status unbekannt - validiere");
                        var currentToken = _configuration["Twitch:ChatOAuthToken"]?.Replace("oauth:", "");
                        if (!string.IsNullOrEmpty(currentToken))
                        {
                            await ValidateChatTokenAsync(currentToken);
                        }
                    }

                    LogSuccess("Automatische Chat Token-Pr�fung abgeschlossen");
                }
                catch (Exception ex)
                {
                    LogError($"Chat Token monitoring error: {ex.Message}");
                }
            }
        }

        public async Task<object> GetChatTokenStatusAsync()
        {
            try
            {
                // NUR ALLE STUNDE LOGGEN
                var shouldLog = DateTime.UtcNow - _lastChatStatusLogTime > _chatStatusLogInterval;

                if (shouldLog)
                {
                    LogDebug("Erstelle Chat Token Status Report...");
                    _lastChatStatusLogTime = DateTime.UtcNow;
                }

                var currentToken = _configuration["Twitch:ChatOAuthToken"]?.Replace("oauth:", "");
                var isValid = false;
                var hoursUntilExpiry = 0.0;

                if (!string.IsNullOrEmpty(currentToken))
                {
                    isValid = await ValidateChatTokenAsync(currentToken);
                    if (isValid && _chatTokenExpiry > DateTime.MinValue)
                    {
                        hoursUntilExpiry = Math.Max(0, (_chatTokenExpiry - DateTime.UtcNow).TotalHours);
                    }
                }

                var status = new
                {
                    valid = isValid,
                    expiresAt = _chatTokenExpiry != DateTime.MinValue ? _chatTokenExpiry : DateTime.UtcNow,
                    hoursUntilExpiry = hoursUntilExpiry,
                    daysUntilExpiry = hoursUntilExpiry / 24.0,
                    needsRefresh = hoursUntilExpiry < 1,
                    status = isValid ?
                        (hoursUntilExpiry < 1 ? "EXPIRES_SOON" : "VALID") :
                        "INVALID",
                    lastCheck = DateTime.UtcNow
                };

                if (shouldLog)
                {
                    LogDebug($"Chat Token Status: {status.status} ({hoursUntilExpiry:F1}h verbleibend)");
                }

                return status;
            }
            catch (Exception ex)
            {
                LogError($"Error retrieving Chat Token status: {ex.Message}");
                return new
                {
                    valid = false,
                    expiresAt = DateTime.UtcNow,
                    hoursUntilExpiry = 0.0,
                    daysUntilExpiry = 0.0,
                    needsRefresh = true,
                    status = "ERROR",
                    lastCheck = DateTime.UtcNow,
                    error = ex.Message
                };
            }
        }


        public async Task<bool> ForceRefreshChatTokenAsync()
        {
            try
            {
                LogInfo("=== Manueller Chat Token-Refresh gestartet ===");
                var newToken = await RefreshChatTokenAsync();
                var success = !string.IsNullOrEmpty(newToken); 

                if (success)
                {
                    LogSuccess("Chat Token successfully renewed");
                }
                else
                {
                    LogWarning("Chat Token-Refresh fehlgeschlagen oder kein neuer Token erhalten");
                }
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Manual Chat Token refresh error: {ex.Message}");
                return false;
            }
        }


        public async Task<string> GetDetailedChatTokenInfoAsync()
        {
            try
            {
                var status = await GetChatTokenStatusAsync();
                var statusObj = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(status));

                var info = new StringBuilder();
                info.AppendLine("=== CHAT TOKEN STATUS REPORT ===");
                info.AppendLine($"Time: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                info.AppendLine();

                info.AppendLine("?? CHAT TOKEN:");
                info.AppendLine($"   Status: {statusObj.GetProperty("status").GetString()}");
                info.AppendLine($"   G�ltig: {statusObj.GetProperty("valid").GetBoolean()}");

                if (statusObj.GetProperty("expiresAt").GetDateTime() != DateTime.MinValue)
                {
                    info.AppendLine($"   L�uft ab: {statusObj.GetProperty("expiresAt").GetDateTime():dd.MM.yyyy HH:mm}");
                    info.AppendLine($"   Verbleibend: {statusObj.GetProperty("hoursUntilExpiry").GetDouble():F1} Stunden");
                }

                info.AppendLine($"   Refresh n�tig: {statusObj.GetProperty("needsRefresh").GetBoolean()}");

                // Zus�tzliche Config-Infos
                info.AppendLine();
                info.AppendLine("?? KONFIGURATION:");
                info.AppendLine($"   Bot Client ID: {(!string.IsNullOrEmpty(_configuration["Twitch:BotClientId"]) ? "? Gesetzt" : "? Fehlt")}");
                info.AppendLine($"   Bot Client Secret: {(!string.IsNullOrEmpty(_configuration["Twitch:BotClientSecret"]) ? "? Gesetzt" : "? Fehlt")}");
                info.AppendLine($"   Chat OAuth Token: {(!string.IsNullOrEmpty(_configuration["Twitch:ChatOAuthToken"]) ? "? Gesetzt" : "? Fehlt")}");
                info.AppendLine($"   Chat Refresh Token: {(!string.IsNullOrEmpty(_configuration["Twitch:ChatRefreshToken"]) ? "? Gesetzt" : "? Fehlt")}");

                return info.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Error creating Chat Token report: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimitSemaphore?.Dispose();
            LogInfo("ChatTokenService disposed");
        }
    }
}

