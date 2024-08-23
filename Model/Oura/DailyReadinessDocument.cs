using System;
using System.Text.Json.Serialization;

namespace lionheart.Model.Oura.Dto
{
    public record OuraDailyReadinessDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("contributors")]
        public required ReadinessContributorsDto Contributors { get; init; }

        [JsonPropertyName("score")]
        public int? Score { get; init; }

        [JsonPropertyName("temperature_deviation")]
        public double? TemperatureDeviation { get; init; }

        [JsonPropertyName("day")]
        public DateOnly Day { get; init; }
    }

    public record ReadinessContributorsDto
    {
        [JsonPropertyName("activity_balance")]
        public int? ActivityBalance { get; init; }

        [JsonPropertyName("body_temperature")]
        public int? BodyTemperature { get; init; }

        [JsonPropertyName("hrv_balance")]
        public int? HrvBalance { get; init; }

        [JsonPropertyName("previous_day_activity")]
        public int? PreviousDayActivity { get; init; }

        [JsonPropertyName("previous_night")]
        public int? PreviousNight { get; init; }

        [JsonPropertyName("recovery_index")]
        public int? RecoveryIndex { get; init; }

        [JsonPropertyName("resting_heart_rate")]
        public int? RestingHeartRate { get; init; }

        [JsonPropertyName("sleep_balance")]
        public int? SleepBalance { get; init; }
    }
}
