using System.Diagnostics;
using System.Globalization;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Score;

/// <summary>
/// The training score, the week's volume and the weeks before as a handful of lines, for the chat
/// model.
/// </summary>
/// <remarks>
/// Lines rather than the screen's records because the model reads every token it is handed, and the
/// full score with its twelve-week series, sixteen muscles and nine patterns runs to thousands. The
/// lines keep every measurement the score was read from and name only what is under target.
/// </remarks>
public sealed record TrainingScoreDigest(
    string Window,
    string Score,
    IReadOnlyList<string> Parts,
    string UpNext,
    string UnderTarget,
    string Weeks)
{
    public static TrainingScoreDigest Of(TrainingScore score, TrainingVolume volume, IReadOnlyList<WeeklyScore> weeks) => new(
        $"{score.Window.From:yyyy-MM-dd}..{score.Window.To:yyyy-MM-dd}",
        score.Unknown.Count is 0
            ? $"{score.Value} of 100"
            : $"{score.Value} of {score.OutOf} that could be read; unknown: {string.Join(", ", score.Unknown.Select(Word))}",
        [Lifting(score.Lifting), Cardio(score.Cardio), Progress(score.Progress), Consistency(score.Consistency)],
        UpNextLine(volume.UpNext),
        UnderTargetLine(volume),
        string.Join(", ", weeks.Select(week => $"{week.Window.To:yyyy-MM-dd} {week.Value}/{week.OutOf}")));

    private static string Lifting(LiftingPart part) =>
        $"lifting {Points(part.Points)}/{part.OutOf}: "
        + string.Join(", ", part.Families.Select(family => $"{PatternWords.Of(family.Family)} {family.HardSets}/{family.Target}"))
        + " hard sets";

    private static string Cardio(CardioPart part) => part.Measure switch
    {
        CardioMeasure.Measured measured =>
            $"cardio {Points(part.Points ?? 0)}/{part.OutOf}: zone 2 {Points(measured.Zone2Minutes)}/{measured.Zone2Target} min, "
            + $"zones 4-5 {Points(measured.HighIntensityMinutes)}/{measured.HighIntensityTarget} min, "
            + $"{measured.Sessions} sessions, heart rate on {measured.HeartRateDays}/7 days",
        CardioMeasure.Unknown unknown => $"cardio unknown ({unknown.Reason}), out of the score",
        _ => throw new UnreachableException(),
    };

    private static string Progress(ProgressPart part) =>
        $"progress {Points(part.Points)}/{part.OutOf}: " + string.Join("; ", part.Families.Select(Priority));

    private static string Priority(FamilyPriority family) => family.Lift switch
    {
        PriorityLift.NoLift => $"{PatternWords.Of(family.Family)} no lift",
        PriorityLift.Chosen chosen =>
            $"{PatternWords.Of(family.Family)} {chosen.Name} ({Choice(chosen.Choice)}, {chosen.SessionsLastFourWeeks} sessions/4 wk) "
            + Standing(chosen.Progress),
        _ => throw new UnreachableException(),
    };

    private static string Choice(PriorityChoice choice) => choice switch
    {
        PriorityChoice.ByPin pinned => $"pinned to {pinned.Pin.Ends:yyyy-MM-dd}",
        PriorityChoice.MostTrained => "not pinned, most trained",
        _ => throw new UnreachableException(),
    };

    private static string Standing(LiftProgress progress) => progress switch
    {
        LiftProgress.NotTrained => "not trained",
        LiftProgress.NoEstimate => "trained, no e1RM",
        LiftProgress.FirstBack back => $"first in 12 wk, e1RM {Points(back.Best)} {Unit(back.Unit)}{Load(back.OnSystemLoad)}",
        LiftProgress.Compared compared =>
            $"{StandingWord(compared.Standing)}, e1RM {Points(compared.Best)} vs {Points(compared.PriorBest)} {Unit(compared.Unit)}{Load(compared.OnSystemLoad)}",
        _ => throw new UnreachableException(),
    };

    private static string StandingWord(ProgressStanding standing) => standing switch
    {
        ProgressStanding.LifetimeBest => "lifetime best",
        ProgressStanding.NewBest => "new 12-wk best",
        ProgressStanding.Held => "held within 5%",
        ProgressStanding.Below => "below",
        _ => throw new UnreachableException(),
    };

    private static string Consistency(ConsistencyPart part) =>
        $"consistency {Points(part.Points)}/{part.OutOf}: sessions per 7-day block {string.Join(",", part.SessionsPerBlock)} (newest first) vs {part.Target}";

    private static string UpNextLine(Balance.UpNext upNext) => upNext switch
    {
        Balance.UpNext.Train train =>
            string.Join(" and ", train.Families.Select(PatternWords.Of))
            + (train.LastTrained is { } last ? $", last trained {last:yyyy-MM-dd}" : ", never trained")
            + (train.TrainedToday ? " (trained today; for the next session)" : "")
            + Readiness(train.Readiness),
        Balance.UpNext.NothingLogged => "nothing classified logged",
        _ => throw new UnreachableException(),
    };

    private static string Readiness(ReadinessNote note) => note switch
    {
        ReadinessNote.Low low => $"; Oura's readiness score {low.OuraScore}, under its usual {low.UsualLow}-{low.UsualHigh}",
        ReadinessNote.Usual or ReadinessNote.NotRead => "",
        _ => throw new UnreachableException(),
    };

    private static string UnderTargetLine(TrainingVolume volume)
    {
        var patterns = volume.Patterns
            .Where(pattern => pattern.Target is { } target && pattern.HardSets < target)
            .Select(pattern => $"{PatternWords.Of(pattern.Pattern)} {pattern.HardSets}/{pattern.Target}");
        var muscles = volume.Muscles
            .Where(muscle => muscle.Sets < muscle.Target)
            .Select(muscle => $"{PatternWords.Of(muscle.Muscle)} {Points(muscle.Sets)}/{muscle.Target}");
        return $"patterns: {string.Join(", ", patterns)}; muscles (secondary counts half): {string.Join(", ", muscles)}";
    }

    private static string Word(ScorePart part) => part.ToString().ToLowerInvariant();

    private static string Load(bool onSystemLoad) => onSystemLoad ? " system load" : "";

    private static string Unit(WeightUnit unit) => unit switch
    {
        WeightUnit.Pounds => "lb",
        WeightUnit.Kilograms => "kg",
        _ => throw new UnreachableException(),
    };

    private static string Points(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
