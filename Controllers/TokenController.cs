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
                return Ok(status); // ← Einfach das direkt zurückgeben
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
                var success = await _tokenService.ForceRefreshTokensAsync(); // ← Neue Methode verwenden

                if (success)
                {
                    return Ok(new { message = "Token erfolgreich erneuert" });
                }
                else
                {
                    return StatusCode(500, new { error = "Token-Refresh fehlgeschlagen" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

    }
}
