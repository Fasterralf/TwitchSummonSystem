using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly LotteryService _lotteryService;

        public ChatController(LotteryService lotteryService) 
        {
            _lotteryService = lotteryService; 
        }

        [HttpGet("pity")]
        public ActionResult<string> GetPityCommand()
        {
            var lotteryData = _lotteryService.GetLotteryData();
            var goldChance = _lotteryService.CalculateGoldChance() * 100;
            return Ok($"Aktuelle Gold Chance: {goldChance:F1}% | Summons: {lotteryData.TotalSummons} | Golds: {lotteryData.TotalGolds}");
        }

        [HttpPost("pity/reset")]
        public ActionResult<string> ResetPityCommand()
        {
            _lotteryService.ResetLottery();
            return Ok("Lottery wurde auf 0 zurückgesetzt!");
        }

        [HttpGet("stats")]
        public ActionResult<string> GetStatsCommand()
        {
            var lotteryData = _lotteryService.GetLotteryData();
            var goldRate = lotteryData.TotalSummons > 0 ?
                (double)lotteryData.TotalGolds / lotteryData.TotalSummons * 100 : 0;
            var goldChance = _lotteryService.CalculateGoldChance() * 100;
            return Ok($"📊 Stats: {lotteryData.TotalSummons} Summons | {lotteryData.TotalGolds} Golds | {goldRate:F1}% Rate | Gold Chance: {goldChance:F1}%");
        }

    }
}
