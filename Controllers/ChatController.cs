using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly LotteryService _lotteryService;
        public ChatController(LotteryService lotteryService) => _lotteryService = lotteryService;

        [HttpGet("pity")]
        public ActionResult<string> GetPityCommand()
        {
            var lotteryData = _lotteryService.GetLotteryData();
            var goldChance = _lotteryService.CalculateGoldChance() * 100;

            var summonsSinceLastGold = lotteryData.SummonsSinceLastGold;
            string nextBonusText = "";

            if (summonsSinceLastGold < 100)
            {
                nextBonusText = $" | Nächster Bonus in {100 - summonsSinceLastGold} Summons";
            }
            else
            {
                var bonusRounds = (summonsSinceLastGold - 100) / 10;
                var nextBonusAt = (bonusRounds + 1) * 10 + 100;
                nextBonusText = $" | Nächster Bonus in {nextBonusAt - summonsSinceLastGold} Summons";
            }

            return Ok($"🎯 Gold Chance: {goldChance:F1}% | Summons: {lotteryData.TotalSummons} | Golds: {lotteryData.TotalGolds}{nextBonusText}");
        }

        [HttpPost("pity/reset")]
        public ActionResult<string> ResetPityCommand()
        {
            _lotteryService.ResetLottery();
            return Ok("🔄 Lottery wurde zurückgesetzt! Nächster Bonus in 100 Summons.");
        }

        [HttpGet("stats")]
        public ActionResult<string> GetStatsCommand()
        {
            var lotteryData = _lotteryService.GetLotteryData();
            var goldRate = lotteryData.TotalSummons > 0 ?
                (double)lotteryData.TotalGolds / lotteryData.TotalSummons * 100 : 0;
            var goldChance = _lotteryService.CalculateGoldChance() * 100;

            var summonsSinceLastGold = lotteryData.SummonsSinceLastGold;
            string nextBonusText = "";

            if (summonsSinceLastGold < 100)
            {
                nextBonusText = $" | Bonus in {100 - summonsSinceLastGold}";
            }
            else
            {
                var bonusRounds = (summonsSinceLastGold - 100) / 10;
                var nextBonusAt = (bonusRounds + 1) * 10 + 100;
                nextBonusText = $" | Bonus in {nextBonusAt - summonsSinceLastGold}";
            }

            return Ok($"📊 {lotteryData.TotalSummons} Summons | {lotteryData.TotalGolds} Golds | {goldRate:F1}% Rate | {goldChance:F1}% Chance{nextBonusText}");
        }
    }
}
