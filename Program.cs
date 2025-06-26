using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Services;
using TwitchSummonSystem.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

builder.Services.AddHealthChecks()
    .AddCheck("TwitchAPI", () =>
    {
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Twitch API reachable");
    })
    .AddCheck("Discord", () =>
    {
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Discord webhook reachable");
    });

builder.Services.AddHttpClient<DiscordService>();
builder.Services.AddHttpClient<TokenService>();
builder.Services.AddHttpClient<TwitchEventSubService>();
builder.Services.AddSingleton<LotteryService>();
builder.Services.AddSingleton<TwitchService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<ChatTokenService>();
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

app.UseMiddleware<GlobalExceptionMiddleware>();

var chatService = app.Services.GetRequiredService<TwitchChatService>();
var eventSubService = app.Services.GetRequiredService<TwitchEventSubService>();

Console.WriteLine("🚀 Initializing services...");

_ = Task.Run(async () =>
{
    await Task.Delay(2000);
    await eventSubService.InitializeRewardAsync();
});

_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(3000);
        var discordService = app.Services.GetRequiredService<DiscordService>();
        await discordService.SendStartupNotificationAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Error sending startup notification: {ex.Message}");
    }
});

app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SummonHub>("/summonhub");
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/obs.html"));

var urls = app.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5173";
Console.WriteLine("🚀 Twitch Summon System started!");
Console.WriteLine($"📺 OBS Browser Source: {urls.Replace("0.0.0.0", "localhost")}/obs.html");
Console.WriteLine($"🔗 Webhook Endpoint: {urls.Replace("0.0.0.0", "localhost")}/api/twitch/webhook");
Console.WriteLine($"❤️ Health Check: {urls.Replace("0.0.0.0", "localhost")}/health");

app.Run();
