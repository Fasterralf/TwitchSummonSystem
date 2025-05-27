namespace TwitchSummonSystem.Models
{
    public class LotteryData
    {
        public double BaseGoldChance { get; set; } = 0.8;
        public double CurrentGoldChance { get; set; } = 0.8;
        public int TotalSummons { get; set; } = 0; 
        public int TotalGolds { get; set; } = 0;
        public int SummonsSinceLastGold { get; set; } = 0;
        public DateTime LastSummon { get; set; } = DateTime.Now;
    }
}
