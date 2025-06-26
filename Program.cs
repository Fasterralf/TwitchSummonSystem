using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Services;
using TwitchSummonSystem.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Environment Variables laden
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

// Health Checks hinzufügen
builder.Services.AddHealthChecks()
    .AddCheck("TwitchAPI", () =>
    {
        // Hier könntest du einen echten Health Check für die Twitch API machen
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Twitch API erreichbar");
    })
    .AddCheck("Discord", () =>
    {
        // Health Check für Discord Webhook
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Discord Webhook erreichbar");
    });

// Rate Limiting hinzufügen
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10, // 10 Requests
                Window = TimeSpan.FromMinutes(1) // pro Minute
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddHttpClient<DiscordService>();
builder.Services.AddHttpClient<TokenService>();
builder.Services.AddHttpClient<TwitchEventSubService>();
builder.Services.AddSingleton<LotteryService>();
builder.Services.AddSingleton<TwitchService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<ChatTokenService>(); // ← Neu
builder.Services.AddSingleton<TwitchChatService>();
builder.Services.AddSingleton<TwitchEventSubService>();
builder.Services.AddSingleton<DiscordService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Global Exception Handling
app.UseMiddleware<GlobalExceptionMiddleware>();

var chatService = app.Services.GetRequiredService<TwitchChatService>();
var eventSubService = app.Services.GetRequiredService<TwitchEventSubService>();

Console.WriteLine("🚀 Services werden initialisiert...");

_ = Task.Run(async () =>
{
    await Task.Delay(2000);
    await eventSubService.InitializeRewardAsync();
});

// Discord Startup-Nachricht senden
_ = Task.Run(async () =>
{
    try
    {
        // Kurz warten bis alle Services initialisiert sind
        await Task.Delay(3000);

        var discordService = app.Services.GetRequiredService<DiscordService>();
        await discordService.SendStartupNotificationAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Fehler beim Senden der Startup-Nachricht: {ex.Message}");
    }
});

app.UseCors("AllowAll");
app.UseRateLimiter(); // Rate Limiting aktivieren
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SummonHub>("/summonhub");
app.MapHealthChecks("/health"); // Health Check Endpoint
app.MapGet("/", () => Results.Redirect("/obs.html"));

var urls = app.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5173";
Console.WriteLine("🚀 Twitch Summon System gestartet!");
Console.WriteLine($"📺 OBS Browser Source: {urls.Replace("0.0.0.0", "localhost")}/obs.html");
Console.WriteLine($"🔗 Webhook Endpoint: {urls.Replace("0.0.0.0", "localhost")}/api/twitch/webhook");
Console.WriteLine($"❤️ Health Check: {urls.Replace("0.0.0.0", "localhost")}/health");

app.Run();
