using Microsoft.AspNetCore.SignalR;
using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Models;
using System.Text.Json;
using System.Text;
using System.Globalization;

namespace TwitchSummonSystem.Services
{
    public class TwitchEventSubService
    {
        private readonly LotteryService _lotteryService;
        private readonly DiscordService _discordService;
        private readonly IHubContext<SummonHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly TokenService _tokenService;
        private readonly HttpClient _httpClient;
        private bool _isInitialized = false;
        private string _rewardId = null!;
        private string _currentRewardTitle = "test";
        private string _configuredRewardName = null!;


        private readonly TwitchChatService _chatService;

        public TwitchEventSubService
            (LotteryService lotteryService, 
            IHubContext<SummonHub> hubContext, 
            IConfiguration configuration, 
            TokenService tokenService,
            TwitchChatService chatService,
            DiscordService discordService)
        {
            _lotteryService = lotteryService;
            _hubContext = hubContext;
            _configuration = configuration;
            _tokenService = tokenService;
            _chatService = chatService;
            _discordService = discordService;
            _httpClient = new HttpClient();
        }

        public async Task<bool> EnsureInitializedAsync()
        {
            if (_isInitialized) return true;

            var clientId = _configuration["Twitch:ClientId"];

            Console.WriteLine($"🔑 Client ID: {clientId}");

            var appAccessToken = await _tokenService.GetAppAccessTokenAsync();

            if (appAccessToken != null)
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {appAccessToken}");

                Console.WriteLine("✅ Twitch EventSub Service mit App Token initialisiert");
                _isInitialized = true;
                return true;
            }
            else
            {
                Console.WriteLine("❌ App Access Token konnte nicht abgerufen werden");
                return false;
            }
        }

        public async Task InitializeRewardAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_rewardId))
                {
                    var rewardId = await FindSummonRewardIdAsync();
                    if (!string.IsNullOrEmpty(rewardId))
                    {
                        _rewardId = rewardId;
                        Console.WriteLine($"✅ Reward ID initialisiert: {_rewardId}");
                    }
                    else
                    {
                        Console.WriteLine("❌ Konnte Reward ID nicht finden");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler bei Reward Initialisierung: {ex.Message}");
            }
        }


        public async Task<bool> CreateChannelPointRewardSubscription(string callbackUrl)
        {
            if (!await EnsureInitializedAsync())
            {
                return false;
            }

            try
            {
                var channelId = _configuration["Twitch:ChannelId"];
                var webhookSecret = GenerateWebhookSecret();

                Console.WriteLine($"📡 Erstelle Subscription für Kanal: {channelId}");
                Console.WriteLine($"🔗 Callback URL: {callbackUrl}");

                var subscriptionData = new
                {
                    type = "channel.channel_points_custom_reward_redemption.add",
                    version = "1",
                    condition = new
                    {
                        broadcaster_user_id = channelId
                    },
                    transport = new
                    {
                        method = "webhook",
                        callback = callbackUrl,
                        secret = webhookSecret
                    }
                };

                var json = JsonSerializer.Serialize(subscriptionData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Console.WriteLine($"📤 Sende Request: {json}");

                var response = await _httpClient.PostAsync("https://api.twitch.tv/helix/eventsub/subscriptions", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📡 EventSub Response: {response.StatusCode}");
                Console.WriteLine($"📄 Response Body: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ EventSub Subscription erstellt für Kanal {channelId}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ EventSub fehlgeschlagen: {response.StatusCode}");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _discordService.SendErrorNotificationAsync($"EventSub Subscription fehlgeschlagen: {response.StatusCode}", "TwitchEventSubService", null);
                        }
                        catch
                        {
                            // Ignore Discord errors
                        }
                    });
                    return false;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EventSub Fehler: {ex.Message}");
                // NEU HINZUFÜGEN:
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _discordService.SendErrorNotificationAsync("EventSub Service Exception!", "TwitchEventSubService", ex);
                    }
                    catch
                    {
                        // Ignore Discord errors
                    }
                });
                return false;
            }

        }

        public async Task<SummonResult> HandleChannelPointRedemption(JsonElement eventData)
        {
            try
            {
                var rewardCost = eventData.GetProperty("reward").GetProperty("cost").GetInt32();
                if (!eventData.TryGetProperty("user_name", out var userNameElement) ||
                    !eventData.TryGetProperty("reward", out var rewardElement) ||
                    !rewardElement.TryGetProperty("title", out var titleElement))
                {
                    Console.WriteLine("❌ Unvollständige Event-Daten erhalten");
                    return null!;
                }

                var username = userNameElement.GetString();
                var rewardTitle = titleElement.GetString();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(rewardTitle))
                {
                    Console.WriteLine("❌ Username oder RewardTitle ist leer");
                    return null!;
                }

                Console.WriteLine($"🎁 Channel Point Reward: {rewardTitle} ({rewardCost} Punkte) von {username}");

                var configuredRewardName = GetCurrentSummonRewardName();
                if (rewardTitle?.Equals(configuredRewardName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    var result = _lotteryService.PerformSummon(username!);
                    await _hubContext.Clients.All.SendAsync("SummonResult", result);

                    _chatService.SendSummonResult(username!, result.IsGold, result.PityCount);

                    var lotteryData = _lotteryService.GetLotteryData();
                    Console.WriteLine($"🎲 {username}: {(result.IsGold ? "⭐ GOLD!" : "❌ Kein Gold")} - Chance: {lotteryData.CurrentGoldChance:F1}%");

                    return result;
                }

                Console.WriteLine($"ℹ️ Kein Summon Reward: {rewardTitle} (erwartet: {configuredRewardName})");
                return null!;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler bei Channel Point Redemption: {ex.Message}");
                return null!;
            }
        }

        private async Task<string> FindSummonRewardIdAsync()
        {
            try
            {
                var channelId = _configuration["Twitch:ChannelId"];
                var userToken = await _tokenService.GetUserAccessTokenAsync(); 
                var clientId = _configuration["Twitch:ClientId"];

                Console.WriteLine($"🔑 Verwende User Token für Channel Points API: {userToken?[..10]}...");

                using var userClient = new HttpClient();
                userClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                userClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {userToken}");

                var response = await userClient.GetAsync($"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={channelId}&only_manageable_rewards=true");

                Console.WriteLine($"📡 Get Rewards Response: {response.StatusCode}");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var rewardsData = JsonSerializer.Deserialize<JsonElement>(content);
                    if (rewardsData.TryGetProperty("data", out var rewards))
                    {
                        Console.WriteLine($"🔍 Gefundene Rewards: {rewards.GetArrayLength()}");
                        foreach (var reward in rewards.EnumerateArray())
                        {
                            var title = reward.GetProperty("title").GetString();
                            var id = reward.GetProperty("id").GetString();
                            Console.WriteLine($"🎯 Prüfe Reward: '{title}' (ID: {id})");

                            var configuredRewardName = GetCurrentSummonRewardName();
                            if (title?.Equals(configuredRewardName, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                Console.WriteLine($"✅ Summon Reward gefunden: {title} (ID: {id})");
                                _currentRewardTitle = title;
                                _rewardId = id!;
                                return id!;
                            }
                        }
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine("🔄 Token ungültig - versuche Refresh...");
                    return null!;
                }

                Console.WriteLine("❌ Summon Reward nicht gefunden");
                return null!;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Suchen des Rewards: {ex.Message}");
                return null!;
            }
        }

        public async Task<List<object>> GetAllRewardsAsync()
        {
            try
            {
                var channelId = _configuration["Twitch:ChannelId"];
                var userToken = await _tokenService.GetUserAccessTokenAsync(); 
                var clientId = _configuration["Twitch:ClientId"];

                using var userClient = new HttpClient();
                userClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                userClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {userToken}");

                var response = await userClient.GetAsync($"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={channelId}&only_manageable_rewards=true");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var rewardsData = JsonSerializer.Deserialize<JsonElement>(content);
                    var rewardList = new List<object>();

                    if (rewardsData.TryGetProperty("data", out var rewards))
                    {
                        foreach (var reward in rewards.EnumerateArray())
                        {
                            rewardList.Add(new
                            {
                                id = reward.GetProperty("id").GetString(),
                                title = reward.GetProperty("title").GetString(),
                                cost = reward.GetProperty("cost").GetInt32(),
                                is_enabled = reward.GetProperty("is_enabled").GetBoolean()
                            });
                        }
                    }
                    return rewardList;
                }
                return [];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Abrufen aller Rewards: {ex.Message}");
                return [];
            }
        }

        public async Task<bool> UpdateSummonRewardNameAsync(string newRewardName)
        {
            try
            {
                _configuredRewardName = newRewardName;

                var newRewardId = await FindRewardByNameAsync(newRewardName);
                if (newRewardId != null)
                {
                    _rewardId = newRewardId;
                    _currentRewardTitle = newRewardName;
                    Console.WriteLine($"✅ Reward Name geändert zu: '{newRewardName}' (ID: {newRewardId})");
                    return true;
                }

                Console.WriteLine($"❌ Reward '{newRewardName}' nicht gefunden");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Ändern des Reward Names: {ex.Message}");
                return false;
            }
        }

        public string GetCurrentSummonRewardName()
        {
            return _configuredRewardName ?? _configuration["Twitch:SummonRewardName"] ?? "test";
        }

        private async Task<string> FindRewardByNameAsync(string rewardName)
        {
            try
            {
                var channelId = _configuration["Twitch:ChannelId"];
                var userToken = _configuration["Twitch:AccessToken"];
                var clientId = _configuration["Twitch:ClientId"];

                using var userClient = new HttpClient();
                userClient.DefaultRequestHeaders.Add("Client-Id", clientId);
                userClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {userToken}");

                var response = await userClient.GetAsync($"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={channelId}");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var rewardsData = JsonSerializer.Deserialize<JsonElement>(content);
                    if (rewardsData.TryGetProperty("data", out var rewards))
                    {
                        foreach (var reward in rewards.EnumerateArray())
                        {
                            var title = reward.GetProperty("title").GetString();
                            var id = reward.GetProperty("id").GetString();

                            if (title?.Equals(rewardName, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                return id!;
                            }
                        }
                    }
                }
                return null!;
            }
            catch
            {
                return null!;
            }
        }

        public string GetCurrentRewardTitle()
        {
            return _currentRewardTitle;
        }

        private static string GenerateWebhookSecret()
        {
            return Guid.NewGuid().ToString("N")[..16];
        }
    }
}
