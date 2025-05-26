using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly LotteryService _lotteryService; // GEÄNDERT

        public ChatController(LotteryService lotteryService) // GEÄNDERT
        {
            _lotteryService = lotteryService; // GEÄNDERT
        }

        [HttpGet("pity")]
        public ActionResult<string> GetPityCommand()
        {
            var lotteryData = _lotteryService.GetLotteryData(); // GEÄNDERT
            var currentPity = _lotteryService.GetCurrentPity(); // GEÄNDERT
            return Ok($"Aktueller Pity Count: {currentPity}/80 | Verbleibende Kugeln: {lotteryData.TotalBalls}");
        }

        [HttpPost("pity/reset")]
        public ActionResult<string> ResetPityCommand()
        {
            _lotteryService.ResetLottery(); // GEÄNDERT
            return Ok("Lottery wurde auf 0 zurückgesetzt!");
        }

        [HttpGet("stats")]
        public ActionResult<string> GetStatsCommand()
        {
            var lotteryData = _lotteryService.GetLotteryData(); // GEÄNDERT
            var goldRate = lotteryData.TotalSummons > 0 ?
                (double)lotteryData.TotalGolds / lotteryData.TotalSummons * 100 : 0;
            var currentPity = _lotteryService.GetCurrentPity(); // GEÄNDERT
            var goldChance = _lotteryService.CalculateGoldChance() * 100; // GEÄNDERT

            return Ok($"📊 Stats: {lotteryData.TotalSummons} Summons | {lotteryData.TotalGolds} Golds | {goldRate:F1}% Rate | Pity: {currentPity}/80 | Gold Chance: {goldChance:F1}%");
        }
    }
}
