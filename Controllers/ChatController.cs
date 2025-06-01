using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly LotteryService _lotteryService;
        private readonly TwitchChatService _chatService;
        private readonly ChatTokenService _chatTokenService;

        public ChatController(LotteryService lotteryService, TwitchChatService chatService, ChatTokenService chatTokenService)
        {
            _lotteryService = lotteryService;
            _chatService = chatService;  // ← Hinzufügen
            _chatTokenService = chatTokenService;  // ← Hinzufügen
        }

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

        [HttpGet("status")]
        public async Task<IActionResult> GetChatStatus()
        {
            var status = await _chatService.GetChatStatusAsync();
            return Ok(status);
        }

        [HttpPost("reconnect")]
        public async Task<IActionResult> ForceReconnect()
        {
            var success = await _chatService.ForceReconnectAsync();
            return Ok(new { success, message = success ? "Reconnect erfolgreich" : "Reconnect fehlgeschlagen" });
        }
    }
}
