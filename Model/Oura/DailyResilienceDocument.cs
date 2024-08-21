using System;
using System.Text.Json.Serialization;

namespace lionheart.Model.Oura.Dto
{
    public record OuraDailyResilienceDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("day")]
        public DateTime Day { get; init; }

        [JsonPropertyName("contributors")]
        public required ResilienceContributorsDto Contributors { get; init; }

        [JsonPropertyName("level")]
        public string Level { get; init; } = string.Empty;
    }

    public record ResilienceContributorsDto
    {
        [JsonPropertyName("sleep_recovery")]
        public double SleepRecovery { get; init; }

        [JsonPropertyName("daytime_recovery")]
        public double DaytimeRecovery { get; init; }

        [JsonPropertyName("stress")]
        public double Stress { get; init; }
    }
}
