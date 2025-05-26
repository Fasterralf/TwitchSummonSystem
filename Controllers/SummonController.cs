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
        private readonly PityService _pityService;
        private readonly IHubContext<SummonHub> _hubContext;

        public SummonController(PityService pityService, IHubContext<SummonHub> hubContext)
        {
            _pityService = pityService;
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
            var result = _pityService.PerformSummon(request.Username);

            // Live-Update an OBS senden
            await _hubContext.Clients.All.SendAsync("SummonResult", result);

            return Ok(result);
        }

        [HttpGet("pity")]
        public ActionResult<PityData> GetPityData()
        {
            var pityData = _pityService.GetPityData();
            return Ok(pityData);
        }

        [HttpPost("pity/reset")]
        public async Task<ActionResult> ResetPity()
        {
            _pityService.ResetPity();
            var pityData = _pityService.GetPityData();

            // Live-Update an OBS senden
            await _hubContext.Clients.All.SendAsync("PityReset", pityData);

            return Ok(new { message = "Pity wurde zurückgesetzt", pityData });
        }

        [HttpGet("stats")]
        public ActionResult GetStats()
        {
            var pityData = _pityService.GetPityData();

            var stats = new
            {
                CurrentPity = pityData.CurrentPity,
                TotalSummons = pityData.TotalSummons,
                TotalGolds = pityData.TotalGolds,
                GoldRate = pityData.TotalSummons > 0 ? (double)pityData.TotalGolds / pityData.TotalSummons * 100 : 0,
                LastSummon = pityData.LastSummon
            };

            return Ok(stats);
        }
    }

    public class SummonRequest
    {
        public string? Username { get; set; }
    }
}
