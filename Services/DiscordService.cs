using System.Text;
using System.Text.Json;

namespace TwitchSummonSystem.Services
{
    public class DiscordService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string? _webhookUrl;

        public DiscordService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _webhookUrl = _configuration["Discord:WebhookUrl"];
        }

        public async Task SendGoldNotificationAsync(string username, double goldChance, int totalSummons, int totalGolds)
        {
            if (string.IsNullOrEmpty(_webhookUrl))
            {
                Console.WriteLine("⚠️ Discord Websocket URL nicht konfiguriert");
                return;
            }

            try
            {
                var embed = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = "🌟 LEGENDARY GOLD GEWONNEN! 🌟",
                            description = $"**{username}** hat Gold erhalten!",
                            color = 16766720, 
                            fields = new[]
                            {
                                new
                                {
                                    name = "🎯 Chance",
                                    value = $"{goldChance:F1}%",
                                    inline = true
                                },
                                new
                                {
                                    name = "📊 Gesamt Summons",
                                    value = totalSummons.ToString(),
                                    inline = true
                                },
                                new
                                {
                                    name = "⭐ Gesamt Golds",
                                    value = totalGolds.ToString(),
                                    inline = true
                                }
                            },
                            thumbnail = new
                            {
                                url = "https://cdn.discordapp.com/attachments/1166234116046100520/1166234120501684224/gold.png"
                            },
                            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        }
                    }
                };

                var json = JsonSerializer.Serialize(embed);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_webhookUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Discord Benachrichtigung gesendet für {username}");
                }
                else
                {
                    Console.WriteLine($"❌ Discord Webhook Fehler: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Discord Service Fehler: {ex.Message}");
            }
        }        
    }
}
