using System.Text.Json.Serialization;

namespace ClaudeStatusMonitor.Models
{
    public class AppConfig
    {
        [JsonPropertyName("refreshIntervalMinutes")]
        public int RefreshIntervalMinutes { get; set; } = 2;
    }
}
