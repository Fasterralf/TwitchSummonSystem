using TwitchLib.Api;
using Microsoft.AspNetCore.SignalR;
using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Models;
using System.Text.Json;
using System.Text;

namespace TwitchSummonSystem.Services
{
    public class TwitchService
    {
        private readonly TwitchAPI _twitchApi;
        private readonly LotteryService _lotteryService;
        private readonly IHubContext<SummonHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly TokenService _tokenService;
        private readonly DiscordService _discordService;

        // Logging Helper
        private void LogInfo(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ℹ️ [TWITCH] {message}");
        private void LogSuccess(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ [TWITCH] {message}");
        private void LogWarning(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ [TWITCH] {message}");
        private void LogError(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ [TWITCH] {message}");
        private void LogDebug(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔍 [TWITCH] {message}");

        public TwitchService(
            LotteryService lotteryService,
            IHubContext<SummonHub> hubContext,
            IConfiguration configuration,
            TokenService tokenService,
            DiscordService discordService)
        {
            _lotteryService = lotteryService;
            _hubContext = hubContext;
            _configuration = configuration;
            _tokenService = tokenService;
            _discordService = discordService;
            _twitchApi = new TwitchAPI();

            LogInfo("Initializing TwitchService...");
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                LogInfo("Starting Twitch API initialization...");

                var clientId = _configuration["Twitch:ClientId"];
                var channelName = _configuration["Twitch:ChannelName"];
                var channelId = _configuration["Twitch:ChannelId"];

                if (string.IsNullOrEmpty(clientId))
                {
                    LogError("Twitch ClientId missing in configuration!");
                    return;
                }

                if (string.IsNullOrEmpty(channelName))
                {
                    LogError("Twitch ChannelName missing in configuration!");
                    return;
                }

                // Hole aktuellen Access Token
                var accessToken = await _tokenService.GetUserAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    LogError("No valid access token available!");
                    return;
                }

                _twitchApi.Settings.ClientId = clientId;
                _twitchApi.Settings.AccessToken = accessToken;

                LogSuccess("Twitch API erfolgreich initialisiert");
                LogInfo($"?? Kanal: {channelName} (ID: {channelId ?? "nicht gesetzt"})");
                LogInfo($"?? Summon Reward: {_configuration["Twitch:SummonRewardName"] ?? "nicht gesetzt"}");

                // Teste API-Verbindung
                await TestApiConnectionAsync();
            }
            catch (Exception ex)
            {
                LogError($"Fehler bei Twitch Service Initialisierung: {ex.Message}");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _discordService.SendErrorNotificationAsync("Twitch Service Initialisierung fehlgeschlagen!", "TwitchService", ex);
                    }
                    catch
                    {
                        // Ignore Discord errors
                    }
                });
            }

        }

        private async Task TestApiConnectionAsync()
        {
            try
            {
                LogDebug("Teste Twitch API Verbindung...");

                var channelName = _configuration["Twitch:ChannelName"];
                if (!string.IsNullOrEmpty(channelName))
                {
                    var users = await _twitchApi.Helix.Users.GetUsersAsync(logins: new List<string> { channelName });
                    if (users?.Users?.Length > 0)
                    {
                        var user = users.Users[0];
                        LogSuccess($"API-Verbindung erfolgreich - Kanal gefunden: {user.DisplayName} (ID: {user.Id})");

                        // Update Channel ID in Config falls nicht gesetzt
                        if (string.IsNullOrEmpty(_configuration["Twitch:ChannelId"]))
                        {
                            _configuration["Twitch:ChannelId"] = user.Id;
                            LogInfo($"Channel ID automatisch gesetzt: {user.Id}");
                        }
                    }
                    else
                    {
                        LogWarning($"Kanal '{channelName}' nicht gefunden!");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"API-Verbindungstest fehlgeschlagen: {ex.Message}");
            }
        }

        public async Task<SummonResult> HandleChannelPointReward(string username, string rewardTitle)
        {
            try
            {
                LogInfo($"?? Channel Point Reward erhalten: '{rewardTitle}' von {username}");

                var summonRewardName = _configuration["Twitch:SummonRewardName"];

                if (string.IsNullOrEmpty(summonRewardName))
                {
                    LogWarning("Summon Reward Name nicht in Konfiguration gesetzt!");
                    return null!;
                }

                // Pr�fe ob es sich um den Summon Reward handelt
                var isSummonReward = rewardTitle.Contains(summonRewardName, StringComparison.CurrentCultureIgnoreCase) ||
                                   rewardTitle.Contains("test", StringComparison.CurrentCultureIgnoreCase);

                if (isSummonReward)
                {
                    LogInfo($"?? F�hre Summon f�r {username} aus...");

                    var result = _lotteryService.PerformSummon(username);

                    // Sende Ergebnis an alle verbundenen Clients
                    await _hubContext.Clients.All.SendAsync("SummonResult", result);

                    var resultText = result.IsGold ? "? GOLD!" : "? Kein Gold";
                    LogSuccess($"?? {username}: {resultText} - Pity: {result.PityCount}/80");

                    // Zus�tzliche Statistiken loggen
                    if (result.IsGold)
                    {
                        LogInfo($"?? Gold nach {result.PityCount} Versuchen! Pity Counter zur�ckgesetzt.");
                    }
                    else if (result.PityCount >= 70)
                    {
                        LogWarning($"?? Hoher Pity Counter: {result.PityCount}/80 - Gold bald garantiert!");
                    }

                    return result;
                }
                else
                {
                    LogDebug($"Reward '{rewardTitle}' ist kein Summon Reward - ignoriert");
                    return null!;
                }
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Verarbeiten des Channel Point Rewards: {ex.Message}");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _discordService.SendErrorNotificationAsync("Channel Point Reward Verarbeitung fehlgeschlagen!", "TwitchService", ex);
                    }
                    catch
                    {
                        // Ignore Discord errors
                    }
                });
                return null!;
            }

        }

        public async Task<string> GetChannelInfoAsync()
        {
            try
            {
                var channelName = _configuration["Twitch:ChannelName"];
                var channelId = _configuration["Twitch:ChannelId"];

                if (string.IsNullOrEmpty(channelName))
                {
                    return "? Kanal nicht konfiguriert";
                }

                // Versuche aktuelle Kanal-Informationen zu holen
                try
                {
                    var users = await _twitchApi.Helix.Users.GetUsersAsync(logins: new List<string> { channelName });
                    if (users?.Users?.Length > 0)
                    {
                        var user = users.Users[0];
                        return $"?? Kanal: {user.DisplayName} (ID: {user.Id}) - Status: ? Verbunden";
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Fehler beim Abrufen der Kanal-Informationen: {ex.Message}");
                }

                return $"?? Kanal: {channelName} (ID: {channelId ?? "unbekannt"}) - Status: ?? Nicht erreichbar";
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Abrufen der Kanal-Informationen: {ex.Message}");
                return "? Fehler beim Abrufen der Kanal-Informationen";
            }
        }

        public async Task<object> GetServiceStatusAsync()
        {
            try
            {
                var channelName = _configuration["Twitch:ChannelName"];
                var channelId = _configuration["Twitch:ChannelId"];
                var summonRewardName = _configuration["Twitch:SummonRewardName"];

                var isApiConnected = false;
                var channelExists = false;
                var userDisplayName = string.Empty;

                try
                {
                    if (!string.IsNullOrEmpty(channelName))
                    {
                        var users = await _twitchApi.Helix.Users.GetUsersAsync(logins: new List<string> { channelName });
                        if (users?.Users?.Length > 0)
                        {
                            isApiConnected = true;
                            channelExists = true;
                            userDisplayName = users.Users[0].DisplayName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"API-Status-Check fehlgeschlagen: {ex.Message}");
                }

                return new
                {
                    apiConnected = isApiConnected,
                    channel = new
                    {
                        name = channelName,
                        displayName = userDisplayName,
                        id = channelId,
                        exists = channelExists
                    },
                    configuration = new
                    {
                        clientIdSet = !string.IsNullOrEmpty(_configuration["Twitch:ClientId"]),
                        channelNameSet = !string.IsNullOrEmpty(channelName),
                        channelIdSet = !string.IsNullOrEmpty(channelId),
                        summonRewardNameSet = !string.IsNullOrEmpty(summonRewardName),
                        summonRewardName = summonRewardName
                    },
                    lastCheck = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Abrufen des Service Status: {ex.Message}");
                return new
                {
                    apiConnected = false,
                    channel = new { name = "", displayName = "", id = "", exists = false },
                    configuration = new { clientIdSet = false, channelNameSet = false, channelIdSet = false, summonRewardNameSet = false, summonRewardName = "" },
                    lastCheck = DateTime.UtcNow,
                    error = ex.Message
                };
            }
        }

        public async Task<bool> RefreshApiTokenAsync()
        {
            try
            {
                LogInfo("Refreshing Twitch API token...");

                var newAccessToken = await _tokenService.GetUserAccessTokenAsync();
                if (!string.IsNullOrEmpty(newAccessToken))
                {
                    _twitchApi.Settings.AccessToken = newAccessToken;
                    LogSuccess("Twitch API Token erfolgreich erneuert");

                    
                    await TestApiConnectionAsync();
                    return true;
                }
                else
                {
                    LogError("No new access token received");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Erneuern des API Tokens: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GetDetailedServiceInfoAsync()
        {
            try
            {
                var status = await GetServiceStatusAsync();
                var statusObj = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(status));

                var info = new StringBuilder();
                info.AppendLine("=== TWITCH SERVICE STATUS ===");
                info.AppendLine($"Zeitpunkt: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                info.AppendLine();

                var channel = statusObj.GetProperty("channel");
                var config = statusObj.GetProperty("configuration");

                info.AppendLine("?? KANAL-INFORMATIONEN:");
                info.AppendLine($"   Name: {channel.GetProperty("name").GetString()}");
                info.AppendLine($"   Display Name: {channel.GetProperty("displayName").GetString()}");
                info.AppendLine($"   ID: {channel.GetProperty("id").GetString()}");
                info.AppendLine($"   Existiert: {(channel.GetProperty("exists").GetBoolean() ? "? Ja" : "? Nein")}");
                info.AppendLine();

                info.AppendLine("?? API-VERBINDUNG:");
                info.AppendLine($"   Status: {(statusObj.GetProperty("apiConnected").GetBoolean() ? "? Verbunden" : "? Getrennt")}");
                info.AppendLine($"   Client ID: {(config.GetProperty("clientIdSet").GetBoolean() ? "? Gesetzt" : "? Fehlt")}");
                info.AppendLine();

                info.AppendLine("?? KONFIGURATION:");
                info.AppendLine($"   Kanal Name: {(config.GetProperty("channelNameSet").GetBoolean() ? "? Gesetzt" : "? Fehlt")}");
                info.AppendLine($"   Kanal ID: {(config.GetProperty("channelIdSet").GetBoolean() ? "? Gesetzt" : "? Fehlt")}");
                info.AppendLine($"   Summon Reward: {(config.GetProperty("summonRewardNameSet").GetBoolean() ? "? Gesetzt" : "? Fehlt")}");
                if (config.GetProperty("summonRewardNameSet").GetBoolean())
                {
                    info.AppendLine($"   Reward Name: '{config.GetProperty("summonRewardName").GetString()}'");
                }

                return info.ToString();
            }
            catch (Exception ex)
            {
                return $"? Fehler beim Erstellen des Service Reports: {ex.Message}";
            }
        }

        // Cleanup
        public void Dispose()
        {
            LogInfo("TwitchService disposed");
        }
    }
}

