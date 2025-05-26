namespace TwitchSummonSystem.Models
{
    public class SummonSettings
    {
        public double BaseGoldChance { get; set; } = 0.01f; // 1%
        public int HardPityLimit { get; set; } = 80;
        public double PityMultiplier { get; set; } = 0.005; // 0.5% pro pity
    }
}
