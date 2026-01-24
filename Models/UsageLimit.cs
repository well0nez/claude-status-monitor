using System;
using System.Text.Json.Serialization;

namespace ClaudeStatusMonitor.Models
{
    public class UsageLimit
    {
        [JsonPropertyName("utilization")]
        public double Utilization { get; set; }

        [JsonPropertyName("resets_at")]
        public DateTime? ResetsAt { get; set; }
    }
}