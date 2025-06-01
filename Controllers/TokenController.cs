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

            // Sichere Extraktion der Token-Daten mit korrekten Property-Namen
            dynamic userAppData = userAppStatus;
            dynamic chatData = chatStatus;

            var combinedStatus = new
            {
                userToken = new
                {
                    valid = userAppData?.userToken?.valid ?? false,
                    expiresAt = userAppData?.userToken?.expiresAt ?? DateTime.UtcNow,
                    // Konvertiere hoursUntilExpiry zu daysUntilExpiry für User Token
                    daysUntilExpiry = (userAppData?.userToken?.hoursUntilExpiry ?? 0.0) / 24.0
                },
                appToken = new
                {
                    valid = userAppData?.appToken?.valid ?? false,
                    expiresAt = userAppData?.appToken?.expiresAt ?? DateTime.UtcNow,
                    // App Token hat bereits daysUntilExpiry
                    daysUntilExpiry = userAppData?.appToken?.daysUntilExpiry ?? 0.0
                },
                chatToken = new
                {
                    valid = chatData?.valid ?? false,
                    expiresAt = chatData?.expiresAt ?? DateTime.UtcNow,
                    // Konvertiere hoursUntilExpiry zu daysUntilExpiry für Chat Token
                    daysUntilExpiry = (chatData?.hoursUntilExpiry ?? 0.0) / 24.0,
                    error = chatData?.error
                },
                lastCheck = DateTime.UtcNow
            };

            return Ok(combinedStatus);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = ex.Message,
                userToken = new { valid = false, expiresAt = DateTime.UtcNow, daysUntilExpiry = 0.0 },
                appToken = new { valid = false, expiresAt = DateTime.UtcNow, daysUntilExpiry = 0.0 },
                chatToken = new { valid = false, error = "Service unavailable", daysUntilExpiry = 0.0 }
            });
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
