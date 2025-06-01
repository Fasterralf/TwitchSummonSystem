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

        private bool _isConnected = false;
        private DateTime _lastConnectionAttempt = DateTime.MinValue;
        private int _reconnectAttempts = 0;
        private readonly int _maxReconnectAttempts = 5;

        // ← Logging Helper hinzufügen:
        private void LogInfo(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ℹ️ [CHAT] {message}");
        private void LogSuccess(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ [CHAT] {message}");
        private void LogError(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ [CHAT] {message}");

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
            LogSuccess("Chat Bot verbunden!");
            _isConnected = true;
            _reconnectAttempts = 0;
        }

        private void OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            Console.WriteLine($"✅ Chat Bot ist Kanal {e.Channel} beigetreten");
        }

        private void OnDisconnected(object? sender, OnDisconnectedEventArgs e)
        {
            LogError("Chat Bot getrennt");
            _isConnected = false;

            // Auto-Reconnect nach 5 Sekunden
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                await ReconnectAsync();
            });
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

        // Ändere diese Methode (entferne async/await da nicht benötigt):
        public Task<object> GetChatStatusAsync()
        {
            try
            {
                var channelName = _configuration["Twitch:ChannelName"];
                var botUsername = _configuration["Twitch:BotUsername"];

                var result = new
                {
                    connected = _isConnected && (_client?.IsConnected ?? false),
                    channel = channelName,
                    botUsername = botUsername,
                    reconnectAttempts = _reconnectAttempts,
                    maxReconnectAttempts = _maxReconnectAttempts,
                    lastConnectionAttempt = _lastConnectionAttempt,
                    configuration = new
                    {
                        channelNameSet = !string.IsNullOrEmpty(channelName),
                        botUsernameSet = !string.IsNullOrEmpty(botUsername),
                        chatTokenSet = !string.IsNullOrEmpty(_configuration["Twitch:ChatOAuthToken"])
                    },
                    lastCheck = DateTime.UtcNow
                };

                return Task.FromResult<object>(result);
            }
            catch (Exception ex)
            {
                LogError($"Fehler beim Abrufen des Chat Status: {ex.Message}");
                var errorResult = new { connected = false, error = ex.Message, lastCheck = DateTime.UtcNow };
                return Task.FromResult<object>(errorResult);
            }
        }


        public async Task<bool> ForceReconnectAsync()
        {
            try
            {
                LogInfo("=== Manueller Chat-Reconnect gestartet ===");

                if (_client?.IsConnected == true)
                {
                    _client.Disconnect();
                    await Task.Delay(2000);
                }

                _reconnectAttempts = 0;
                await ReconnectAsync();

                return _isConnected;
            }
            catch (Exception ex)
            {
                LogError($"Manueller Reconnect fehlgeschlagen: {ex.Message}");
                return false;
            }
        }

        private async Task ReconnectAsync()
        {
            if (_reconnectAttempts >= _maxReconnectAttempts)
            {
                LogError($"Maximale Reconnect-Versuche erreicht ({_maxReconnectAttempts})");
                return;
            }

            try
            {
                _reconnectAttempts++;
                _lastConnectionAttempt = DateTime.UtcNow;

                LogInfo($"Reconnect-Versuch {_reconnectAttempts}/{_maxReconnectAttempts}...");

                var channelName = _configuration["Twitch:ChannelName"];
                var botUsername = _configuration["Twitch:BotUsername"];
                var chatToken = await _chatTokenService.GetChatTokenAsync();

                if (string.IsNullOrEmpty(chatToken))
                {
                    LogError("Kein Chat Token verfügbar für Reconnect");
                    return;
                }

                var credentials = new ConnectionCredentials(botUsername, chatToken);
                _client.Initialize(credentials, channelName);
                _client.Connect();

                // Warte kurz und prüfe Verbindung
                await Task.Delay(3000);

                if (_client.IsConnected)
                {
                    LogSuccess("Reconnect erfolgreich!");
                    _isConnected = true;
                    _reconnectAttempts = 0;
                }
                else
                {
                    LogError("Reconnect fehlgeschlagen - Client nicht verbunden");
                    // Nächster Versuch in 10 Sekunden
                    await Task.Delay(10000);
                    _ = Task.Run(ReconnectAsync);
                }
            }
            catch (Exception ex)
            {
                LogError($"Reconnect-Fehler: {ex.Message}");
                // Nächster Versuch in 15 Sekunden
                await Task.Delay(15000);
                _ = Task.Run(ReconnectAsync);
            }
        }
    }
}
