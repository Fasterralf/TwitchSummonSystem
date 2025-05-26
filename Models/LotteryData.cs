namespace TwitchSummonSystem.Models
{
    public class LotteryData
    {
        public int TotalBalls { get; set; } = 80;
        public int GoldBalls { get; set; } = 1;
        public int LoseBalls { get; set; } = 79;
        public int TotalSummons { get; set; } = 0;
        public int TotalGolds { get; set; } = 0;
        public DateTime LastSummon { get; set; } = DateTime.Now;
    }
}
