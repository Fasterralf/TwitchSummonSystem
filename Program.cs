using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Services hinzufügen
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Unsere Services registrieren
builder.Services.AddSingleton<LotteryService>();
builder.Services.AddSingleton<TwitchService>();
builder.Services.AddSingleton<TokenService>(); // NEU
builder.Services.AddSingleton<TwitchChatService>(); // NEU
builder.Services.AddSingleton<TwitchEventSubService>(); // NEU
builder.Services.AddSingleton<DiscordService>();

// CORS für OBS Browser Source
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

// Chat Service sofort initialisieren
var chatService = app.Services.GetRequiredService<TwitchChatService>();
var eventSubService = app.Services.GetRequiredService<TwitchEventSubService>();
Console.WriteLine("🚀 Services werden initialisiert...");

_ = Task.Run(async () =>
{
    await Task.Delay(2000); // Kurz warten
    await eventSubService.InitializeRewardAsync();
});

// Middleware konfigurieren
app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Controller und SignalR Hub routen
app.MapControllers();
app.MapHub<SummonHub>("/summonhub");

// Standard Route für OBS Browser Source
app.MapGet("/", () => Results.Redirect("/obs.html"));

Console.WriteLine("🚀 Twitch Summon System gestartet!");
Console.WriteLine("📺 OBS Browser Source: http://localhost:5173/obs.html");
Console.WriteLine("🔗 Webhook Endpoint: http://localhost:5173/api/twitch/webhook");

app.Run();
