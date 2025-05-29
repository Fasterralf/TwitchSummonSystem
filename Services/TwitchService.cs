using TwitchLib.Api;
using Microsoft.AspNetCore.SignalR;
using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Models;

namespace TwitchSummonSystem.Services
{
    public class TwitchService
    {
        private readonly TwitchAPI _twitchApi;
        private readonly LotteryService _lotteryService; 
        private readonly IHubContext<SummonHub> _hubContext;
        private readonly IConfiguration _configuration;

        public TwitchService(LotteryService lotteryService, IHubContext<SummonHub> hubContext, IConfiguration configuration)
        {
            _lotteryService = lotteryService;
            _hubContext = hubContext;
            _configuration = configuration;
            _twitchApi = new TwitchAPI();

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var clientId = _configuration["Twitch:ClientId"];
            var accessToken = _configuration["Twitch:AccessToken"];

            _twitchApi.Settings.ClientId = clientId;
            _twitchApi.Settings.AccessToken = accessToken;

            Console.WriteLine("✅ Twitch Service für ArabNP initialisiert");
            Console.WriteLine($"📺 Kanal: {_configuration["Twitch:ChannelName"]}");

            await Task.CompletedTask;
        }

        public async Task<SummonResult> HandleChannelPointReward(string username, string rewardTitle)
        {
            Console.WriteLine($"🎁 Channel Point Reward: {rewardTitle} von {username}");

            var summonRewardName = _configuration["Twitch:SummonRewardName"];
            if (rewardTitle.Contains(summonRewardName!) || rewardTitle.Contains("test", StringComparison.CurrentCultureIgnoreCase))
            {
                var result = _lotteryService.PerformSummon(username);

                await _hubContext.Clients.All.SendAsync("SummonResult", result);

                Console.WriteLine($"🎲 {username}: {(result.IsGold ? "⭐ GOLD!" : "❌ Kein Gold")} - Pity: {result.PityCount}/80");

                return result;
            }

            return null!;
        }

        public string GetChannelInfo()
        {
            return $"Kanal: {_configuration["Twitch:ChannelName"]} (ID: {_configuration["Twitch:ChannelId"]})";
        }
    }
}
