using TwitchSummonSystem.Hubs;
using TwitchSummonSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Services hinzufügen
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Unsere Services registrieren
builder.Services.AddSingleton<PityService>();
builder.Services.AddSingleton<TwitchService>();
builder.Services.AddSingleton<TokenService>(); // NEU
builder.Services.AddSingleton<TwitchChatService>(); // NEU
builder.Services.AddSingleton<TwitchEventSubService>(); // NEU

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
