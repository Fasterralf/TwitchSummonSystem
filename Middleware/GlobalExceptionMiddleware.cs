using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;
using TwitchSummonSystem.Services;

namespace TwitchSummonSystem.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly DiscordService _discordService;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, DiscordService discordService)
        {
            _next = next;
            _logger = logger;
            _discordService = discordService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unbehandelter Fehler aufgetreten");

                // Discord Benachrichtigung senden (fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _discordService.SendErrorNotificationAsync(
                            $"Unbehandelter Fehler in {context.Request.Path}",
                            "GlobalExceptionHandler",
                            ex
                        );
                    }
                    catch
                    {
                        // Ignore Discord errors
                    }
                });

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                error = "Ein unerwarteter Fehler ist aufgetreten",
                message = exception.Message,
                timestamp = DateTime.UtcNow
            };

            var jsonResponse = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
