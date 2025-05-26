using TwitchSummonSystem.Models;
using Newtonsoft.Json;

namespace TwitchSummonSystem.Services
{
    public class PityService
    {
        private readonly string _dataFilePath = "pity_data.json";
        private PityData _pityData;
        private readonly SummonSettings _settings;

        public PityService()
        {
            _settings = new SummonSettings();
            LoadPityData();
        }

        public PityData GetPityData()
        {
            return _pityData;
        }

        // HINZUGEFÜGT: Fehlende GetCurrentPity Methode
        public int GetCurrentPity()
        {
            return _pityData.CurrentPity;
        }

        public SummonResult PerformSummon(string username)
        {
            // Berechne Gold-Chance
            double goldChance = CalculateGoldChance();

            // Würfeln
            Random random = new Random();
            bool isGold = random.NextDouble() < goldChance;

            // Erstelle Ergebnis
            var result = new SummonResult
            {
                IsGold = isGold,
                PityCount = _pityData.CurrentPity,
                GoldChance = goldChance * 100, // In Prozent
                Timestamp = DateTime.Now,
                Username = username
            };

            // Update Pity
            if (isGold)
            {
                _pityData.CurrentPity = 0; // Reset bei Gold
                _pityData.TotalGolds++;
            }
            else
            {
                _pityData.CurrentPity++; // Erhöhe Pity
            }

            _pityData.TotalSummons++;
            _pityData.LastSummon = DateTime.Now;
            SavePityData();

            return result;
        }

        public void ResetPity()
        {
            _pityData.CurrentPity = 0;
            SavePityData();
        }

        // GEÄNDERT: Überladung hinzugefügt für Kompatibilität
        public double CalculateGoldChance()
        {
            return CalculateGoldChance(_pityData.CurrentPity);
        }

        public double CalculateGoldChance(int pityCount)
        {
            // Hard Pity: Bei 80 = 100%
            if (pityCount >= _settings.HardPityLimit)
            {
                return 1.0; // 100%
            }

            // Soft Pity ab 74 (wie in Genshin Impact)
            if (pityCount >= 74)
            {
                double softPityBonus = (pityCount - 73) * 0.06; // 6% pro Pull ab 74
                return Math.Min(_settings.BaseGoldChance + (pityCount * _settings.PityMultiplier) + softPityBonus, 1.0);
            }

            // Base Chance + Pity Bonus
            double chance = _settings.BaseGoldChance + (pityCount * _settings.PityMultiplier);
            return Math.Min(chance, 1.0);
        }


        private void LoadPityData()
        {
            if (File.Exists(_dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_dataFilePath);
                    _pityData = JsonConvert.DeserializeObject<PityData>(json) ?? new PityData();
                }
                catch
                {
                    _pityData = new PityData();
                }
            }
            else
            {
                _pityData = new PityData();
            }
        }

        private void SavePityData()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_pityData, Formatting.Indented);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Speichern: {ex.Message}");
            }
        }
    }
}
