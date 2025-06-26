using System.ComponentModel.DataAnnotations;

namespace TwitchSummonSystem.Configuration
{
    public class TwitchConfiguration
    {
        [Required]
        public string ClientId { get; set; } = string.Empty;
        
        [Required]
        public string ClientSecret { get; set; } = string.Empty;
        
        [Required]
        public string AccessToken { get; set; } = string.Empty;
        
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
        
        [Required]
        public string ChannelId { get; set; } = string.Empty;
        
        [Required]
        public string ChannelName { get; set; } = string.Empty;
        
        [Required]
        public string SummonRewardName { get; set; } = string.Empty;
        
        [Required]
        public string BotUsername { get; set; } = string.Empty;
        
        [Required]
        public string BotClientId { get; set; } = string.Empty;
        
        [Required]
        public string BotClientSecret { get; set; } = string.Empty;
        
        [Required]
        public string ChatOAuthToken { get; set; } = string.Empty;
        
        [Required]
        public string ChatRefreshToken { get; set; } = string.Empty;
    }

    public class DiscordConfiguration
    {
        [Required]
        [Url]
        public string WebhookUrl { get; set; } = string.Empty;
        
        [Required]
        [Url]
        public string ErrorWebhookUrl { get; set; } = string.Empty;
    }
}
