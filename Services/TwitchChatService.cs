using Microsoft.Extensions.Diagnostics.HealthChecks;
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
        private readonly ChatTokenService _chatTokenService;
        private readonly DiscordService _discordService;
        private TwitchClient? _client;
        private bool _isConnected = false;
        private bool _isReconnecting = false;
        private DateTime _lastConnectionAttempt = DateTime.MinValue;
        private int _reconnectAttempts = 0;
        private readonly int _maxReconnectAttempts = 5;
        private readonly SemaphoreSlim _reconnectSemaphore = new(1, 1);
        private readonly Timer _healthCheckTimer;

        // Logging Helper
        private void LogInfo(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ℹ️ [CHAT] {message}");
        private void LogSuccess(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ [CHAT] {message}");
        private void LogError(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ [CHAT] {message}");

        public bool IsConnected => _isConnected && (_client?.IsConnected ?? false);

        public TwitchChatService(IConfiguration configuration, LotteryService lotteryService, ChatTokenService chatTokenService, DiscordService discordService)
        {
            _configuration = configuration;
            _lotteryService = lotteryService;
            _chatTokenService = chatTokenService;
            _discordService = discordService;

            // Health Check Timer - prüft alle 30 Sekunden
            _healthCheckTimer = new Timer(HealthCheck, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Initialisierung verzögert starten
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Kurz warten bis andere Services bereit sind
                await InitializeChatBot();
            });
        }

        private async Task InitializeChatBot()
        {
            try
            {
                LogInfo("Initialisiere Chat Bot...");

                var channelName = _configuration["Twitch:ChannelName"];
                var botUsername = _configuration["Twitch:BotUsername"];

                if (string.IsNullOrEmpty(channelName) || string.IsNullOrEmpty(botUsername))
                {
                    LogError("Channel Name oder Bot Username nicht konfiguriert");
                    return;
                }

                var chatToken = await _chatTokenService.GetChatTokenAsync();
                if (string.IsNullOrEmpty(chatToken))
                {
                    LogError("Kein Chat Token verfügbar");
                    return;
                }

                Console.WriteLine($"🤖 Initialisiere Chat Bot für Kanal: {channelName}");
                Console.WriteLine($"🔑 Chat Token: {chatToken[..15]}...");

                await CreateAndConnectClient(botUsername, chatToken, channelName);
            }
            catch (Exception ex)
            {
                LogError($"Chat Bot Initialisierung fehlgeschlagen: {ex.Message}");
                await _discordService.SendErrorNotificationAsync("Chat Bot Verbindung Initialisierung fehlgeschlagen", "TwitchChatService", ex);
                // Retry nach 10 Sekunden
                _ = Task.Run(async () =>
                {
                    await Task.Delay(10000);
                    await InitializeChatBot();
                });
            }
        }

        private async Task CreateAndConnectClient(string botUsername, string chatToken, string channelName)
        {
            try
            {
                // Alte Verbindung sauber trennen
                if (_client != null)
                {
                    try
                    {
                        _client.OnConnected -= OnConnected;
                        _client.OnJoinedChannel -= OnJoinedChannel;
                        _client.OnMessageReceived -= OnMessageReceived;
                        _client.OnDisconnected -= OnDisconnected;

                        if (_client.IsConnected)
                        {
                            _client.Disconnect();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Fehler beim Trennen der alten Verbindung: {ex.Message}");
                        await _discordService.SendErrorNotificationAsync("Fehler beim Trennen der alten Verbindung", "TwitchChatService", ex);
                    }

                    await Task.Delay(1000); // Kurz warten
                }

                // Neuen Client erstellen
                var clientOptions = new ClientOptions
                {
                    MessagesAllowedInPeriod = 750,
                    ThrottlingPeriod = TimeSpan.FromSeconds(30)
                };

                var customClient = new WebSocketClient(clientOptions);
                _client = new TwitchClient(customClient);

                var credentials = new ConnectionCredentials(botUsername, chatToken);
                _client.Initialize(credentials, channelName);

                // Event Handler registrieren
                _client.OnConnected += OnConnected;
                _client.OnJoinedChannel += OnJoinedChannel;
                _client.OnMessageReceived += OnMessageReceived;
                _client.OnDisconnected += OnDisconnected;

                // Verbindung herstellen
                _client.Connect();

                // Warten auf Verbindung mit Timeout
                var timeout = TimeSpan.FromSeconds(15);
                var startTime = DateTime.Now;

                while (!(_client?.IsConnected ?? false) && DateTime.Now - startTime < timeout)
                {
                    await Task.Delay(500);
                }

                if (_client?.IsConnected ?? false)
                {
                    LogSuccess("Chat Bot erfolgreich verbunden!");
                }
                else
                {
                    LogError("Chat Bot Verbindung Timeout");
                    await _discordService.SendErrorNotificationAsync("Chat Bot Verbindung Timeout", "TwitchChatService");
                    throw new TimeoutException("Chat Bot Verbindung Timeout");
                }
            }
            catch (Exception ex)
            {
                LogError($"Client Erstellung/Verbindung fehlgeschlagen: {ex.Message}");
                await _discordService.SendErrorNotificationAsync("Chat Bot Verbindung fehlgeschlagen", "TwitchChatService", ex);
                throw;
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

            // Zusätzliche Bestätigung nach dem Channel Join
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                if (_client?.IsConnected ?? false)
                {
                    Console.WriteLine("✅ Chat Bot ist bereit für Nachrichten");
                }
            });
        }

        private void OnDisconnected(object? sender, OnDisconnectedEventArgs e)
        {
            LogError("Chat Bot getrennt");
            _isConnected = false;

            // Auto-Reconnect nur wenn nicht bereits am reconnecten
            if (!_isReconnecting)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    await ReconnectAsync();
                });
            }
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

        public async void SendSummonResult(string username, bool isGold, int pityCount)
        {
            // Prüfen ob verbunden vor dem Senden
            if (!IsConnected)
            {
                LogError("Kann Summon Result nicht senden - Chat nicht verbunden");
                await _discordService.SendErrorNotificationAsync("Chat nicht verbunden", "TwitchChatService", new Exception("Chat nicht verbunden"));
                // Versuche Reconnect
                _ = Task.Run(ReconnectAsync);
                return;
            }

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

        private async void SendMessage(string message)
        {
            try
            {
                if (!IsConnected)
                {
                    LogError("Kann Nachricht nicht senden - Chat nicht verbunden");
                    return;
                }

                var channelName = _configuration["Twitch:ChannelName"];
                _client?.SendMessage(channelName, message);
                Console.WriteLine($"💬 Chat: {message}");
            }
            catch (Exception ex)
            {
                await _discordService.SendErrorNotificationAsync("Chat Nachricht Fehler", "TwitchChatService", ex);
                Console.WriteLine($"❌ Chat Nachricht Fehler: {ex.Message}");
            }
        }

        public async Task<object> GetChatStatusAsync()
        {
            try
            {
                var channelName = _configuration["Twitch:ChannelName"];
                var botUsername = _configuration["Twitch:BotUsername"];
                var result = new
                {
                    connected = IsConnected,
                    channel = channelName,
                    botUsername = botUsername,
                    reconnectAttempts = _reconnectAttempts,
                    maxReconnectAttempts = _maxReconnectAttempts,
                    lastConnectionAttempt = _lastConnectionAttempt,
                    isReconnecting = _isReconnecting,
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
                await _discordService.SendErrorNotificationAsync("Chat Status Fehler", "TwitchChatService", ex);
                var errorResult = new { connected = false, error = ex.Message, lastCheck = DateTime.UtcNow };
                return Task.FromResult<object>(errorResult);
            }
        }

        public async Task<bool> ForceReconnectAsync()
        {
            try
            {
                LogInfo("=== Manueller Chat-Reconnect gestartet ===");
                _reconnectAttempts = 0; // Reset für manuellen Reconnect
                return await ReconnectAsync();
            }
            catch (Exception ex)
            {
                await _discordService.SendErrorNotificationAsync("Manueller Reconnect Fehler", "TwitchChatService", ex);
                LogError($"Manueller Reconnect fehlgeschlagen: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ReconnectAsync()
        {
            if (_isReconnecting)
            {
                LogInfo("Reconnect bereits in Bearbeitung...");
                return false;
            }

            await _reconnectSemaphore.WaitAsync();
            try
            {
                _isReconnecting = true;

                if (_reconnectAttempts >= _maxReconnectAttempts)
                {
                    await _discordService.SendErrorNotificationAsync("Maximale Reconnect-Versuche erreicht", "TwitchChatService", null);
                    LogError($"Maximale Reconnect-Versuche erreicht ({_maxReconnectAttempts})");
                    return false;
                }

                _reconnectAttempts++;
                _lastConnectionAttempt = DateTime.UtcNow;
                LogInfo($"Reconnect-Versuch {_reconnectAttempts}/{_maxReconnectAttempts}...");

                var channelName = _configuration["Twitch:ChannelName"];
                var botUsername = _configuration["Twitch:BotUsername"];
                var chatToken = await _chatTokenService.GetChatTokenAsync();

                if (string.IsNullOrEmpty(chatToken))
                {
                    LogError("Kein Chat Token verfügbar für Reconnect");
                    return false;
                }

                await CreateAndConnectClient(botUsername!, chatToken, channelName!);

                if (IsConnected)
                {
                    LogSuccess("Reconnect erfolgreich!");
                    _reconnectAttempts = 0;
                    return true;
                }
                else
                {
                    await _discordService.SendErrorNotificationAsync("Reconnect Fehler", "TwitchChatService", null);
                    LogError("Reconnect fehlgeschlagen - Client nicht verbunden");

                    // Nächster Versuch mit exponential backoff
                    var delay = Math.Min(10000 * _reconnectAttempts, 60000); // Max 60 Sekunden
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(delay);
                        await ReconnectAsync();
                    });

                    return false;
                }
            }
            catch (Exception ex)
            {
                await _discordService.SendErrorNotificationAsync("Reconnect Fehler", "TwitchChatService", ex);
                LogError($"Reconnect-Fehler: {ex.Message}");

                // Nächster Versuch mit exponential backoff
                var delay = Math.Min(15000 * _reconnectAttempts, 120000); // Max 2 Minuten
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delay);
                    await ReconnectAsync();
                });

                return false;
            }
            finally
            {
                _isReconnecting = false;
                _reconnectSemaphore.Release();
            }
        }

        // Health Check Timer Callback
        private void HealthCheck(object? state)
        {
            try
            {
                if (!IsConnected && !_isReconnecting)
                {
                    LogInfo("Health Check: Verbindung verloren - starte Reconnect");
                    _ = Task.Run(ReconnectAsync);
                }
            }
            catch (Exception ex)
            {
                
                LogError($"Health Check Fehler: {ex.Message}");
            }
        }

        // Dispose Pattern für saubere Ressourcen-Freigabe
        public void Dispose()
        {
            try
            {
                _healthCheckTimer?.Dispose();
                _reconnectSemaphore?.Dispose();

                if (_client != null)
                {
                    _client.OnConnected -= OnConnected;
                    _client.OnJoinedChannel -= OnJoinedChannel;
                    _client.OnMessageReceived -= OnMessageReceived;
                    _client.OnDisconnected -= OnDisconnected;

                    if (_client.IsConnected)
                    {
                        _client.Disconnect();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Dispose Fehler: {ex.Message}");
            }
        }
    }
}

