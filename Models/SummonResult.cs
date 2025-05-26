namespace TwitchSummonSystem.Models
{
    public class SummonResult
    {
        public bool IsGold { get; set; }
        public int PityCount { get; set; }
        public double GoldChance { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Username { get; set; }
    }
}
