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
        private readonly ChatTokenService _chatTokenService; // ← Neu
        private TwitchClient _client = null!;

        public TwitchChatService(IConfiguration configuration, LotteryService lotteryService, ChatTokenService chatTokenService)
        {
            _configuration = configuration;
            _lotteryService = lotteryService;
            _chatTokenService = chatTokenService; // ← Neu
            _ = Task.Run(async () => await InitializeChatBot());

        }

        private async Task InitializeChatBot()
        {
            try
            {
                var channelName = _configuration["Twitch:ChannelName"];
                var botUsername = _configuration["Twitch:BotUsername"];
                var chatToken = await _chatTokenService.GetChatTokenAsync(); // ← Verwende ChatTokenService

                Console.WriteLine($"🤖 Initialisiere Chat Bot für Kanal: {channelName}");
                Console.WriteLine($"🔑 Chat Token: {chatToken[..15]}...");

                var clientOptions = new ClientOptions
                {
                    MessagesAllowedInPeriod = 750,
                    ThrottlingPeriod = TimeSpan.FromSeconds(30)
                };

                var customClient = new WebSocketClient(clientOptions);
                _client = new TwitchClient(customClient);

                var credentials = new ConnectionCredentials(botUsername, chatToken);
                _client.Initialize(credentials, channelName);

                _client.OnConnected += OnConnected;
                _client.OnJoinedChannel += OnJoinedChannel;
                _client.OnMessageReceived += OnMessageReceived;
                _client.OnDisconnected += OnDisconnected;

                _client.Connect();

                _ = Task.Run(async () =>
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

        private void OnConnected(object? sender, OnConnectedArgs e)
        {
            Console.WriteLine("✅ Chat Bot verbunden!");
        }

        private void OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            Console.WriteLine($"✅ Chat Bot ist Kanal {e.Channel} beigetreten");
        }

        private void OnDisconnected(object? sender, OnDisconnectedEventArgs e)
        {
            Console.WriteLine("❌ Chat Bot getrennt");
        }

        private void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
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
            var random = new Random();

            if (isGold)
            {
                var goldMessages = new[]
                {
            $"🌟✨ LEGENDARY! ✨🌟 @{username} hat GOLD erhalten! ⭐🎉😱🎊",
            $"🔥⚡ AMAZING! ⚡🔥 @{username} ist der GOLD Champion! 🌟😍🎊⭐",
            $"🎊🌟 INCREDIBLE! 🌟🎊 @{username} hat das LEGENDARY GOLD! ⭐✨🤯🔥",
            $"⭐🎉 FANTASTIC! 🎉⭐ @{username} hat GOLD gesummoned! 🌟💫🏆😎",
            $"🔥🌟 GODLIKE! 🌟🔥 @{username} mit dem LEGENDARY Pull! ⭐🤩🎊✨",
            $"🎊⚡ INSANE! ⚡🎊 @{username} ist ein GOLD Legend! 🌟😤💫⭐"
        };
                SendMessage(goldMessages[random.Next(goldMessages.Length)]);
            }
            else
            {
                var normalMessages = new[]
                {
            $"🎲 @{username} Normal Summon - Bis zum nächsten Stream! 💪✨😔",
            $"🎯 @{username} Kein Gold heute - Nächster Stream, neue Chance! ⭐😅",
            $"🎮 @{username} Normal Hit - Stream Summon verbraucht! 🔥 See you next time! 👋😊",
            $"🎲 @{username} Nicht heute - Aber nächsten Stream wieder! 🌟😬💪",
            $"🎯 @{username} Normal Summon - Nächster Stream = neue Hoffnung! 🚀🤞⭐",
            $"🎮 @{username} Kein Glück heute - nächster Stream wird's besser! 💪😤🌟",
            $"🎲 @{username} Stream Summon done - Next stream, next chance! ✨👍👋",
            $"🎯 @{username} Normal - Aber hey, nächster Stream wartet! 🚀😉⭐"
        };
                SendMessage(normalMessages[random.Next(normalMessages.Length)]);
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
