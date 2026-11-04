using System.Text.Json.Serialization;
using lionheart.Model.Analysis;

namespace lionheart.Model.Cardio;

/// <summary>
/// The athlete's cardio over a range of days: every session, the weeks they add up to, Oura's
/// fitness estimates, and the comparison that says whether the aerobic base is moving.
/// </summary>
/// <remarks>
/// A derived read model rather than stored documents, built on the server because the input is the
/// all-day heart-rate series — tens of thousands of samples a month — and the answer is a few
/// hundred numbers. Every figure is measured against the athlete's own record; nothing here is a
/// population norm except the age prediction the zones may fall back on, which says so.
/// </remarks>
public sealed record CardioReport(
    DayRange Range,
    MaxHeartRate? MaxHeartRate,
    IReadOnlyList<CardioSession> Sessions,
    IReadOnlyList<CardioWeek> Weeks,
    IReadOnlyList<DayValue> Vo2Max,
    IReadOnlyList<DayValue> CardiovascularAge,
    IReadOnlyList<DayValue> RestingHeartRate,
    AerobicBase AerobicBase,
    IReadOnlyList<ExcludedWorkouts> Excluded)
{
    /// <summary>
    /// The Apple Watch's VO2max estimates over the range, read from Apple Health; <see cref="Vo2Max"/>
    /// is Oura's. Two series because they are two devices' models of the same figure, and a line
    /// drawn through both would read a change of device as a change of fitness.
    /// </summary>
    public IReadOnlyList<DayValue> AppleWatchVo2Max { get; init; } = [];
}

/// <summary>One figure on one day.</summary>
public sealed record DayValue(DateOnly Day, double Value);

/// <summary>
/// One cardio session: a workout the ring saw or a run the athlete logged, and its heart rate.
/// </summary>
public sealed record CardioSession(
    CardioSessionSource Source,
    DateTimeOffset Start,
    DateTimeOffset End,
    double? DistanceMeters,
    SessionHeartRate HeartRate)
{
    public double Minutes => (End - Start).TotalMinutes;
}

/// <summary>Where a cardio session's record comes from.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RingWorkout), "ringWorkout")]
[JsonDerivedType(typeof(LoggedSession), "loggedSession")]
public abstract record CardioSessionSource
{
    private protected CardioSessionSource() { }

    /// <summary>A workout Oura detected or the athlete entered on the ring.</summary>
    public sealed record RingWorkout(Guid OuraId, string Activity, WorkoutCategory Category) : CardioSessionSource;

    /// <summary>
    /// A completed Lionheart session carrying trail-run or distance-and-time sets. Where the ring
    /// saw the same session, this is the record kept: the athlete's own log names it and measures
    /// it. Its heart rate is the recording app's where it was imported from Apple Health with one,
    /// and the ring's over the same clock time otherwise.
    /// </summary>
    /// <param name="IsRecorded">
    /// Imported from Apple Health, so a route and the recording app's own figures stand behind it
    /// (<c>GET /api/training/sessions/{id}/recording</c>).
    /// </param>
    public sealed record LoggedSession(Guid TrainingSessionId, bool IsTrailRun, bool IsRecorded) : CardioSessionSource;
}

/// <summary>Whose samples a session's heart rate was read from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeartRateSource>))]
public enum HeartRateSource
{
    /// <summary>The ring's all-day series over the session's clock time.</summary>
    [JsonStringEnumMemberName("ring")] Ring,

    /// <summary>The samples the app that recorded the workout wrote to Apple Health.</summary>
    [JsonStringEnumMemberName("appleHealth")] AppleHealth,
}

/// <summary>What the heart rate held across a session — the recording app's, or the ring's all-day series.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NotRecorded), "notRecorded")]
[JsonDerivedType(typeof(Recorded), "recorded")]
public abstract record SessionHeartRate
{
    private protected SessionHeartRate() { }

    /// <summary>No sample falls inside the session — the ring was off, or the series is not synced that far back.</summary>
    public sealed record NotRecorded : SessionHeartRate;

    /// <param name="Coverage">
    /// The fraction of the session's clock time the samples stand for, 0 to 1. Below about a half,
    /// the zone minutes describe part of the session rather than the whole of it.
    /// </param>
    /// <param name="Zones">Absent only when there is no maximum heart rate to draw zones from.</param>
    /// <param name="Source">Whose samples these are.</param>
    public sealed record Recorded(
        int AverageBpm,
        int PeakBpm,
        double Coverage,
        ZoneMinutes? Zones,
        HeartRateSource Source) : SessionHeartRate;
}

/// <summary>
/// One week of cardio, Monday to Sunday: how many sessions, how long, and the zones they were in.
/// </summary>
/// <remarks>
/// A week with no session is present with zeros. For this athlete the weeks that do not happen are
/// the pattern worth seeing, so a gap must read as a week of nothing rather than as no week.
/// </remarks>
/// <param name="DaysInRange">
/// How many of the week's seven days fall inside the range — fewer at a range's ends, so a short
/// week reads as short rather than as a light one.
/// </param>
public sealed record CardioWeek(
    DateOnly WeekStarting,
    int DaysInRange,
    int Sessions,
    double Minutes,
    ZoneMinutes Zones);

/// <summary>Oura workouts in the range that are not cardio, counted by category.</summary>
/// <remarks>
/// Stated rather than silently dropped, so a reader can see how much the ring filed that is not
/// training and judge the line drawn.
/// </remarks>
public sealed record ExcludedWorkouts(WorkoutCategory Category, WorkoutExclusion Reason, int Count);

/// <summary>Why a ring workout is not a cardio session of its own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkoutExclusion>))]
public enum WorkoutExclusion
{
    /// <summary>Lies at least half inside a logged run, which is the same session and already counted.</summary>
    [JsonStringEnumMemberName("sameAsLoggedRun")] SameAsLoggedRun,

    /// <summary>Lies at least half inside a logged lifting session: the gym as the ring saw it.</summary>
    [JsonStringEnumMemberName("insideLoggedLifting")] InsideLoggedLifting,

    /// <summary>A walk shorter than <see cref="WorkoutCategories.SessionWalk"/>.</summary>
    [JsonStringEnumMemberName("shortWalk")] ShortWalk,

    [JsonStringEnumMemberName("strength")] Strength,

    /// <summary>Housework, yard work, stretching: movement that is not training.</summary>
    [JsonStringEnumMemberName("notTraining")] NotTraining,

    /// <summary>An activity this application does not yet place.</summary>
    [JsonStringEnumMemberName("unclassified")] Unclassified,

    /// <summary>Filed as a moment rather than a span, so it has no duration to count.</summary>
    [JsonStringEnumMemberName("noDuration")] NoDuration,
}

/// <summary>
/// A cardio session placed on the clock, before any heart rate is read for it.
/// </summary>
/// <remarks>
/// What gathering the sessions produces, so the heart rate is read only for the days these touch
/// and the habits report can place them on a day without a heart rate it never asked for.
/// </remarks>
public sealed record GatheredSession(
    CardioSessionSource Source,
    DateTimeOffset Start,
    DateTimeOffset End,
    double? DistanceMeters);

/// <summary>
/// Whether the aerobic base is moving: each figure over the last four weeks against the eight before.
/// </summary>
/// <remarks>
/// Twelve weeks because VO2max and resting heart rate move over months rather than days, and a
/// four-week window against the eight before it is long enough to be a change and short enough to
/// be this training block. A figure with no data in either window is absent rather than zero.
/// </remarks>
public sealed record AerobicBase(
    DayRange Recent,
    DayRange Before,
    Comparison Zone2MinutesPerWeek,
    Comparison CardioMinutesPerWeek,
    Comparison Vo2Max,
    Comparison RestingHeartRate,
    Comparison CardiovascularAge)
{
    /// <summary>The Apple Watch's VO2max over the two windows; <see cref="Vo2Max"/> is Oura's.</summary>
    public Comparison AppleWatchVo2Max { get; init; } = new(null, null);

    public const int RecentWeeks = 4;
    public const int BeforeWeeks = 8;
}

/// <summary>A figure in a recent window and the window before it.</summary>
public sealed record Comparison(double? Recent, double? Before)
{
    public double? Change => Recent - Before;
}
