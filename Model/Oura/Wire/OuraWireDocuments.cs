using System.Text.Json.Serialization;

namespace lionheart.Model.Oura.Wire;

// The Oura v2 payloads exactly as they arrive, one record per collection. Nothing here is a
// domain type: these exist only to be deserialized and then mapped by OuraDocumentMapping, which
// is where every value is checked and named.
//
// Every member is nullable, including the ones Oura always sends. That is the whole point of a
// separate wire layer: deserialization of an unexpected payload has to produce a record the
// mapper can refuse with a reason, not throw halfway through a file. Ids, days and instants arrive
// as strings and are parsed by the mapper for the same reason — a malformed day should be
// reportable as a malformed day rather than as an unreadable line.
//
// The same records serve the live pull and the archive import, because the archived .jsonl files
// are recorded Oura responses. One mapping, exercised by both paths, is what lets tests over
// recorded payloads cover a pull that cannot be run without the athlete's token.

/// <summary>The envelope every Oura collection response arrives in.</summary>
public sealed record OuraPage<T>
{
    [JsonPropertyName("data")]
    public List<T>? Data { get; init; }

    /// <summary>
    /// The cursor for the next page, absent on the last one.
    /// </summary>
    /// <remarks>
    /// The explicit name is load-bearing: Oura spells this <c>next_token</c>, and binding it by
    /// convention matches nothing, which makes paging stop silently after the first page rather
    /// than fail. <c>OuraClientTests</c> is the tripwire.
    /// </remarks>
    [JsonPropertyName("next_token")]
    public string? NextToken { get; init; }
}

/// <summary>An evenly-spaced sample run: <c>{interval, items, timestamp}</c>.</summary>
public sealed record WireSampleSeries
{
    [JsonPropertyName("interval")]
    public double? Interval { get; init; }

    [JsonPropertyName("items")]
    public List<double?>? Items { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }
}

public sealed record WireDailyActivity
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("score")] public int? Score { get; init; }
    [JsonPropertyName("contributors")] public WireActivityContributors? Contributors { get; init; }
    [JsonPropertyName("steps")] public int? Steps { get; init; }
    [JsonPropertyName("active_calories")] public int? ActiveCalories { get; init; }
    [JsonPropertyName("total_calories")] public int? TotalCalories { get; init; }
    [JsonPropertyName("target_calories")] public int? TargetCalories { get; init; }
    [JsonPropertyName("equivalent_walking_distance")] public int? EquivalentWalkingDistance { get; init; }
    [JsonPropertyName("meters_to_target")] public int? MetersToTarget { get; init; }
    [JsonPropertyName("target_meters")] public int? TargetMeters { get; init; }
    [JsonPropertyName("inactivity_alerts")] public int? InactivityAlerts { get; init; }
    [JsonPropertyName("average_met_minutes")] public double? AverageMetMinutes { get; init; }
    [JsonPropertyName("low_activity_met_minutes")] public int? LowActivityMetMinutes { get; init; }
    [JsonPropertyName("medium_activity_met_minutes")] public int? MediumActivityMetMinutes { get; init; }
    [JsonPropertyName("high_activity_met_minutes")] public int? HighActivityMetMinutes { get; init; }
    [JsonPropertyName("sedentary_met_minutes")] public int? SedentaryMetMinutes { get; init; }
    [JsonPropertyName("resting_time")] public int? RestingTime { get; init; }
    [JsonPropertyName("sedentary_time")] public int? SedentaryTime { get; init; }
    [JsonPropertyName("low_activity_time")] public int? LowActivityTime { get; init; }
    [JsonPropertyName("medium_activity_time")] public int? MediumActivityTime { get; init; }
    [JsonPropertyName("high_activity_time")] public int? HighActivityTime { get; init; }
    [JsonPropertyName("non_wear_time")] public int? NonWearTime { get; init; }

    /// <summary>When the activity day begins: 04:00 on the athlete's own clock, with its offset.</summary>
    [JsonPropertyName("timestamp")] public string? Timestamp { get; init; }

    [JsonPropertyName("class_5_min")] public string? Class5Min { get; init; }
}

public sealed record WireActivityContributors
{
    [JsonPropertyName("meet_daily_targets")] public int? MeetDailyTargets { get; init; }
    [JsonPropertyName("move_every_hour")] public int? MoveEveryHour { get; init; }
    [JsonPropertyName("recovery_time")] public int? RecoveryTime { get; init; }
    [JsonPropertyName("stay_active")] public int? StayActive { get; init; }
    [JsonPropertyName("training_frequency")] public int? TrainingFrequency { get; init; }
    [JsonPropertyName("training_volume")] public int? TrainingVolume { get; init; }
}

public sealed record WireDailyReadiness
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("score")] public int? Score { get; init; }
    [JsonPropertyName("temperature_deviation")] public double? TemperatureDeviation { get; init; }
    [JsonPropertyName("temperature_trend_deviation")] public double? TemperatureTrendDeviation { get; init; }
    [JsonPropertyName("contributors")] public WireReadinessContributors? Contributors { get; init; }
}

public sealed record WireReadinessContributors
{
    [JsonPropertyName("activity_balance")] public int? ActivityBalance { get; init; }
    [JsonPropertyName("body_temperature")] public int? BodyTemperature { get; init; }
    [JsonPropertyName("hrv_balance")] public int? HrvBalance { get; init; }
    [JsonPropertyName("previous_day_activity")] public int? PreviousDayActivity { get; init; }
    [JsonPropertyName("previous_night")] public int? PreviousNight { get; init; }
    [JsonPropertyName("recovery_index")] public int? RecoveryIndex { get; init; }
    [JsonPropertyName("resting_heart_rate")] public int? RestingHeartRate { get; init; }
    [JsonPropertyName("sleep_balance")] public int? SleepBalance { get; init; }
    [JsonPropertyName("sleep_regularity")] public int? SleepRegularity { get; init; }
}

public sealed record WireDailySleep
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("score")] public int? Score { get; init; }
    [JsonPropertyName("contributors")] public WireSleepContributors? Contributors { get; init; }
}

public sealed record WireSleepContributors
{
    [JsonPropertyName("deep_sleep")] public int? DeepSleep { get; init; }
    [JsonPropertyName("efficiency")] public int? Efficiency { get; init; }
    [JsonPropertyName("latency")] public int? Latency { get; init; }
    [JsonPropertyName("rem_sleep")] public int? RemSleep { get; init; }
    [JsonPropertyName("restfulness")] public int? Restfulness { get; init; }
    [JsonPropertyName("timing")] public int? Timing { get; init; }
    [JsonPropertyName("total_sleep")] public int? TotalSleep { get; init; }
}

public sealed record WireDailySpo2
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("spo2_percentage")] public WireSpo2Percentage? Spo2Percentage { get; init; }
    [JsonPropertyName("breathing_disturbance_index")] public int? BreathingDisturbanceIndex { get; init; }
}

public sealed record WireSpo2Percentage
{
    [JsonPropertyName("average")] public double? Average { get; init; }
}

public sealed record WireDailyStress
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("stress_high")] public int? StressHigh { get; init; }
    [JsonPropertyName("recovery_high")] public int? RecoveryHigh { get; init; }
    [JsonPropertyName("day_summary")] public string? DaySummary { get; init; }
}

public sealed record WireDailyResilience
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("level")] public string? Level { get; init; }
    [JsonPropertyName("contributors")] public WireResilienceContributors? Contributors { get; init; }
}

public sealed record WireResilienceContributors
{
    [JsonPropertyName("sleep_recovery")] public double? SleepRecovery { get; init; }
    [JsonPropertyName("daytime_recovery")] public double? DaytimeRecovery { get; init; }
    [JsonPropertyName("stress")] public double? Stress { get; init; }
}

public sealed record WireVo2Max
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("timestamp")] public string? Timestamp { get; init; }
    [JsonPropertyName("vo2_max")] public int? Vo2Max { get; init; }
}

public sealed record WireDailyCardiovascularAge
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("vascular_age")] public int? VascularAge { get; init; }
    [JsonPropertyName("pulse_wave_velocity")] public double? PulseWaveVelocity { get; init; }
}

/// <summary>
/// One sample of the all-day heart-rate series. The only Oura payload here with no id and no day:
/// it is a bare reading, and its UTC instant is all there is to place it by.
/// </summary>
public sealed record WireHeartRate
{
    [JsonPropertyName("timestamp")] public string? Timestamp { get; init; }
    [JsonPropertyName("bpm")] public int? Bpm { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
}

public sealed record WireSleep
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("period")] public int? Period { get; init; }
    [JsonPropertyName("bedtime_start")] public string? BedtimeStart { get; init; }
    [JsonPropertyName("bedtime_end")] public string? BedtimeEnd { get; init; }
    [JsonPropertyName("time_in_bed")] public int? TimeInBed { get; init; }
    [JsonPropertyName("total_sleep_duration")] public int? TotalSleepDuration { get; init; }
    [JsonPropertyName("deep_sleep_duration")] public int? DeepSleepDuration { get; init; }
    [JsonPropertyName("light_sleep_duration")] public int? LightSleepDuration { get; init; }
    [JsonPropertyName("rem_sleep_duration")] public int? RemSleepDuration { get; init; }
    [JsonPropertyName("awake_time")] public int? AwakeTime { get; init; }
    [JsonPropertyName("latency")] public int? Latency { get; init; }
    [JsonPropertyName("efficiency")] public int? Efficiency { get; init; }
    [JsonPropertyName("restless_periods")] public int? RestlessPeriods { get; init; }
    [JsonPropertyName("average_heart_rate")] public double? AverageHeartRate { get; init; }
    [JsonPropertyName("lowest_heart_rate")] public int? LowestHeartRate { get; init; }
    [JsonPropertyName("average_hrv")] public int? AverageHrv { get; init; }
    [JsonPropertyName("average_breath")] public double? AverageBreath { get; init; }
    [JsonPropertyName("readiness_score_delta")] public int? ReadinessScoreDelta { get; init; }
    [JsonPropertyName("sleep_score_delta")] public int? SleepScoreDelta { get; init; }
    [JsonPropertyName("sleep_algorithm_version")] public string? SleepAlgorithmVersion { get; init; }
    [JsonPropertyName("sleep_analysis_reason")] public string? SleepAnalysisReason { get; init; }
    [JsonPropertyName("sleep_phase_5_min")] public string? SleepPhase5Min { get; init; }
    [JsonPropertyName("heart_rate")] public WireSampleSeries? HeartRate { get; init; }
    [JsonPropertyName("hrv")] public WireSampleSeries? Hrv { get; init; }
}

public sealed record WireWorkout
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("activity")] public string? Activity { get; init; }
    [JsonPropertyName("intensity")] public string? Intensity { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("label")] public string? Label { get; init; }
    [JsonPropertyName("calories")] public double? Calories { get; init; }
    [JsonPropertyName("distance")] public double? Distance { get; init; }
    [JsonPropertyName("start_datetime")] public string? StartDatetime { get; init; }
    [JsonPropertyName("end_datetime")] public string? EndDatetime { get; init; }
}

public sealed record WireSession
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("mood")] public string? Mood { get; init; }
    [JsonPropertyName("start_datetime")] public string? StartDatetime { get; init; }
    [JsonPropertyName("end_datetime")] public string? EndDatetime { get; init; }
    [JsonPropertyName("heart_rate")] public WireSampleSeries? HeartRate { get; init; }
    [JsonPropertyName("heart_rate_variability")] public WireSampleSeries? HeartRateVariability { get; init; }
    [JsonPropertyName("motion_count")] public WireSampleSeries? MotionCount { get; init; }
}

public sealed record WireTag
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("day")] public string? Day { get; init; }
    [JsonPropertyName("tags")] public List<string?>? Tags { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("timestamp")] public string? Timestamp { get; init; }
}
