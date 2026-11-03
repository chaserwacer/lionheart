using lionheart.Model.Analysis;

namespace lionheart.Model.Oura.Readings;

// The payload half of the four Oura collections that are events rather than days: zero to many
// per calendar day, each occupying a span of clock time. When the event happened lives on the
// document as a Period, not in these records, so there is one answer to "when" per event and the
// stamped StartTime column cannot disagree with it.
//
// The note in DailyReadings.cs on nullable numerics and on storing Oura's vocabularies as strings
// applies here too.

/// <summary>
/// One sleep period the ring scored: a night, a nap, or a stretch of rest.
/// </summary>
/// <remarks>
/// Stage durations, the hypnogram and the overnight HRV curve are the whole reason this collection
/// is stored separately from the daily sleep score, which flattens all of it into one number.
/// <para>
/// Two fields Oura exposes are deliberately not here. Its 30-second hypnogram and movement strings
/// carry ten times the bytes of the 5-minute hypnogram for a resolution nothing in this
/// application reads, and their lengths disagree with each other in over half of the archived
/// records, so they cannot be aligned sample-for-sample anyway. Its <c>readiness</c> block is the
/// same readiness document already stored for that day, agreeing with it on all but a handful of
/// nights; keeping both would leave two answers to "how recovered was he".
/// </para>
/// </remarks>
public sealed record SleepPeriodReading(
    Period Period,
    string Kind,
    int PeriodIndex,
    int TimeInBedSeconds,
    int TotalSleepSeconds,
    int DeepSleepSeconds,
    int LightSleepSeconds,
    int RemSleepSeconds,
    int AwakeSeconds,
    int LatencySeconds,
    int Efficiency,
    int RestlessPeriods,
    double? AverageHeartRate,
    int? LowestHeartRate,
    int? AverageHrv,
    double? AverageBreath,
    int? ReadinessScoreDelta,
    int? SleepScoreDelta,
    string? AlgorithmVersion,
    string? AnalysisReason,
    string Hypnogram,
    OuraSampleSeries HeartRate,
    OuraSampleSeries Hrv)
{
    /// <summary>Bedtime to final waking, as Oura timed it.</summary>
    public Period Period { get; init; } = Require.Present(Period, nameof(Period));

    /// <summary>
    /// Oura's own word for what this period was. Four values appear in practice: a main sleep, a
    /// shorter sleep, a late nap, or rest.
    /// </summary>
    public string Kind { get; init; } = Require.Present(Kind, nameof(Kind));

    public int PeriodIndex { get; init; } = PeriodIndex;
    public int TimeInBedSeconds { get; init; } = TimeInBedSeconds;
    public int TotalSleepSeconds { get; init; } = TotalSleepSeconds;
    public int DeepSleepSeconds { get; init; } = DeepSleepSeconds;
    public int LightSleepSeconds { get; init; } = LightSleepSeconds;
    public int RemSleepSeconds { get; init; } = RemSleepSeconds;
    public int AwakeSeconds { get; init; } = AwakeSeconds;
    public int LatencySeconds { get; init; } = LatencySeconds;
    public int Efficiency { get; init; } = Efficiency;
    public int RestlessPeriods { get; init; } = RestlessPeriods;

    /// <summary>
    /// Mean heart rate across the period.
    /// </summary>
    /// <remarks>
    /// Oura reports <c>0</c> rather than null on short periods it took no heart-rate reading
    /// across — 148 of the archived records, nearly all of them brief naps. It is stored as it
    /// arrived rather than translated to absence: a zero here is Oura's answer, and rewriting its
    /// data on the way in would leave nothing able to tell the two cases apart later. Read it
    /// alongside <see cref="HeartRate"/>, whose samples are all null on exactly those records.
    /// </remarks>
    public double? AverageHeartRate { get; init; } = AverageHeartRate;

    public int? LowestHeartRate { get; init; } = LowestHeartRate;
    public int? AverageHrv { get; init; } = AverageHrv;
    public double? AverageBreath { get; init; } = AverageBreath;
    public int? ReadinessScoreDelta { get; init; } = ReadinessScoreDelta;
    public int? SleepScoreDelta { get; init; } = SleepScoreDelta;
    public string? AlgorithmVersion { get; init; } = AlgorithmVersion;
    public string? AnalysisReason { get; init; } = AnalysisReason;

    /// <summary>
    /// The sleep stage per five-minute slot, as Oura's own digit-per-slot encoding.
    /// </summary>
    /// <remarks>
    /// Kept verbatim rather than decoded into typed stages. The digit-to-stage mapping is
    /// documented by Oura but not derivable from the payloads, so decoding on the way in would
    /// bake an unverifiable assumption into storage; a decoder belongs wherever a reader needs
    /// stages, and can be corrected without a migration. The string is also not safely
    /// index-aligned with <see cref="HeartRate"/> — the two lengths differ by one slot in a
    /// fifth of the archived records.
    /// </remarks>
    public string Hypnogram { get; init; } = Require.Present(Hypnogram, nameof(Hypnogram));

    public OuraSampleSeries HeartRate { get; init; } = Require.Present(HeartRate, nameof(HeartRate));
    public OuraSampleSeries Hrv { get; init; } = Require.Present(Hrv, nameof(Hrv));

    /// <summary>
    /// Whether this is the period Oura treats as the night's main sleep, as opposed to a nap or a
    /// stretch of rest. Defined once here so no reader spells the comparison itself.
    /// </summary>
    public bool IsMainSleep => Kind == MainSleepKind;

    public const string MainSleepKind = "long_sleep";
}

/// <summary>
/// One workout the ring detected or the athlete logged on it — training load Lionheart did not
/// record itself.
/// </summary>
/// <remarks>
/// Distance is in meters and calories in kcal, both absent on the activities Oura cannot measure
/// them for (housework and walking account for most of it).
/// </remarks>
public sealed record WorkoutReading(
    Period Period,
    string Activity,
    string Intensity,
    string Source,
    string? Label,
    double? Calories,
    double? DistanceMeters)
{
    public Period Period { get; init; } = Require.Present(Period, nameof(Period));
    public string Activity { get; init; } = Require.Present(Activity, nameof(Activity));
    public string Intensity { get; init; } = Require.Present(Intensity, nameof(Intensity));

    /// <summary>Whether the athlete entered this workout or confirmed one the ring proposed.</summary>
    public string Source { get; init; } = Require.Present(Source, nameof(Source));

    public string? Label { get; init; } = Label;
    public double? Calories { get; init; } = Calories;
    public double? DistanceMeters { get; init; } = DistanceMeters;
}

/// <summary>A guided session the athlete ran on the ring: meditation, breathing, rest.</summary>
public sealed record SessionReading(
    Period Period,
    string Kind,
    string? Mood,
    OuraSampleSeries HeartRate,
    OuraSampleSeries Hrv,
    OuraSampleSeries MotionCount)
{
    public Period Period { get; init; } = Require.Present(Period, nameof(Period));
    public string Kind { get; init; } = Require.Present(Kind, nameof(Kind));
    public string? Mood { get; init; } = Mood;
    public OuraSampleSeries HeartRate { get; init; } = Require.Present(HeartRate, nameof(HeartRate));
    public OuraSampleSeries Hrv { get; init; } = Require.Present(Hrv, nameof(Hrv));
    public OuraSampleSeries MotionCount { get; init; } = Require.Present(MotionCount, nameof(MotionCount));
}

/// <summary>
/// Something the athlete tagged on the ring — sauna, alcohol, travel, a late coffee.
/// </summary>
/// <remarks>
/// The closest thing in the whole Oura surface to the athlete's own account of a day, which is why
/// a collection of 89 records earns a table: a bad night explains itself when the same night
/// carries <c>tag_sleep_alcohol</c> and <c>tag_sleep_late_hotbath</c>.
/// </remarks>
public sealed record TagReading(Period Period, IReadOnlyList<string> Codes, string? Text)
{
    /// <summary>A tag marks a moment, so this is always an <see cref="Analysis.Instant"/>.</summary>
    public Period Period { get; init; } = Require.Present(Period, nameof(Period));

    public IReadOnlyList<string> Codes { get; init; } = Require.Present(Codes, nameof(Codes));
    public string? Text { get; init; } = Text;

    /// <summary>
    /// Compared by the codes it carries, not by the list holding them — see
    /// <see cref="OuraSampleSeries.Equals(OuraSampleSeries?)"/> for why the generated equality
    /// will not do.
    /// </summary>
    public bool Equals(TagReading? other) =>
        other is not null
        && Period.Equals(other.Period)
        && Text == other.Text
        && Codes.SequenceEqual(other.Codes);

    public override int GetHashCode() => HashCode.Combine(Period, Text, Codes.Count);
}
