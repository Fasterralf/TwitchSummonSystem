using Microsoft.AspNetCore.Mvc;
using TwitchSummonSystem.Services;
using System.Text.Json;

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

            // Konvertiere zu JsonElement für sicheren Zugriff
            var userAppJson = JsonSerializer.Serialize(userAppStatus);
            var userAppElement = JsonSerializer.Deserialize<JsonElement>(userAppJson);

            var chatJson = JsonSerializer.Serialize(chatStatus);
            var chatElement = JsonSerializer.Deserialize<JsonElement>(chatJson);

            // Sichere Extraktion der User Token Daten
            var userTokenElement = userAppElement.GetProperty("userToken");
            var userValid = userTokenElement.TryGetProperty("valid", out var userValidProp) ? userValidProp.GetBoolean() : false;
            var userExpiresAt = userTokenElement.TryGetProperty("expiresAt", out var userExpiresProp) ? userExpiresProp.GetDateTime() : DateTime.UtcNow;
            var userHours = userTokenElement.TryGetProperty("hoursUntilExpiry", out var userHoursProp) ? userHoursProp.GetDouble() : 0.0;

            // Sichere Extraktion der App Token Daten
            var appTokenElement = userAppElement.GetProperty("appToken");
            var appValid = appTokenElement.TryGetProperty("valid", out var appValidProp) ? appValidProp.GetBoolean() : false;
            var appExpiresAt = appTokenElement.TryGetProperty("expiresAt", out var appExpiresProp) ? appExpiresProp.GetDateTime() : DateTime.UtcNow;
            var appDays = appTokenElement.TryGetProperty("daysUntilExpiry", out var appDaysProp) ? appDaysProp.GetDouble() : 0.0;

            // Sichere Extraktion der Chat Token Daten
            var chatValid = chatElement.TryGetProperty("valid", out var chatValidProp) ? chatValidProp.GetBoolean() : false;
            var chatExpiresAt = chatElement.TryGetProperty("expiresAt", out var chatExpiresProp) ? chatExpiresProp.GetDateTime() : DateTime.UtcNow;
            var chatHours = chatElement.TryGetProperty("hoursUntilExpiry", out var chatHoursProp) ? chatHoursProp.GetDouble() : 0.0;
            var chatError = chatElement.TryGetProperty("error", out var chatErrorProp) ? chatErrorProp.GetString() : null;

            var combinedStatus = new
            {
                userToken = new
                {
                    valid = userValid,
                    expiresAt = userExpiresAt,
                    daysUntilExpiry = userHours / 24.0
                },
                appToken = new
                {
                    valid = appValid,
                    expiresAt = appExpiresAt,
                    daysUntilExpiry = appDays
                },
                chatToken = new
                {
                    valid = chatValid,
                    expiresAt = chatExpiresAt,
                    daysUntilExpiry = chatHours / 24.0,
                    error = chatError
                },
                lastCheck = DateTime.UtcNow
            };

            return Ok(combinedStatus);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? [TOKEN-CONTROLLER] Error in GetTokenStatus: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? [TOKEN-CONTROLLER] Stack Trace: {ex.StackTrace}");

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
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? [TOKEN-CONTROLLER] Starting manual token refresh");

            var userAppSuccess = await _tokenService.ForceRefreshTokensAsync();
            var chatSuccess = await _chatTokenService.ForceRefreshChatTokenAsync();

            var success = userAppSuccess && chatSuccess;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? [TOKEN-CONTROLLER] Refresh result - UserApp: {userAppSuccess}, Chat: {chatSuccess}");

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
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? [TOKEN-CONTROLLER] Error in ForceRefreshTokens: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
