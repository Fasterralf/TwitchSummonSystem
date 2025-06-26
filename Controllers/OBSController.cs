using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using TwitchSummonSystem.Hubs;

[ApiController]
[Route("api/obs")]
public class OBSController : ControllerBase
{
    private readonly IHubContext<SummonHub> _hubContext;
    private readonly string _configPath = "obs-config.json";

    public OBSController(IHubContext<SummonHub> hubContext)
    {
        _hubContext = hubContext;
    }

    private bool GetStatsVisibilityFromFile()
    {
        try
        {
            if (System.IO.File.Exists(_configPath))
            {
                var json = System.IO.File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<OBSConfig>(json);
                return config?.StatsVisible ?? true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading OBS config: {ex.Message}");
        }
        return true; // Default: sichtbar
    }

    private void SaveStatsVisibilityToFile(bool visible)
    {
        try
        {
            var config = new OBSConfig { StatsVisible = visible };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving OBS config: {ex.Message}");
        }
    }

    [HttpGet("stats-visibility")]
    public IActionResult GetStatsVisibility()
    {
        var visible = GetStatsVisibilityFromFile();
        return Ok(new { visible = visible });
    }

    [HttpPost("toggle-stats")]
    public async Task<IActionResult> ToggleStats([FromBody] StatsVisibilityRequest request)
    {
        try
        {
            SaveStatsVisibilityToFile(request.Visible);

            // SignalR Nachricht an alle OBS Clients
            await _hubContext.Clients.All.SendAsync("StatsVisibilityChanged", request.Visible);

            return Ok(new
            {
                success = true,
                visible = request.Visible,
                message = request.Visible ? "Stats eingeblendet" : "Stats ausgeblendet"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}

public class OBSConfig
{
    public bool StatsVisible { get; set; } = true;
}

public class StatsVisibilityRequest
{
    public bool Visible { get; set; }
}
