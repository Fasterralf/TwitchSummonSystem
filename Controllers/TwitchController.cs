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
        private readonly LotteryService _lotteryService; // GEÄNDERT
        private readonly IHubContext<SummonHub> _hubContext; // NEU


        public TwitchController(TwitchEventSubService eventSubService, LotteryService lotteryService, IHubContext<SummonHub> hubContext) // GEÄNDERT
        {
            _eventSubService = eventSubService;
            _lotteryService = lotteryService; // GEÄNDERT
            _hubContext = hubContext; // NEU
        }

        [HttpPost("webhook")]
        public async Task<ActionResult> HandleWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                if (!VerifyWebhookSignature(body))
                {
                    return Unauthorized("Invalid signature");
                }

                Console.WriteLine($"📨 Webhook erhalten: {body}");

                var webhookData = JsonSerializer.Deserialize<JsonElement>(body);
                
                // Challenge Response für Webhook Verification
                if (webhookData.TryGetProperty("challenge", out var challenge))
                {
                    Console.WriteLine("🔐 Webhook Challenge erhalten");
                    return Ok(challenge.GetString());
                }

                // Event Data verarbeiten
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
                Console.WriteLine($"❌ Webhook Fehler: {ex.Message}");
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
                // Direkt den PityService aufrufen (ohne EventSub)
                var result = _lotteryService.PerformSummon(request.Username ?? "TestUser");

                // Live-Update an OBS senden
                await _hubContext.Clients.All.SendAsync("SummonResult", result);

                Console.WriteLine($"🎲 Simuliert - {request.Username}: {(result.IsGold ? "⭐ GOLD!" : "❌ Kein Gold")} - Pity: {result.PityCount}/80");

                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Simulation Fehler: {ex.Message}");
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

                Console.WriteLine($"⭐ FORCE GOLD - {request.Username}: GOLD GARANTIERT!");

                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Force Gold Fehler: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("update-reward-title")]
        public async Task<ActionResult> UpdateRewardTitle([FromBody] UpdateRewardTitleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Title))
                {
                    return BadRequest(new { error = "Titel darf nicht leer sein" });
                }

                if (request.Title.Length > 45)
                {
                    return BadRequest(new { error = "Titel zu lang (max. 45 Zeichen)" });
                }

                var success = await _eventSubService.UpdateRewardTitleAsync(request.Title);

                if (success)
                {
                    return Ok(new { success = true, message = $"Reward Titel aktualisiert: {request.Title}" });
                }
                else
                {
                    return StatusCode(500, new { error = "Fehler beim Aktualisieren des Reward Titels" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Update Reward Title Fehler: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("current-reward-title")]
        public ActionResult GetCurrentRewardTitle()
        {
            try
            {
                var title = _eventSubService.GetCurrentRewardTitle();
                return Ok(new { title });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("available-rewards")]
        public async Task<ActionResult> GetAvailableRewards()
        {
            try
            {
                var rewards = await _eventSubService.GetAllRewardsAsync();
                return Ok(rewards);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Get Available Rewards Fehler: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }



        private bool VerifyWebhookSignature(string body)
        {
            var signature = Request.Headers["Twitch-Eventsub-Message-Signature"].FirstOrDefault();
            var timestamp = Request.Headers["Twitch-Eventsub-Message-Timestamp"].FirstOrDefault();
            var messageId = Request.Headers["Twitch-Eventsub-Message-Id"].FirstOrDefault();

            // Implement HMAC-SHA256 verification here
            return true; // Simplified for now
        }
    }

    public class SetupWebhookRequest
    {
        public string CallbackUrl { get; set; }
    }

    public class SimulateRewardRequest
    {
        public string Username { get; set; }
    }

    public class UpdateRewardTitleRequest
    {
        public string Title { get; set; }
    }
}
