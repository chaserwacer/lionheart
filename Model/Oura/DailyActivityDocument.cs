using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace lionheart.Model.Oura.Dto
{
    /// <summary>
    /// Object representing daily activity data document retrieved from Oura Ring API. This mirrors the structure of the Oura API response.
    /// </summary>
    public record OuraDailyActivityDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("score")]
        public int? Score { get; init; }

        [JsonPropertyName("active_calories")]
        public int ActiveCalories { get; init; }

        [JsonPropertyName("contributors")]
        public required ActivityContributorsDto Contributors { get; init; }

        [JsonPropertyName("steps")]
        public int Steps { get; init; }

        [JsonPropertyName("target_calories")]
        public int TargetCalories { get; init; }

        [JsonPropertyName("total_calories")]
        public int TotalCalories { get; init; }

        [JsonPropertyName("day")]
        public DateOnly Day { get; init; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; init; } = string.Empty;
    }

    public record ActivityContributorsDto
    {
        [JsonPropertyName("meet_daily_targets")]
        public int? MeetDailyTargets { get; init; }

        [JsonPropertyName("move_every_hour")]
        public int? MoveEveryHour { get; init; }

        [JsonPropertyName("recovery_time")]
        public int? RecoveryTime { get; init; }

        [JsonPropertyName("stay_active")]
        public int? StayActive { get; init; }

        [JsonPropertyName("training_frequency")]
        public int? TrainingFrequency { get; init; }

        [JsonPropertyName("training_volume")]
        public int? TrainingVolume { get; init; }
    }

    // public record Met
    // {
    //     public int Interval { get; init; }
    //     public List<object> Items { get; init; } = [];
    //     public string Timestamp { get; init; }  = string.Empty;
    // }
}
