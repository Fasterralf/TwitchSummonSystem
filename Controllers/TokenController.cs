using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly ChatTokenService _chatTokenService; 

    public TokenController(TokenService tokenService, ChatTokenService chatTokenService) 
    {
        _tokenService = tokenService;
        _chatTokenService = chatTokenService; 
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetTokenStatus()
    {
        try
        {
            var userAppStatus = await _tokenService.GetTokenStatusAsync();
            var chatStatus = await _chatTokenService.GetChatTokenStatusAsync(); 

            var combinedStatus = new
            {
                userToken = ((dynamic)userAppStatus).userToken,
                appToken = ((dynamic)userAppStatus).appToken,
                chatToken = chatStatus, 
                lastCheck = DateTime.UtcNow
            };

            return Ok(combinedStatus);
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
            var userAppSuccess = await _tokenService.ForceRefreshTokensAsync();
            var chatSuccess = await _chatTokenService.ForceRefreshChatTokenAsync(); 

            var success = userAppSuccess && chatSuccess;

            if (success)
            {
                return Ok(new { message = "Alle Token erfolgreich erneuert" });
            }
            else
            {
                return StatusCode(500, new
                {
                    error = "Token-Refresh teilweise fehlgeschlagen",
                    userAppSuccess = userAppSuccess,
                    chatSuccess = chatSuccess
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
