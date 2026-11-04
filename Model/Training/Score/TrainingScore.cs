using System.Text.Json.Serialization;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Balance;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Focus;

namespace lionheart.Model.Training.Score;

/// <summary>
/// Lionheart's own training score for the seven days ending on a day: lifting volume across push,
/// pull and legs, zone-2 and high-intensity cardio, progress on the priority lifts, and
/// consistency, each stated as the measurements it was read from.
/// </summary>
/// <remarks>
/// Built to be earned rather than handed out: every part is a measurement against a target that a
/// good week of this athlete's reaches only in part, so a decent lifting week reads in the 50s and
/// 60s and 90 takes everything at once, a new best included. A part with no data is unknown, never
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

/// <summary>Hard sets of push, pull and legs against eight each. Over-doing one family does not pay for another.</summary>
public sealed record LiftingPart(double Points, int OutOf, IReadOnlyList<FamilyVolume> Families);

/// <summary>Zone-2 and high-intensity minutes from cardio sessions, or why they cannot be read.</summary>
public sealed record CardioPart(double? Points, int OutOf, CardioMeasure Measure);

/// <summary>Each family's priority lift over the window, against its own recent best.</summary>
public sealed record ProgressPart(double Points, int OutOf, IReadOnlyList<FamilyPriority> Families);

/// <summary>Sessions in each of the last four seven-day blocks, the window's own first.</summary>
public sealed record ConsistencyPart(double Points, int OutOf, IReadOnlyList<int> SessionsPerBlock, int Target);

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
    public const int HighIntensityPoints = 10;
    public const int ProgressPoints = 20;
    public const int ConsistencyPoints = 20;

    /// <summary>The physical-activity guidelines' 150 moderate minutes a week, read as zone 2.</summary>
    public const int Zone2Minutes = 150;

    /// <summary>Twenty minutes at 80 % of maximum or above: one short hard session.</summary>
    public const int HighIntensityMinutes = 20;

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
