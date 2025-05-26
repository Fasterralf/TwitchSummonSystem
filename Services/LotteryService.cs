using TwitchSummonSystem.Models;
using Newtonsoft.Json;

namespace TwitchSummonSystem.Services
{
    public class LotteryService
    {
        private readonly string _dataFilePath = "lottery_data.json";
        private LotteryData _lotteryData;

        public LotteryService()
        {
            LoadLotteryData();
        }

        public LotteryData GetLotteryData()
        {
            return _lotteryData;
        }

        public int GetCurrentPity()
        {
            return 80 - _lotteryData.TotalBalls; // Wie viele Kugeln schon gezogen
        }

        public SummonResult PerformSummon(string username)
        {
            // Berechne aktuelle Chance
            double goldChance = (double)_lotteryData.GoldBalls / _lotteryData.TotalBalls;

            // Ziehe zufällige Kugel
            Random random = new Random();
            bool isGold = random.NextDouble() < goldChance;

            var result = new SummonResult
            {
                IsGold = isGold,
                PityCount = GetCurrentPity(),
                GoldChance = goldChance * 100,
                Timestamp = DateTime.Now,
                Username = username
            };

            if (isGold)
            {
                // Gold gezogen - Reset das System
                _lotteryData.GoldBalls = 1;
                _lotteryData.LoseBalls = 79;
                _lotteryData.TotalBalls = 80;
                _lotteryData.TotalGolds++;
            }
            else
            {
                // Lose gezogen - entferne eine Lose-Kugel
                _lotteryData.LoseBalls--;
                _lotteryData.TotalBalls--;

                // Sicherheit: Wenn nur noch Gold übrig
                if (_lotteryData.LoseBalls <= 0)
                {
                    result.IsGold = true;
                    _lotteryData.GoldBalls = 1;
                    _lotteryData.LoseBalls = 79;
                    _lotteryData.TotalBalls = 80;
                    _lotteryData.TotalGolds++;
                }
            }

            _lotteryData.TotalSummons++;
            _lotteryData.LastSummon = DateTime.Now;
            SaveLotteryData();

            return result;
        }

        public void ResetLottery()
        {
            _lotteryData.GoldBalls = 1;
            _lotteryData.LoseBalls = 79;
            _lotteryData.TotalBalls = 80;
            SaveLotteryData();
        }

        public double CalculateGoldChance()
        {
            return (double)_lotteryData.GoldBalls / _lotteryData.TotalBalls;
        }

        private void LoadLotteryData()
        {
            if (File.Exists(_dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_dataFilePath);
                    _lotteryData = JsonConvert.DeserializeObject<LotteryData>(json) ?? new LotteryData();
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
