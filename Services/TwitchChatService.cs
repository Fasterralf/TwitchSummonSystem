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
                _client.OnDisconnected += OnDisconnected; // KORRIGIERT

                _client.Connect();
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

            // Chat Commands
            if (message == "!pity")
            {
                var pityCount = _lotteryService.GetCurrentPity(); // GEÄNDERT
                var goldChance = _lotteryService.CalculateGoldChance() * 100; // GEÄNDERT
                var lotteryData = _lotteryService.GetLotteryData(); // GEÄNDERT
                SendMessage($"@{username} Pity: {pityCount}/80 | Gold Chance: {goldChance:F1}% | Kugeln im Topf: {lotteryData.TotalBalls}");
            }
            else if (message == "!pity reset" && (e.ChatMessage.IsModerator || e.ChatMessage.IsBroadcaster))
            {
                _lotteryService.ResetLottery(); // GEÄNDERT
                SendMessage($"@{username} Lottery wurde zurückgesetzt!");
            }
            else if (message == "!summon stats")
            {
                var pityCount = _lotteryService.GetCurrentPity(); // GEÄNDERT
                var goldChance = _lotteryService.CalculateGoldChance() * 100; // GEÄNDERT
                var lotteryData = _lotteryService.GetLotteryData(); // GEÄNDERT
                SendMessage($"📊 Stats: Pity {pityCount}/80 | Gold Chance: {goldChance:F1}% | Kugeln: {lotteryData.TotalBalls} | Guaranteed in {lotteryData.LoseBalls}");
            }
        }

        public void SendSummonResult(string username, bool isGold, int pityCount)
        {
            var lotteryData = _lotteryService.GetLotteryData(); // GEÄNDERT
            var goldChance = _lotteryService.CalculateGoldChance() * 100; // GEÄNDERT

            if (isGold)
            {
                SendMessage($"🌟✨ @{username} hat LEGENDARY GOLD erhalten! ⭐🎉 Lottery Reset! Glückwunsch! 🎊");
            }
            else
            {
                var remaining = lotteryData.LoseBalls; // GEÄNDERT
                if (lotteryData.TotalBalls <= 6) // Sehr wenige Kugeln übrig
                {
                    SendMessage($"🔥 @{username} FAST GUARANTEED! Nur noch {lotteryData.TotalBalls} Kugeln! Chance: {goldChance:F1}%!");
                }
                else if (lotteryData.TotalBalls <= 20) // Getting close
                {
                    SendMessage($"⚡ @{username} Getting close! {lotteryData.TotalBalls} Kugeln übrig | Chance: {goldChance:F1}%");
                }
                else
                {
                    SendMessage($"❌ @{username} No gold. Pity: {pityCount}/80 | Chance: {goldChance:F1}% | {lotteryData.TotalBalls} Kugeln übrig");
                }
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
