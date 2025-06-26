using TwitchSummonSystem.Models;
using Newtonsoft.Json;
using Microsoft.AspNetCore.SignalR;
using TwitchSummonSystem.Hubs;
using System.Data.SqlTypes;

namespace TwitchSummonSystem.Services
{
    public class LotteryService
    {
        private readonly string _dataFilePath = "lottery_data.json";
        private readonly string _backupDirectory = "lottery_backups";
        private readonly IHubContext<SummonHub> _hubContext;
        private LotteryData _lotteryData = null!;
        private readonly DiscordService _discordService;
        private readonly object _lockObject = new();
        private readonly Timer _backupTimer;

        public LotteryService(IHubContext<SummonHub> hubContext, DiscordService discordService)
        {
            _hubContext = hubContext;
            _discordService = discordService;
            
            // Backup Directory erstellen
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }
            
            LoadLotteryData();
            
            // Backup Timer - alle 30 Minuten
            _backupTimer = new Timer(CreateBackup, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        public LotteryData GetLotteryData()
        {
            return _lotteryData;
        }

        public SummonResult PerformSummon(string username)
        {
            lock (_lockObject)
            {
                CalculateCurrentGoldChance();

                Random random = new();
                double roll = random.NextDouble() * 100; 
                bool isGold = roll < _lotteryData.CurrentGoldChance;

                var result = new SummonResult
                {
                    IsGold = isGold,
                    PityCount = _lotteryData.SummonsSinceLastGold,
                    GoldChance = _lotteryData.CurrentGoldChance,
                    Timestamp = DateTime.Now,
                    Username = username
                };

                _lotteryData.TotalSummons++;
                _lotteryData.SummonsSinceLastGold++;

                if (isGold)
                {
                    _lotteryData.TotalGolds++;
                    _lotteryData.CurrentGoldChance = _lotteryData.BaseGoldChance;
                    _lotteryData.SummonsSinceLastGold = 0;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _discordService.SendGoldNotificationAsync(
                                username,
                                _lotteryData.CurrentGoldChance,
                                _lotteryData.TotalSummons,
                                _lotteryData.TotalGolds
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Fehler beim Senden der Benachrichtigung an Discord: {ex.Message}");
                        }
                    });
                }

                _lotteryData.LastSummon = DateTime.Now;
                SaveLotteryData();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _hubContext.Clients.All.SendAsync("SummonResult", result);
                        await _hubContext.Clients.All.SendAsync("LotteryUpdate", _lotteryData);
                        Console.WriteLine($"📡 SignalR Events gesendet: Summon + LotteryUpdate");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ SignalR Fehler: {ex.Message}");
                    }
                });

                return result;
            }
        }

        public SummonResult PerformForceGoldSummon(string username)
        {
            CalculateCurrentGoldChance();

            var result = new SummonResult
            {
                IsGold = true, 
                PityCount = _lotteryData.SummonsSinceLastGold,
                GoldChance = _lotteryData.CurrentGoldChance,
                Timestamp = DateTime.Now,
                Username = username
            };

            _lotteryData.TotalSummons++;
            _lotteryData.TotalGolds++;
            _lotteryData.CurrentGoldChance = _lotteryData.BaseGoldChance;
            _lotteryData.SummonsSinceLastGold = 0; 

            _ = Task.Run(async () =>
            {
                try
                {
                    await _discordService.SendGoldNotificationAsync(
                        username,
                        _lotteryData.CurrentGoldChance,
                        _lotteryData.TotalSummons,
                        _lotteryData.TotalGolds
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Fehler beim Senden der Benachrichtigung an Discord: {ex.Message}");
                }
            });

            _lotteryData.LastSummon = DateTime.Now;
            SaveLotteryData();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _hubContext.Clients.All.SendAsync("SummonResult", result);
                    await _hubContext.Clients.All.SendAsync("LotteryUpdate", _lotteryData);
                    Console.WriteLine($"📡 SignalR Events gesendet: Force Gold Summon");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ SignalR Fehler: {ex.Message}");
                }
            });

            return result;
        }

        private void CalculateCurrentGoldChance()
        {
            double chance = _lotteryData.BaseGoldChance; 

            if (_lotteryData.SummonsSinceLastGold >= 100)
            {
                chance += 1.0;
                int additionalBlocks = (_lotteryData.SummonsSinceLastGold - 100) / 10;
                chance += additionalBlocks * 1.0;
            }

            _lotteryData.CurrentGoldChance = chance;
        }

        public void ResetLottery()
        {
            _lotteryData.BaseGoldChance = 0.8;
            _lotteryData.CurrentGoldChance = 0.8;
            _lotteryData.TotalSummons = 0;
            _lotteryData.TotalGolds = 0;
            _lotteryData.SummonsSinceLastGold = 0;
            SaveLotteryData();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _hubContext.Clients.All.SendAsync("PityReset", _lotteryData);
                    await _hubContext.Clients.All.SendAsync("LotteryUpdate", _lotteryData);
                    Console.WriteLine($"📡 SignalR Events gesendet: PityReset + LotteryUpdate");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ SignalR Fehler: {ex.Message}");
                }
            });
        }

        public double CalculateGoldChance()
        {
            CalculateCurrentGoldChance();
            return _lotteryData.CurrentGoldChance / 100.0; 
        }

        private void LoadLotteryData()
        {
            if (File.Exists(_dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_dataFilePath);
                    _lotteryData = JsonConvert.DeserializeObject<LotteryData>(json) ?? new LotteryData();
                    if (_lotteryData.BaseGoldChance == 0)
                    {
                        _lotteryData.BaseGoldChance = 0.8;
                        _lotteryData.CurrentGoldChance = 0.8;
                    }
                }
                catch (Exception ex) // ERWEITERN von catch zu catch (Exception ex)
                {
                    _lotteryData = new LotteryData();
                    // NEU HINZUFÜGEN:
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _discordService.SendErrorNotificationAsync("Fehler beim Laden der Lottery-Daten - verwende Defaults", "LotteryService", ex);
                        }
                        catch
                        {
                            // Ignore Discord errors
                        }
                    });
                }
            }
            else
            {
                _lotteryData = new LotteryData();
            }
        }


        private void SaveLotteryData()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_lotteryData, Formatting.Indented);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Speichern: {ex.Message}");
                // NEU HINZUFÜGEN:
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _discordService.SendErrorNotificationAsync("Kritischer Fehler beim Speichern der Lottery-Daten!", "LotteryService", ex);
                    }
                    catch
                    {
                        // Ignore Discord errors
                    }
                });
            }
        }

        private void CreateBackup(object? state)
        {
            try
            {
                lock (_lockObject)
                {
                    var backupFileName = $"lottery_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    var backupPath = Path.Combine(_backupDirectory, backupFileName);
                    
                    var json = JsonConvert.SerializeObject(_lotteryData, Formatting.Indented);
                    File.WriteAllText(backupPath, json);
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 💾 Backup erstellt: {backupFileName}");
                    
                    // Alte Backups löschen (behalte nur die letzten 10)
                    CleanupOldBackups();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Backup Fehler: {ex.Message}");
            }
        }

        private void CleanupOldBackups()
        {
            try
            {
                var backupFiles = Directory.GetFiles(_backupDirectory, "lottery_backup_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(10); // Behalte die neuesten 10

                foreach (var file in backupFiles)
                {
                    file.Delete();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🗑️ Altes Backup gelöscht: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ Backup Cleanup Fehler: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _backupTimer?.Dispose();
        }
    }
}
