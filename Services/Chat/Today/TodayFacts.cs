using lionheart.Model.Cardio;
using lionheart.Model.Training.Patterns;

namespace lionheart.Services.Chat.Today;

/// <summary>
/// The athlete's clock: the instant a message arrives, on the wall clock he lives by, and the days
/// it puts him in.
/// </summary>
/// <remarks>
/// Every "today" the chat states is read from this, never from UTC. The server runs on his own
/// machine, so its zone is his; a UTC day ends at seven in the evening in his winter, which is when
/// he trains and when he asks about it.
/// </remarks>
public sealed record AthleteNow(DateTimeOffset Local)
{
    public static AthleteNow Of(TimeProvider clock) => new(clock.GetLocalNow());

    public DateOnly Today => DateOnly.FromDateTime(Local.DateTime);

    public DateOnly Yesterday => Today.AddDays(-1);

    /// <summary>The Monday the week runs from, matching the score's Monday-to-Sunday weeks.</summary>
    public DateOnly WeekStart => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
}

/// <summary>Everything the briefing states, read once and rendered by <see cref="TodayBriefing"/>.</summary>
/// <param name="Sessions">Completed sessions from the earlier of the week's Monday and yesterday, oldest first.</param>
/// <param name="Cardio">Cardio sessions over the same days, as the cardio report counts them.</param>
/// <param name="MaxHeartRate">The maximum the cardio report draws its zones from; absent without a usable age.</param>
/// <param name="Nights">Main sleeps filed under today and yesterday, the newest first; absent when not synced.</param>
/// <param name="Score">What the score tool answers for the seven days ending today, verbatim; empty when it had nothing.</param>
public sealed record TodayFacts(
    AthleteNow Now,
    IReadOnlyList<SessionFact> Sessions,
    IReadOnlyList<CardioFact> Cardio,
    MaxHeartRate? MaxHeartRate,
    LoadFact Load,
    IReadOnlyList<NightFact> Nights,
    NightBaseline Baseline,
    ReadinessFact? Readiness,
    WeekMeans Means,
    WellnessFact? Wellness,
    IReadOnlyList<InjuryFact> Injuries,
    IReadOnlyList<PlannedFact> Planned,
    string Score);

/// <summary>One completed session: when, how long, and the lifting in it.</summary>
/// <param name="Lifts">Each movement's hard sets, in the order first performed; empty for a session of only cardio sets.</param>
/// <param name="Difficulty">His own 1-5 rating of how hard the session was, when he gave one.</param>
/// <param name="Fatigue">His own 1-5 rating of his accumulated fatigue at the session, when he gave one.</param>
public sealed record SessionFact(
    DateOnly Day,
    DateTimeOffset Start,
    TimeSpan Duration,
    IReadOnlyList<LiftFact> Lifts,
    bool HasCardioSets,
    int? Difficulty,
    int? Fatigue)
{
    public int HardSets => Lifts.Sum(lift => lift.HardSets);
}

/// <param name="TopSet">The heaviest hard set as performed, e.g. "185 lb × 5".</param>
public sealed record LiftFact(string Movement, PatternFamily? Family, int HardSets, string TopSet);

/// <summary>One cardio session as the cardio report counted it, with the zone minutes that carry the score.</summary>
/// <param name="ZoneTwo">Minutes in zone 2; absent when the heart rate was not recorded.</param>
/// <param name="ZoneFourPlus">Minutes at zone 4 or above; absent when the heart rate was not recorded.</param>
public sealed record CardioFact(
    DateOnly Day,
    DateTimeOffset Start,
    double Minutes,
    string Activity,
    int? AverageBpm,
    double? ZoneTwo,
    double? ZoneFourPlus);

/// <summary>Hard sets and sessions in the last seven days against the weekly mean of the last 28.</summary>
public sealed record LoadFact(int HardSetsLast7, double HardSetsWeeklyMean28, int SessionsLast7, double SessionsWeeklyMean28);

/// <summary>One night, filed by Oura under the day he woke.</summary>
public sealed record NightFact(
    DateOnly Day,
    DateTimeOffset Bedtime,
    DateTimeOffset Wake,
    int TotalSleepSeconds,
    int Efficiency,
    int? LowestHeartRate,
    int? AverageHrv,
    int? SleepScore);

/// <summary>The nights before last night, as the measure of what is normal for him.</summary>
public sealed record NightBaseline(double? AverageHrv, double? LowestHeartRate, int Nights);

/// <summary>Today's readiness and the contributors that bear on recovery, each Oura's own 1-100 score.</summary>
/// <param name="UsualLow">The tenth percentile of the last 90 days' readiness, when there are enough days.</param>
public sealed record ReadinessFact(
    int? Score,
    int? HrvBalance,
    int? RestingHeartRate,
    double? TemperatureDeviation,
    int? UsualLow,
    int? UsualHigh);

public sealed record WeekMeans(double? Readiness, double? SleepScore, int Days);

public sealed record WellnessFact(DateOnly Date, double Overall, int Mood, int Energy, int Stress, int Motivation);

public sealed record InjuryFact(string Name, int? LastPain);

public sealed record PlannedFact(DateOnly Day, IReadOnlyList<string> Movements);
