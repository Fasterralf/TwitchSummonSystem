using Microsoft.AspNetCore.SignalR;
using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Models;
using System.Text.Json;
using System.Text;

namespace TwitchSummonSystem.Services
{
    public class TwitchEventSubService
    {
        private readonly LotteryService _lotteryService;
        private readonly IHubContext<SummonHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly TokenService _tokenService;
        private readonly HttpClient _httpClient;
        private bool _isInitialized = false;

        private readonly TwitchChatService _chatService;

        public TwitchEventSubService(LotteryService lotteryService, IHubContext<SummonHub> hubContext, IConfiguration configuration, TokenService tokenService, TwitchChatService chatService)
        {
            _lotteryService = lotteryService;
            _hubContext = hubContext;
            _configuration = configuration;
            _tokenService = tokenService;
            _chatService = chatService; // NEU
            _httpClient = new HttpClient();
        }

        public async Task<bool> EnsureInitializedAsync()
        {
            if (_isInitialized) return true;

            var clientId = _configuration["Twitch:ClientId"];

            Console.WriteLine($"🔑 Client ID: {clientId}");

            // App Access Token holen
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

        public async Task<bool> CreateChannelPointRewardSubscription(string callbackUrl)
        {
            // Erst initialisieren
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
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EventSub Fehler: {ex.Message}");
                return false;
            }
        }

        public async Task<SummonResult> HandleChannelPointRedemption(JsonElement eventData)
        {
            try
            {
                var username = eventData.GetProperty("user_name").GetString();
                var rewardTitle = eventData.GetProperty("reward").GetProperty("title").GetString();
                var rewardCost = eventData.GetProperty("reward").GetProperty("cost").GetInt32();

                Console.WriteLine($"🎁 Channel Point Reward: {rewardTitle} ({rewardCost} Punkte) von {username}");

                var summonRewardName = _configuration["Twitch:SummonRewardName"];
                if (rewardTitle?.ToLower() == summonRewardName?.ToLower() ||
                    rewardTitle?.ToLower().Contains("test") == true)
                {
                    var result = _lotteryService.PerformSummon(username);
                    await _hubContext.Clients.All.SendAsync("SummonResult", result);

                    _chatService.SendSummonResult(username, result.IsGold, result.PityCount);


                    Console.WriteLine($"🎲 {username}: {(result.IsGold ? "⭐ GOLD!" : "❌ Kein Gold")} - Pity: {result.PityCount}/80");

                    return result;
                }

                Console.WriteLine($"ℹ️ Kein Summon Reward: {rewardTitle} (erwartet: {summonRewardName})");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler bei Channel Point Redemption: {ex.Message}");
                return null;
            }
        }

        private string GenerateWebhookSecret()
        {
            return Guid.NewGuid().ToString("N")[..16];
        }
    }
}
