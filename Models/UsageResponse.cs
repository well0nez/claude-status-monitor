using System.Text.Json.Serialization;

namespace ClaudeStatusMonitor.Models
{
    public class UsageResponse
    {
        [JsonPropertyName("five_hour")]
        public UsageLimit? FiveHour { get; set; }

        [JsonPropertyName("seven_day")]
        public UsageLimit? SevenDay { get; set; }

        [JsonPropertyName("seven_day_sonnet")]
        public UsageLimit? SevenDaySonnet { get; set; }

        [JsonPropertyName("seven_day_oauth_apps")]
        public UsageLimit? SevenDayOAuthApps { get; set; }

        [JsonPropertyName("seven_day_opus")]
        public UsageLimit? SevenDayOpus { get; set; }

        [JsonPropertyName("seven_day_cowork")]
        public UsageLimit? SevenDayCowork { get; set; }

        [JsonPropertyName("iguana_necktie")]
        public object? IguanaNecktie { get; set; }

        [JsonPropertyName("extra_usage")]
        public object? ExtraUsage { get; set; }
    }
}