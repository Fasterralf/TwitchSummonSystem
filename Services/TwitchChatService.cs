using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;

namespace TwitchSummonSystem.Services
{
    public class TwitchChatService
    {
        private readonly IConfiguration _configuration;
        private readonly LotteryService _lotteryService;
        private TwitchClient _client;

        public TwitchChatService(IConfiguration configuration, LotteryService lotteryService)
        {
            _configuration = configuration;
            _lotteryService = lotteryService;
            InitializeChatBot();
        }

        private void InitializeChatBot()
        {
            try
            {
                var channelName = _configuration["Twitch:ChannelName"];
                var botUsername = _configuration["Twitch:BotUsername"];
                var chatToken = _configuration["Twitch:ChatOAuthToken"];

                Console.WriteLine($"🤖 Initialisiere Chat Bot für Kanal: {channelName}");

                var clientOptions = new ClientOptions
                {
                    MessagesAllowedInPeriod = 750,
                    ThrottlingPeriod = TimeSpan.FromSeconds(30)
                };

                var customClient = new WebSocketClient(clientOptions);
                _client = new TwitchClient(customClient);

                var credentials = new ConnectionCredentials(botUsername, chatToken);
                _client.Initialize(credentials, channelName);

                // Event Handler
                _client.OnConnected += OnConnected;
                _client.OnJoinedChannel += OnJoinedChannel;
                _client.OnMessageReceived += OnMessageReceived;
                _client.OnDisconnected += OnDisconnected; 

                _client.Connect();

                Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    if (_client.IsConnected)
                    {
                        Console.WriteLine("✅ Chat Bot ist bereit für Nachrichten");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Chat Bot Fehler: {ex.Message}");
            }
        }

        private void OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine("✅ Chat Bot verbunden!");
        }

        private void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Console.WriteLine($"✅ Chat Bot ist Kanal {e.Channel} beigetreten");
        }

        private void OnDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            Console.WriteLine("❌ Chat Bot getrennt");
        }

        private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            var message = e.ChatMessage.Message.ToLower();
            var username = e.ChatMessage.Username;

            if (message == "!pity")
            {
                var goldChance = _lotteryService.CalculateGoldChance() * 100;
                var lotteryData = _lotteryService.GetLotteryData();
                SendMessage($"@{username} Gold Chance: {goldChance:F1}% | Summons: {lotteryData.TotalSummons} | Golds: {lotteryData.TotalGolds}");
            }
            else if (message == "!pity reset" && (e.ChatMessage.IsModerator || e.ChatMessage.IsBroadcaster))
            {
                _lotteryService.ResetLottery();
                SendMessage($"@{username} Lottery wurde zurückgesetzt!");
            }
            else if (message == "!summon stats")
            {
                var goldChance = _lotteryService.CalculateGoldChance() * 100;
                var lotteryData = _lotteryService.GetLotteryData();
                var goldRate = lotteryData.TotalSummons > 0 ? (double)lotteryData.TotalGolds / lotteryData.TotalSummons * 100 : 0;
                SendMessage($"📊 Stats: {lotteryData.TotalSummons} Summons | {goldChance:F1}% Chance | {goldRate:F1}% Rate | {lotteryData.TotalGolds} Golds");
            }
        }

        public void SendSummonResult(string username, bool isGold, int pityCount)
        {
            var lotteryData = _lotteryService.GetLotteryData();
            var goldChance = lotteryData.CurrentGoldChance; 

            if (isGold)
            {
                SendMessage($"🌟✨ @{username} hat LEGENDARY GOLD erhalten! ⭐🎉 Chance war: {goldChance:F1}%! 🎊");
            }
            else
            {
                SendMessage($"❌ @{username} No gold. Chance: {goldChance:F1}% | Summons: {lotteryData.TotalSummons} | Golds: {lotteryData.TotalGolds}");
            }
        }


        private void SendMessage(string message)
        {
            try
            {
                var channelName = _configuration["Twitch:ChannelName"];
                _client?.SendMessage(channelName, message);
                Console.WriteLine($"💬 Chat: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Chat Nachricht Fehler: {ex.Message}");
            }
        }
    }
}
