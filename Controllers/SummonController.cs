using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Models;
using TwitchSummonSystem.Services;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SummonController : ControllerBase
    {
        private readonly LotteryService _lotteryService; // GEÄNDERT
        private readonly IHubContext<SummonHub> _hubContext;

        public SummonController(LotteryService lotteryService, IHubContext<SummonHub> hubContext) // GEÄNDERT
        {
            _lotteryService = lotteryService; // GEÄNDERT
            _hubContext = hubContext;
        }

        [HttpPost("perform")]
        public async Task<ActionResult<SummonResult>> PerformSummon([FromBody] SummonRequest request)
        {
            if (string.IsNullOrEmpty(request.Username))
            {
                return BadRequest("Username ist erforderlich");
            }

            // Summon durchführen
            var result = _lotteryService.PerformSummon(request.Username); // GEÄNDERT

            // Live-Update an OBS senden
            await _hubContext.Clients.All.SendAsync("SummonResult", result);

            return Ok(result);
        }

        [HttpGet("pity")]
        public ActionResult<LotteryData> GetPityData() // GEÄNDERT Return Type
        {
            var lotteryData = _lotteryService.GetLotteryData(); // GEÄNDERT
            return Ok(lotteryData);
        }

        [HttpPost("pity/reset")]
        public async Task<ActionResult> ResetPity()
        {
            _lotteryService.ResetLottery(); // GEÄNDERT
            var lotteryData = _lotteryService.GetLotteryData(); // GEÄNDERT

            // Live-Update an OBS senden
            await _hubContext.Clients.All.SendAsync("PityReset", lotteryData);

            return Ok(new { message = "Lottery wurde zurückgesetzt", lotteryData }); // GEÄNDERT
        }

        [HttpGet("stats")]
        public ActionResult GetStats()
        {
            var lotteryData = _lotteryService.GetLotteryData(); // GEÄNDERT
            var stats = new
            {
                CurrentPity = _lotteryService.GetCurrentPity(), // GEÄNDERT
                TotalSummons = lotteryData.TotalSummons,
                TotalGolds = lotteryData.TotalGolds,
                GoldRate = lotteryData.TotalSummons > 0 ? (double)lotteryData.TotalGolds / lotteryData.TotalSummons * 100 : 0,
                LastSummon = lotteryData.LastSummon,
                RemainingBalls = lotteryData.TotalBalls, // NEU
                GoldChance = _lotteryService.CalculateGoldChance() * 100 // NEU
            };

            return Ok(stats);
        }
    }

    public class SummonRequest
    {
        public string? Username { get; set; }
    }
}
