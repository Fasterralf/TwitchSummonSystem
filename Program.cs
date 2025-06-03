using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
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
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SummonHub>("/summonhub");
app.MapGet("/", () => Results.Redirect("/obs.html"));

Console.WriteLine("🚀 Twitch Summon System gestartet!");
Console.WriteLine("📺 OBS Browser Source: http://localhost:5173/obs.html");
Console.WriteLine("🔗 Webhook Endpoint: http://localhost:5173/api/twitch/webhook");

app.Run();
