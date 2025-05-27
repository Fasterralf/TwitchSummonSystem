using TwitchSummonSystem.Models;
using Newtonsoft.Json;
using Microsoft.AspNetCore.SignalR;
using TwitchSummonSystem.Hubs;

namespace TwitchSummonSystem.Services
{
    public class LotteryService
    {
        private readonly string _dataFilePath = "lottery_data.json";
        private readonly IHubContext<SummonHub> _hubContext;
        private LotteryData _lotteryData;

        public LotteryService(IHubContext<SummonHub> hubContext)
        {
            _hubContext = hubContext;
            LoadLotteryData();
        }

        public LotteryData GetLotteryData()
        {
            return _lotteryData;
        }

        public SummonResult PerformSummon(string username)
        {
            // Aktuelle Chance berechnen
            CalculateCurrentGoldChance();

            // Zufallszahl generieren
            Random random = new Random();
            double roll = random.NextDouble() * 100; // 0-100%
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
            return _lotteryData.CurrentGoldChance / 100.0; // Als Dezimalwert zurückgeben
        }

        private void LoadLotteryData()
        {
            if (File.Exists(_dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_dataFilePath);
                    _lotteryData = JsonConvert.DeserializeObject<LotteryData>(json) ?? new LotteryData();

                    // Migration: Falls alte Daten vorhanden sind
                    if (_lotteryData.BaseGoldChance == 0)
                    {
                        _lotteryData.BaseGoldChance = 0.8;
                        _lotteryData.CurrentGoldChance = 0.8;
                    }
                }
                catch
                {
                    _lotteryData = new LotteryData();
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
            }
        }
    }
}
