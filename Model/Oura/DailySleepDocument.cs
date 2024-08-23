using System;
using System.Text.Json.Serialization;

namespace lionheart.Model.Oura.Dto
{
    public record OuraDailySleepDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("contributors")]
        public required SleepContributorsDto Contributors { get; init; }

        [JsonPropertyName("day")]
        public DateOnly Day { get; init; }

        [JsonPropertyName("score")]
        public int? Score { get; init; }
    }

    public record SleepContributorsDto
    {
        [JsonPropertyName("deep_sleep")]
        public int? DeepSleep { get; init; }

        [JsonPropertyName("efficiency")]
        public int? Efficiency { get; init; }

        [JsonPropertyName("latency")]
        public int? Latency { get; init; }

        [JsonPropertyName("rem_sleep")]
        public int? RemSleep { get; init; }

        [JsonPropertyName("restfulness")]
        public int? Restfulness { get; init; }

        [JsonPropertyName("timing")]
        public int? Timing { get; init; }

        [JsonPropertyName("total_sleep")]
        public int? TotalSleep { get; init; }
    }
}
