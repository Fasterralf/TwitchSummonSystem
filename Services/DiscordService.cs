using System.Text;
using System.Text.Json;

namespace TwitchSummonSystem.Services
{
    public class DiscordService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string? _errorWebhookUrl;
        private readonly string? _webhookUrl;

        public DiscordService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _errorWebhookUrl = _configuration["Discord:ErrorWebhookUrl"]; // ? RICHTIG
            _webhookUrl = _configuration["Discord:WebhookUrl"];
        }

        public async Task SendStartupNotificationAsync()
        {
            try
            {
                // ERROR WEBHOOK URL für System-Nachrichten verwenden!
                var targetUrl = _errorWebhookUrl ?? _webhookUrl;

                var embed = new
                {
                    title = "?? System gestartet",
                    description = "TwitchSummonSystem wurde erfolgreich gestartet",
                    color = 65280, // Grün
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    fields = new[]
                    {
                new { name = "Status", value = "? Online", inline = true },
                new { name = "Startzeit", value = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), inline = true },
                new { name = "Services", value = "Discord ?\nToken Management ?\nChat Bot ?", inline = false }
            }
                };

                var payload = new
                {
                    embeds = new[] { embed }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(targetUrl, content); // ? ERROR CHANNEL

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Startup notification sent to Discord ERROR channel");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ?? Discord startup notification failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ? Discord startup notification error: {ex.Message}");
            }
        }

        public async Task SendErrorNotificationAsync(string errorMessage, string? component = null, Exception? exception = null)
        {
            if (string.IsNullOrEmpty(_errorWebhookUrl))
            {
                Console.WriteLine("?? Discord error webhook URL not configured");
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
                            title = "?? SYSTEM ERROR",
                            description = $"**Fehler aufgetreten:** {errorMessage}",
                            color = 15158332, // Rot
                            fields = new List<object>
                            {
                                new
                                {
                                    name = "? Zeitpunkt",
                                    value = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
                                    inline = true
                                }
                            }.Concat(component != null ? new[]
                            {
                                new
                                {
                                    name = "?? Komponente",
                                    value = component,
                                    inline = true
                                }
                            } : Array.Empty<object>())
                            .Concat(exception != null ? new[]
                            {
                                new
                                {
                                    name = "?? Exception Details",
                                    value = $"```{exception.GetType().Name}: {exception.Message}```",
                                    inline = false
                                }
                            } : Array.Empty<object>())
                            .ToArray(),
                            thumbnail = new
                            {
                                url = "https://cdn.discordapp.com/attachments/1166234116046100520/1166234120501684224/error.png"
                            },
                            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        }
                    }
                };

                var json = JsonSerializer.Serialize(embed);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_errorWebhookUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"? Discord error notification sent");
                }
                else
                {
                    Console.WriteLine($"? Discord error webhook failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Discord error service failed: {ex.Message}");
            }
        }

        public async Task SendGoldNotificationAsync(string username, double goldChance, int totalSummons, int totalGolds)
        {
            if (string.IsNullOrEmpty(_webhookUrl))
            {
                Console.WriteLine("?? Discord webhook URL not configured");
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
                            title = "?? LEGENDARY GOLD GEWONNEN! ??",
                            description = $"**{username}** hat Gold erhalten!",
                            color = 16766720, 
                            fields = new[]
                            {
                                new
                                {
                                    name = "?? Chance",
                                    value = $"{goldChance:F1}%",
                                    inline = true
                                },
                                new
                                {
                                    name = "?? Gesamt Summons",
                                    value = totalSummons.ToString(),
                                    inline = true
                                },
                                new
                                {
                                    name = "? Gesamt Golds",
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
                    Console.WriteLine($"? Discord notification sent for {username}");
                }
                else
                {
                    Console.WriteLine($"? Discord webhook error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Discord service error: {ex.Message}");
            }
        }        
    }
}
