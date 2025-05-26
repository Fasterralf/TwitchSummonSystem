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
        private readonly PityService _pityService;
        private TwitchClient _client;

        public TwitchChatService(IConfiguration configuration, PityService pityService)
        {
            _configuration = configuration;
            _pityService = pityService;
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

        // KORRIGIERT: Richtige Event Handler Signatur für OnDisconnected
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
                var pityCount = _pityService.GetCurrentPity();
                SendMessage($"@{username} Aktueller Pity: {pityCount}/80 (Gold Chance: {_pityService.CalculateGoldChance():F1}%)");
            }
            else if (message == "!pity reset" && (e.ChatMessage.IsModerator || e.ChatMessage.IsBroadcaster))
            {
                _pityService.ResetPity();
                SendMessage($"@{username} Pity wurde auf 0 zurückgesetzt!");
            }
            else if (message == "!summon stats")
            {
                var pityCount = _pityService.GetCurrentPity();
                var goldChance = _pityService.CalculateGoldChance();
                SendMessage($"📊 Summon Stats: Pity {pityCount}/80 | Gold Chance: {goldChance:F1}% | Hard Pity in {80 - pityCount} Summons");
            }
        }

        public void SendSummonResult(string username, bool isGold, int pityCount)
        {
            if (isGold)
            {
                SendMessage($"🌟✨ @{username} hat LEGENDARY GOLD erhalten! ⭐🎉 Pity Reset! Glückwunsch! 🎊");
            }
            else
            {
                var goldChance = _pityService.CalculateGoldChance(pityCount);
                var remaining = 80 - pityCount;

                if (pityCount >= 74) // Soft Pity
                {
                    SendMessage($"🔥 @{username} Soft Pity Zone! Pity: {pityCount}/80 | Chance: {goldChance:F1}% | Guaranteed in {remaining}!");
                }
                else if (pityCount >= 60) // Getting close
                {
                    SendMessage($"⚡ @{username} Getting close! Pity: {pityCount}/80 | Chance: {goldChance:F1}% | {remaining} until guaranteed!");
                }
                else
                {
                    SendMessage($"❌ @{username} No gold this time. Pity: {pityCount}/80 | Chance: {goldChance:F1}%");
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
