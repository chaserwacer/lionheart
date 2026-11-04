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
        UpNextLine(volume.UpNext, score.Window.To),
        UnderTargetLine(volume),
        string.Join(", ", weeks.Select(week => $"{week.Window.To:yyyy-MM-dd} {week.Value}/{week.OutOf}")));

    private static string Lifting(LiftingPart part) =>
        $"lifting {Points(part.Points)}/{part.OutOf}: "
        + string.Join(", ", part.Families.Select(family => $"{PatternWords.Of(family.Of.Family)} {family.Of.HardSets}/{Target(family.Of.Target)}"))
        + " hard sets";

    /// <summary>"6 (committed 8, 4-week mean 5.5)": this week's target, the standard, and what it grew from.</summary>
    private static string Target(WeeklyTarget target) =>
        $"{Points(target.Target)} (committed {Points(target.Committed)}, "
        + (target.Chronic is { } chronic ? $"4-week mean {Points(chronic)})" : "no 4-week history)");

    private static string Cardio(CardioPart part) => part.Measure switch
    {
        CardioMeasure.Measured measured =>
            $"cardio {Points(part.Points ?? 0)}/{part.OutOf}: "
            + string.Join(", ", part.Zones.Select(zone => $"{BandWords(zone.Of.Band)} {Points(zone.Of.Minutes)}/{Target(zone.Of.Target)} min"))
            + $", {measured.QualitySessions} quality of {measured.Sessions} sessions, ring heart rate on {measured.HeartRateDays}/7 days",
        CardioMeasure.Unknown unknown => $"cardio unknown ({unknown.Reason}), out of the score",
        _ => throw new UnreachableException(),
    };

    private static string Progress(ProgressPart part) =>
        $"progress {Points(part.Points)}/{part.OutOf}: " + string.Join("; ", part.Families.Select(family => Priority(family.Of)));

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
        $"consistency {Points(part.Points)}/{part.OutOf}: sessions per 7-day block {string.Join(",", part.Blocks.Select(block => block.Of.Sessions))} (newest first) vs {part.Target}";

    /// <remarks>
    /// The due families and the day's own lifting are two separate statements, so the model cannot
    /// read "already lifted today" as "these families were trained today".
    /// </remarks>
    private static string UpNextLine(Balance.UpNext upNext, DateOnly day) => upNext switch
    {
        Balance.UpNext.Train train =>
            $"next: {string.Join(" and ", train.Families.Select(PatternWords.Of))} "
            + (train.LastTrained is { } last ? $"(last trained {last:yyyy-MM-dd})" : "(never trained)")
            + (train.TrainedToday
                ? $"; {day:yyyy-MM-dd} already has hard sets logged, so this is the session after it"
                : $"; no hard sets logged on {day:yyyy-MM-dd}")
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

    private static string BandWords(ZoneBand band) => band switch
    {
        ZoneBand.Zone2 => "zone 2",
        ZoneBand.Quality => "quality (zone 4-5 work bouts)",
        _ => throw new UnreachableException(),
    };

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
