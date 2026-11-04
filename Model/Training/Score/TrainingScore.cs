using System.Text.Json.Serialization;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Balance;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;

namespace lionheart.Model.Training.Score;

/// <summary>
/// Lionheart's own training score for the seven days ending on a day: lifting volume across push,
/// pull and legs, zone-2 and quality cardio, progress on the priority lifts, and consistency, each
/// stated as the measurements it was read from.
/// </summary>
/// <remarks>
/// Built to be earned rather than handed out. The volume measurements are read against targets
/// that grow from his recent weeks (<see cref="Progression"/>), but half of every one is kept for
/// the committed standard, so a comeback week that meets its ramp reads well and 90 still takes a
/// committed week, a new best included. A part with no data is unknown, never
/// zero: it is named, its points are out of reach rather than lost, and the value is what the known
/// parts earned. So a week with the ring off can never outscore the same week with it on.
/// </remarks>
/// <param name="Value">The points the known parts earned, out of 100.</param>
/// <param name="OutOf">The points that could be read: 100, less the weight of any part unknown.</param>
/// <param name="Unknown">The parts whose data is missing; empty for a whole score.</param>
public sealed record TrainingScore(
    DayRange Window,
    int Value,
    int OutOf,
    IReadOnlyList<ScorePart> Unknown,
    LiftingPart Lifting,
    CardioPart Cardio,
    ProgressPart Progress,
    ConsistencyPart Consistency);

/// <summary>The four parts of the score, by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScorePart>))]
public enum ScorePart
{
    [JsonStringEnumMemberName("lifting")] Lifting,
    [JsonStringEnumMemberName("cardio")] Cardio,
    [JsonStringEnumMemberName("progress")] Progress,
    [JsonStringEnumMemberName("consistency")] Consistency,
}

/// <summary>One measurement of a part, with the points it earned out of the points it could.</summary>
/// <remarks>
/// Every figure the score is read from carries its own contribution, so the breakdown is the
/// server's arithmetic drawn rather than a client's second reading of the rules.
/// </remarks>
public sealed record Scored<T>(T Of, double Points, double OutOf);

/// <summary>
/// Hard sets of push, pull and legs, each against its week's target. Over-doing one family does
/// not pay for another.
/// </summary>
public sealed record LiftingPart(double Points, int OutOf, IReadOnlyList<Scored<FamilyTarget>> Families);

/// <summary>One family's hard sets over the window against the week's target for it.</summary>
public sealed record FamilyTarget(PatternFamily Family, int HardSets, WeeklyTarget Target, DateOnly? LastTrained);

/// <summary>Zone-2 and quality minutes from cardio sessions, or why they cannot be read.</summary>
/// <param name="Zones">Each band against its target; empty when the measure is unknown.</param>
public sealed record CardioPart(double? Points, int OutOf, CardioMeasure Measure, IReadOnlyList<Scored<ZoneTarget>> Zones);

/// <summary>Minutes in a band against the week's target for it.</summary>
public sealed record ZoneTarget(ZoneBand Band, double Minutes, WeeklyTarget Target);

/// <summary>The two kinds of cardio minutes the score reads.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ZoneBand>))]
public enum ZoneBand
{
    /// <summary>60–70 % of maximum: the aerobic base.</summary>
    [JsonStringEnumMemberName("zone2")] Zone2,

    /// <summary>
    /// Work bouts at zone 4 or above (80 % of maximum and over) in endurance sessions that held a
    /// session's worth of them: threshold and interval work (<see cref="QualityWork"/>).
    /// </summary>
    [JsonStringEnumMemberName("quality")] Quality,
}

/// <summary>Each family's priority lift over the window, against its own recent best.</summary>
public sealed record ProgressPart(double Points, int OutOf, IReadOnlyList<Scored<FamilyPriority>> Families);

/// <summary>Sessions in each of the last four seven-day blocks, the window's own first.</summary>
public sealed record ConsistencyPart(double Points, int OutOf, IReadOnlyList<Scored<SessionBlock>> Blocks, int Target);

/// <summary>Completed sessions in one seven-day block.</summary>
public sealed record SessionBlock(DayRange Window, int Sessions);

/// <summary>One week's score, for the trend: its value and each part's points.</summary>
public sealed record WeeklyScore(
    DayRange Window, int Value, int OutOf, IReadOnlyList<ScorePart> Unknown,
    double LiftingPoints, double? CardioPoints, double ProgressPoints, double ConsistencyPoints)
{
    public static WeeklyScore Of(TrainingScore score) => new(
        score.Window, score.Value, score.OutOf, score.Unknown,
        score.Lifting.Points, score.Cardio.Points, score.Progress.Points, score.Consistency.Points);
}

/// <summary>The weights and targets the score is read by — one place, so the explanation and the arithmetic agree.</summary>
/// <remarks>
/// Weights sum to 100. Lifting leads because push, pull and legs are how the athlete judges his
/// gym health; cardio and progress are the parts Oura's activity score never asked of him;
/// consistency is his first goal, read over four weeks because one week cannot show it.
/// </remarks>
public static class ScoreRules
{
    public const int LiftingPoints = 35;
    public const int CardioPoints = 25;
    public const int Zone2Points = 15;
    public const int QualityPoints = 10;
    public const int ProgressPoints = 20;
    public const int ConsistencyPoints = 20;

    /// <summary>
    /// A committed week of zone 2: the physical-activity guidelines' 150 moderate minutes, from an
    /// hour's floor — two half-hour sessions — in five-minute steps.
    /// </summary>
    public static readonly Standard Zone2 = new(150, 60, 5);

    /// <summary>
    /// A committed week of quality work: forty minutes in work bouts, two sessions of the 16 to 32
    /// minutes the interval trials accumulated (Helgerud et al. 2007; Seiler et al. 2013) — the
    /// one or two hard sessions a week that polarised and pyramidal programmes hold (Seiler 2010;
    /// Casado et al. 2022). The floor is one ten-minute quality session.
    /// </summary>
    public static readonly Standard Quality = new(40, QualityWork.LeastSessionMinutes, 5);

    /// <summary>
    /// A committed week of a family: eight hard sets, the volume screen's target. The floor is the
    /// four that buy most of the dose (Pelland 2026).
    /// </summary>
    public static readonly Standard FamilySets = new(VolumeTargets.Family, 4, 1);

    /// <summary>
    /// Three sessions a week: his median in every block he has trained, so a block that simply
    /// does not stop earns the whole part, and a fourth session earns its points in volume instead.
    /// </summary>
    public const int SessionsPerWeek = 3;

    public const int ConsistencyBlocks = 4;

    /// <summary>
    /// A priority lift's share of its points by how the window's best compares with the twelve
    /// weeks before.
    /// </summary>
    /// <remarks>
    /// Only a new best pays well. With every other part full, holding all three lifts reads 85, so
    /// 90 takes a new best on at least one priority, the progress the owner asked a 90 to mean.
    /// </remarks>
    public const double NewBestShare = 1;
    public const double HeldShare = 0.25;
    public const double FirstBackShare = 0.25;
    public const double BelowShare = 0.1;
}
