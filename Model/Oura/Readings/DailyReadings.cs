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
    int NonWearTimeSeconds,
    ActivityTimeline? Timeline)
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

    /// <summary>
    /// The day's shape: which activity class each five-minute slot fell in.
    /// </summary>
    /// <remarks>
    /// Absent where Oura sent no classification, and on any document stored before this member
    /// existed until the archive or a sync is read again. The time totals above stay Oura's own
    /// figures rather than being summed from this: Oura computes them per minute, so they differ
    /// from a count of five-minute slots by a few minutes a band on most days.
    /// </remarks>
    public ActivityTimeline? Timeline { get; init; } = Timeline;
}

/// <summary>
/// The activity class of every five-minute slot of one Oura activity day, from its first slot.
/// </summary>
/// <remarks>
/// Oura's activity day runs from 04:00 local time to 04:00 the next morning, and
/// <see cref="StartedAt"/> carries that local offset, so slot <c>n</c> sits at
/// <c>StartedAt + 5n</c> minutes on the athlete's own clock. A full day is 288 slots; a day still
/// in progress, or one with a time-zone change in it, is shorter or longer.
/// <para>
/// Kept as Oura's digit-per-slot string, the same way the hypnogram is, and decoded where it is
/// read. The encoding is Oura's documented one — 0 non-wear, 1 rest, 2 inactive, 3 low, 4 medium,
/// 5 high — and storing the digits rather than a decoded list keeps a day at 288 bytes and lets a
/// decoder that is ever wrong be corrected without touching storage.
/// </para>
/// </remarks>
public sealed record ActivityTimeline(DateTimeOffset StartedAt, string Classes)
{
    public DateTimeOffset StartedAt { get; init; } = StartedAt;
    public string Classes { get; init; } = Require.Present(Classes, nameof(Classes));

    public const int SlotMinutes = 5;

    /// <summary>Where slot <paramref name="index"/> begins on the clock.</summary>
    public DateTimeOffset SlotStart(int index) => StartedAt.AddMinutes(SlotMinutes * index);
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

/// <summary>
/// Oura's estimate of the athlete's VO2max, in ml/kg/min.
/// </summary>
/// <remarks>
/// An <strong>estimate</strong>, not a measurement: Oura derives it from heart rate and movement
/// without a gas-exchange test, and issues one on the days it has enough walking to work from. Its
/// value lies in its trend against itself. Shaped from Oura's published schema; the athlete's
/// archive holds no file for this collection, so no recorded payload pins it.
/// </remarks>
public sealed record Vo2MaxReading(DateTimeOffset EstimatedAt, int Vo2Max);

/// <summary>
/// Oura's prediction of the athlete's vascular age, and the pulse-wave velocity it is derived from.
/// </summary>
/// <remarks>
/// A model output rather than a measurement, and both members are independently absent in Oura's
/// schema. Pulse-wave velocity is in metres per second; Oura documents it as derived from the
/// vascular age, possibly with an offset added, so the age is the primary figure. Shaped from the
/// published schema, like <see cref="Vo2MaxReading"/>.
/// </remarks>
public sealed record CardiovascularAgeReading(int? VascularAge, double? PulseWaveVelocity);
