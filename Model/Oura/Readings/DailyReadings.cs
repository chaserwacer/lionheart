namespace lionheart.Model.Oura.Readings;

// One record per Oura daily document: what the ring measured for a calendar day. Each is the
// payload half of its document type in Model/Oura/OuraDocuments.cs, which owns identity and day.
//
// Two decisions here read as departures from house convention and are argued in full in
// docs/oura-integration.md. In short: a nullable numeric is Oura reporting no value, which is the
// measurement's own content rather than a gap — and a substituted 0 would be indistinguishable
// from a real score of zero. Oura's vocabularies stay the strings Oura sent, because they are an
// external vocabulary it extends on its own schedule and a closed set would mean rejecting a
// record the day it does.

/// <summary>How much the athlete moved, and Oura's score for it.</summary>
public sealed record DailyActivityReading(
    int? Score,
    ActivityContributors Contributors,
    int Steps,
    int ActiveCalories,
    int TotalCalories,
    int TargetCalories,
    int EquivalentWalkingDistanceMeters,
    int MetersToTarget,
    int TargetMeters,
    int InactivityAlerts,
    double AverageMet,   // a unitless mean multiple of resting metabolic rate, unlike the sums below
    int LowActivityMetMinutes,
    int MediumActivityMetMinutes,
    int HighActivityMetMinutes,
    int SedentaryMetMinutes,
    int RestingTimeSeconds,
    int SedentaryTimeSeconds,
    int LowActivityTimeSeconds,
    int MediumActivityTimeSeconds,
    int HighActivityTimeSeconds,
    int NonWearTimeSeconds)
{
    public int? Score { get; init; } = Score;
    public ActivityContributors Contributors { get; init; } = Require.Present(Contributors, nameof(Contributors));
    public int Steps { get; init; } = Steps;
    public int ActiveCalories { get; init; } = ActiveCalories;
    public int TotalCalories { get; init; } = TotalCalories;
    public int TargetCalories { get; init; } = TargetCalories;
    public int EquivalentWalkingDistanceMeters { get; init; } = EquivalentWalkingDistanceMeters;
    public int MetersToTarget { get; init; } = MetersToTarget;
    public int TargetMeters { get; init; } = TargetMeters;
    public int InactivityAlerts { get; init; } = InactivityAlerts;
    public double AverageMet { get; init; } = AverageMet;
    public int LowActivityMetMinutes { get; init; } = LowActivityMetMinutes;
    public int MediumActivityMetMinutes { get; init; } = MediumActivityMetMinutes;
    public int HighActivityMetMinutes { get; init; } = HighActivityMetMinutes;
    public int SedentaryMetMinutes { get; init; } = SedentaryMetMinutes;
    public int RestingTimeSeconds { get; init; } = RestingTimeSeconds;
    public int SedentaryTimeSeconds { get; init; } = SedentaryTimeSeconds;
    public int LowActivityTimeSeconds { get; init; } = LowActivityTimeSeconds;
    public int MediumActivityTimeSeconds { get; init; } = MediumActivityTimeSeconds;
    public int HighActivityTimeSeconds { get; init; } = HighActivityTimeSeconds;
    public int NonWearTimeSeconds { get; init; } = NonWearTimeSeconds;
}

/// <summary>The six components Oura's activity score is built from, each 1-100.</summary>
public sealed record ActivityContributors(
    int? MeetDailyTargets,
    int? MoveEveryHour,
    int? RecoveryTime,
    int? StayActive,
    int? TrainingFrequency,
    int? TrainingVolume);

/// <summary>Oura's read on how recovered the body is, and the components behind it.</summary>
public sealed record DailyReadinessReading(
    int? Score,
    double? TemperatureDeviation,
    double? TemperatureTrendDeviation,
    ReadinessContributors Contributors)
{
    public int? Score { get; init; } = Score;
    public double? TemperatureDeviation { get; init; } = TemperatureDeviation;
    public double? TemperatureTrendDeviation { get; init; } = TemperatureTrendDeviation;
    public ReadinessContributors Contributors { get; init; } = Require.Present(Contributors, nameof(Contributors));
}

/// <summary>
/// The nine components Oura's readiness score is built from, each 1-100.
/// </summary>
/// <remarks>
/// Each is a score comparing that day against the athlete's own baseline, not a measurement:
/// <see cref="RestingHeartRate"/> is how his resting rate compared, in the range 1-100, and is not
/// a heart rate. The rate itself lives on the night, as
/// <see cref="SleepPeriodReading.LowestHeartRate"/>.
/// </remarks>
public sealed record ReadinessContributors(
    int? ActivityBalance,
    int? BodyTemperature,
    int? HrvBalance,
    int? PreviousDayActivity,
    int? PreviousNight,
    int? RecoveryIndex,
    int? RestingHeartRate,
    int? SleepBalance,
    int? SleepRegularity);

/// <summary>Oura's score for the night as a whole, and the components behind it.</summary>
public sealed record DailySleepReading(int? Score, SleepContributors Contributors)
{
    public int? Score { get; init; } = Score;
    public SleepContributors Contributors { get; init; } = Require.Present(Contributors, nameof(Contributors));
}

/// <summary>The seven components Oura's sleep score is built from, each 1-100.</summary>
public sealed record SleepContributors(
    int? DeepSleep,
    int? Efficiency,
    int? Latency,
    int? RemSleep,
    int? Restfulness,
    int? Timing,
    int? TotalSleep);

/// <summary>
/// Overnight blood-oxygen saturation and how disturbed the night's breathing was.
/// </summary>
/// <remarks>
/// The two are independently absent — 25 archived records carry one without the other — so neither
/// stands in for the other's presence. Oura has also issued two documents for the same day across
/// one stretch of this athlete's history, so a day is not a unique key for this collection; see
/// docs/oura-integration.md.
/// </remarks>
public sealed record DailySpo2Reading(double? AverageSpo2Percentage, int? BreathingDisturbanceIndex);

/// <summary>
/// How much of the day the ring read as stressed and how much as recovered, in seconds, plus
/// Oura's one-word verdict on the balance between them.
/// </summary>
public sealed record DailyStressReading(int? StressHighSeconds, int? RecoveryHighSeconds, string? DaySummary);

/// <summary>
/// Oura's resilience level and the three recovery components behind it.
/// </summary>
/// <remarks>
/// The only collection stored here whose shape is not pinned by a recorded payload — the athlete's
/// archived history holds no resilience file — so these members follow Oura's published schema,
/// and this is the one mapping no recorded-payload test can cover.
/// </remarks>
public sealed record DailyResilienceReading(string Level, ResilienceContributors Contributors)
{
    public string Level { get; init; } = Require.Present(Level, nameof(Level));
    public ResilienceContributors Contributors { get; init; } = Require.Present(Contributors, nameof(Contributors));
}

/// <summary>The three components Oura's resilience level is built from.</summary>
public sealed record ResilienceContributors(double? SleepRecovery, double? DaytimeRecovery, double? Stress);
