using Microsoft.AspNetCore.Mvc;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ConfigController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("status")]
        public ActionResult GetStatus()
        {
            try
            {
                // Echte Werte aus der geladenen Konfiguration (inklusive Environment Variables)
                var twitchConfig = new
                {
                    ChannelName = _configuration["Twitch:ChannelName"] ?? "nicht gesetzt",
                    BotUsername = _configuration["Twitch:BotUsername"] ?? "nicht gesetzt", 
                    SummonRewardName = _configuration["Twitch:SummonRewardName"] ?? "nicht gesetzt",
                    HasChatToken = !string.IsNullOrEmpty(_configuration["Twitch:ChatOAuthToken"]),
                    HasClientId = !string.IsNullOrEmpty(_configuration["Twitch:ClientId"]),
                    HasAccessToken = !string.IsNullOrEmpty(_configuration["Twitch:AccessToken"])
                };

                var discordConfig = new
                {
                    HasWebhookUrl = !string.IsNullOrEmpty(_configuration["Discord:WebhookUrl"]),
                    HasErrorWebhookUrl = !string.IsNullOrEmpty(_configuration["Discord:ErrorWebhookUrl"])
                };

                return Ok(new 
                { 
                    twitch = twitchConfig,
                    discord = discordConfig,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Fehler beim Abrufen der Konfiguration", message = ex.Message });
            }
        }
    }
}
