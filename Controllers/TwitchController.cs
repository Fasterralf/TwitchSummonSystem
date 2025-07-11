using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using TwitchSummonSystem.Hubs;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TwitchController : ControllerBase
    {
        private readonly TwitchEventSubService _eventSubService;
        private readonly LotteryService _lotteryService; 
        private readonly IHubContext<SummonHub> _hubContext;

        public TwitchController(TwitchEventSubService eventSubService, LotteryService lotteryService, IHubContext<SummonHub> hubContext) 
        {
            _eventSubService = eventSubService;
            _lotteryService = lotteryService; 
            _hubContext = hubContext; 
        }

        [HttpPost("webhook")]
        public async Task<ActionResult> HandleWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                if (!VerifyWebhookSignature(body, GetMessageId()))
                {
                    return Unauthorized("Invalid signature");
                }

                Console.WriteLine($"?? Webhook received: {body}");

                var webhookData = JsonSerializer.Deserialize<JsonElement>(body);
                
                if (webhookData.TryGetProperty("challenge", out var challenge))
                {
                    Console.WriteLine("?? Webhook challenge received");
                    return Ok(challenge.GetString());
                }

                if (webhookData.TryGetProperty("event", out var eventData))
                {
                    var eventType = webhookData.GetProperty("subscription").GetProperty("type").GetString();
                    
                    if (eventType == "channel.channel_points_custom_reward_redemption.add")
                    {
                        var result = await _eventSubService.HandleChannelPointRedemption(eventData);
                        
                        if (result != null)
                        {
                            return Ok(new { success = true, result });
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Webhook error: {ex.Message}");
                return StatusCode(500);
            }
        }

        [HttpPost("setup-webhook")]
        public async Task<ActionResult> SetupWebhook([FromBody] SetupWebhookRequest request)
        {
            var success = await _eventSubService.CreateChannelPointRewardSubscription(request.CallbackUrl);
            
            if (success)
            {
                return Ok(new { message = "Webhook erfolgreich eingerichtet" });
            }
            
            return BadRequest(new { message = "Webhook Setup fehlgeschlagen" });
        }

        [HttpPost("simulate-reward")]
        public async Task<ActionResult> SimulateReward([FromBody] SimulateRewardRequest request)
        {
            try
            {
                var result = _lotteryService.PerformSummon(request.Username ?? "TestUser");

                await _hubContext.Clients.All.SendAsync("SummonResult", result);

                Console.WriteLine($"?? Simuliert - {request.Username}: {(result.IsGold ? "? GOLD!" : "? No gold")} - Pity: {result.PityCount}/80");

                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Simulation error: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("simulate-force-gold")]
        public async Task<ActionResult> SimulateForceGold([FromBody] SimulateRewardRequest request)
        {
            try
            {
                var result = _lotteryService.PerformForceGoldSummon(request.Username ?? "ForceGoldTest");

                await _hubContext.Clients.All.SendAsync("SummonResult", result);

                Console.WriteLine($"? FORCE GOLD - {request.Username}: GOLD GUARANTEED!");

                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Force gold error: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("update-reward-name")]
        public async Task<IActionResult> UpdateRewardName([FromBody] UpdateRewardNameRequest request)
        {
            try
            {
                var success = await _eventSubService.UpdateSummonRewardNameAsync(request.RewardName);
                if (success)
                {
                    return Ok(new { success = true, message = $"Reward Name auf '{request.RewardName}' ge�ndert" });
                }
                return BadRequest(new { success = false, error = "Reward nicht gefunden" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("current-reward-name")]
        public IActionResult GetCurrentRewardName()
        {
            var currentName = _eventSubService.GetCurrentSummonRewardName();
            return Ok(new { rewardName = currentName });
        }

        [HttpGet("available-rewards")]
        public async Task<IActionResult> GetAvailableRewards()
        {
            try
            {
                var rewards = await _eventSubService.GetAllRewardsAsync();
                return Ok(new { rewards });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        public class UpdateRewardNameRequest
        {
            public string RewardName { get; set; } = null!;
        }

        private string? GetMessageId()
        {
            return Request.Headers["Twitch-Eventsub-Message-Id"].FirstOrDefault();
        }

        private bool VerifyWebhookSignature(string body, string? messageId)
        {
            if (body is null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            var signature = Request.Headers["Twitch-Eventsub-Message-Signature"].FirstOrDefault();
            _ = Request.Headers["Twitch-Eventsub-Message-Timestamp"].FirstOrDefault();
            return true; 
        }
    }

    public class SetupWebhookRequest
    {
        public string CallbackUrl { get; set; } = null!;
    }

    public class SimulateRewardRequest
    {
        public string Username { get; set; } = null!;
    }
}
