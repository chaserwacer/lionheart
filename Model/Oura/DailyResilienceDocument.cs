using System;
using System.Text.Json.Serialization;

namespace lionheart.Model.Oura.Dto
{
    /// <summary>
    /// Object representing daily resilience data document retrieved from Oura Ring API. This mirrors the structure of the Oura API response.
    /// </summary>
    public record OuraDailyResilienceDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("day")]
        public DateOnly Day { get; init; }

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
