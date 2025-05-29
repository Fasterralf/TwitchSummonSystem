using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;

namespace TwitchSummonSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TokenController : ControllerBase
    {
        private readonly TokenService _tokenService;

        public TokenController(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        [HttpGet("status")]
        public async Task<ActionResult> GetTokenStatus()
        {
            try
            {
                var status = await _tokenService.GetTokenStatusAsync();
                return Ok(new
                {
                    userToken = new
                    {
                        valid = status.UserTokenValid,
                        expiresAt = status.UserTokenExpiry,
                        daysUntilExpiry = (status.UserTokenExpiry - DateTime.Now).TotalDays
                    },
                    appToken = new
                    {
                        expiresAt = status.AppTokenExpiry,
                        daysUntilExpiry = (status.AppTokenExpiry - DateTime.Now).TotalDays
                    },
                    lastCheck = status.LastCheck
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult> ForceRefreshTokens()
        {
            try
            {
                var newUserToken = await _tokenService.GetUserAccessTokenAsync();
                var newAppToken = await _tokenService.GetAppAccessTokenAsync();

                return Ok(new
                {
                    message = "Token erfolgreich erneuert",
                    userTokenUpdated = !string.IsNullOrEmpty(newUserToken),
                    appTokenUpdated = !string.IsNullOrEmpty(newAppToken)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
