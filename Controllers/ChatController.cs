using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly PityService _pityService;

        public ChatController(PityService pityService)
        {
            _pityService = pityService;
        }

        [HttpGet("pity")]
        public ActionResult<string> GetPityCommand()
        {
            var pityData = _pityService.GetPityData();
            return Ok($"Aktueller Pity Count: {pityData.CurrentPity}/80");
        }

        [HttpPost("pity/reset")]
        public ActionResult<string> ResetPityCommand()
        {
            _pityService.ResetPity();
            return Ok("Pity wurde auf 0 zurückgesetzt!");
        }

        [HttpGet("stats")]
        public ActionResult<string> GetStatsCommand()
        {
            var pityData = _pityService.GetPityData();
            var goldRate = pityData.TotalSummons > 0 ?
                (double)pityData.TotalGolds / pityData.TotalSummons * 100 : 0;

            return Ok($"📊 Stats: {pityData.TotalSummons} Summons | {pityData.TotalGolds} Golds | {goldRate:F1}% Rate | Pity: {pityData.CurrentPity}/80");
        }
    }
}
