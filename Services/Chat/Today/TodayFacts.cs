using lionheart.Model.Cardio;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;

namespace lionheart.Services.Chat.Today;

/// <summary>Everything the briefing states, read once and rendered by <see cref="TodayBriefing"/>.</summary>
/// <param name="Sessions">Completed sessions from the earlier of the week's Monday and yesterday, oldest first.</param>
/// <param name="Cardio">The cardio report over the same days, or that it could not be read.</param>
/// <param name="Nights">Main sleeps filed under today and yesterday, the newest first; absent when not synced.</param>
/// <param name="NonTraining">The days from today through the next six he has marked non-training, oldest first.</param>
/// <param name="Everyday">Yesterday's and today's everyday load against his usual day, where the ring read them.</param>
public sealed record TodayFacts(
    AthleteNow Now,
    IReadOnlyList<SessionFact> Sessions,
    CardioDays Cardio,
    LoadFact Load,
    IReadOnlyList<NightFact> Nights,
    NightBaseline Baseline,
    ReadinessFact? Readiness,
    WeekMeans Means,
    WellnessFact? Wellness,
    IReadOnlyList<InjuryFact> Injuries,
    IReadOnlyList<PlannedFact> Planned,
    IReadOnlyList<NonTrainingFact> NonTraining,
    EverydayLoad Everyday)
{
    /// <summary>
    /// What the score tool answers for the seven days ending today, verbatim; empty when it had
    /// nothing, and empty on the facts a reader other than the briefing asks for.
    /// </summary>
    public string Score { get; init; } = "";

    /// <summary>What the next-sessions tool answers for today, verbatim; empty as <see cref="Score"/> is.</summary>
    public string Next { get; init; } = "";
}

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

/// <summary>
/// The briefing's days of cardio: what the cardio report counted, or that it could not be read.
/// </summary>
/// <remarks>
/// Two states because the briefing must never state an absence it does not know: a report that
/// failed is not a day without cardio, and the model is told which it is.
/// </remarks>
public abstract record CardioDays
{
    private protected CardioDays() { }

    /// <param name="MaxHeartRate">The maximum the report draws its zones from; absent without a usable age.</param>
    public sealed record Read(IReadOnlyList<CardioFact> Sessions, MaxHeartRate? MaxHeartRate) : CardioDays;

    public sealed record Unread : CardioDays;

    /// <summary>The sessions known to have happened: none when the report could not be read.</summary>
    public IReadOnlyList<CardioFact> Known => this is Read read ? read.Sessions : [];
}

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

/// <summary>Hard sets and sessions in the last seven days against the four seven-day blocks before them.</summary>
public sealed record LoadFact(LoadRatio HardSets, LoadRatio Sessions);

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

/// <summary>The nights before the two the briefing states, as the measure of what is normal for him.</summary>
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

/// <summary>A session planned on a day.</summary>
/// <param name="Movements">
/// What it prescribes: each movement by name, and a freeform prescription in its own words, so a
/// planned run reads as the run rather than as nothing.
/// </param>
public sealed record PlannedFact(DateOnly Day, IReadOnlyList<string> Movements);

/// <summary>A day he has said he will not train, with what he said about it.</summary>
public sealed record NonTrainingFact(DateOnly Day, string Note);
