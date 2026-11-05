using System.Diagnostics;
using System.Globalization;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Load;
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
/// <param name="Adjusted">
/// The adjusted score and the days behind it, with every adjusted target, or that it is the score
/// itself when nothing was marked non-training.
/// </param>
/// <param name="Aims">Each family's priority lift with its next top set and the reasons for it.</param>
/// <param name="Advice">The calendar week's one piece of advice, word for word as the app shows it, so the chat agrees with it.</param>
/// <param name="Load">The window's load against the four weeks before: hard sets, any deload, cardio and running ramps, everyday load.</param>
/// <param name="Injuries">The active injuries the week manages around: the families they pause and what led up to each.</param>
public sealed record TrainingScoreDigest(
    string Window,
    string Score,
    IReadOnlyList<string> Parts,
    string Adjusted,
    string Aims,
    string UpNext,
    string UnderTarget,
    string Weeks,
    string Advice,
    string Load,
    string Injuries)
{
    public static TrainingScoreDigest Of(TrainingScore score, TrainingVolume volume, IReadOnlyList<WeeklyScore> weeks) => new(
        $"{score.Window.From:yyyy-MM-dd}..{score.Window.To:yyyy-MM-dd}",
        score.Unknown.Count is 0
            ? $"{score.Value} of 100"
            : $"{score.Value} of {score.OutOf} that could be read; unknown: {string.Join(", ", score.Unknown.Select(Word))}",
        [Lifting(score.Lifting), Cardio(score.Cardio), Progress(score.Progress), Consistency(score.Consistency)],
        AdjustedLine(score),
        string.Join("; ", score.Expectations.Priorities.Select(priority => $"{PatternWords.Of(priority.Family)}: {Expectations.PriorityAimWords.Line(priority)}")),
        UpNextLine(volume.UpNext, score.Window.To),
        UnderTargetLine(volume),
        string.Join(", ", weeks.Select(week => $"{week.Window.To:yyyy-MM-dd} {week.Value}/{week.OutOf}")),
        AdviceLine(score.Expectations.Advice),
        LoadLine(score.Expectations.Load),
        InjuriesLine(score.Expectations.Injuries));

    /// <summary>"week of 2026-11-02: Hold your running this week — reasons", the banner's own words.</summary>
    private static string AdviceLine(WeeklyAdvice advice) =>
        $"week of {advice.WeekStarting:yyyy-MM-dd}: {advice.Headline} — {string.Join(" ", advice.Reasons)}";

    private static string LoadLine(WeekLoad load)
    {
        var lifting = $"hard sets {Ratio(load.Lifting.HardSets, Points)} ({RampWord(load.Lifting.Ramp)}); deload: "
                      + (load.Lifting.Deload is Deload.Suggested suggested
                          ? $"suggested this week ({(suggested.Following ? "following it" : "trained through it")}) — {string.Join(" ", suggested.Reasons)}"
                          : $"not suggested ({load.Lifting.Deload.BuildingWeeks} building weeks in a row)");
        var cardio = load.Cardio switch
        {
            CardioLoad.Read read =>
                $"cardio minutes {Ratio(read.Minutes.Ratio, Points)} ({RampWord(read.Minutes.Standing)}); running {Ratio(read.RunningMeters.Ratio, Imperial.Miles)} ({RampWord(read.RunningMeters.Standing)})",
            CardioLoad.Unread => "cardio unread",
            _ => throw new UnreachableException(),
        };
        var far = load.Everyday.Days
            .Where(day => day.IsFarAbove)
            .Select(day => $"{day.Day:yyyy-MM-dd} {day.Ratio.ToString("0.0", CultureInfo.InvariantCulture)}x")
            .ToList();
        var everyday = far.Count is 0
            ? "everyday load: no day far above his usual"
            : $"everyday load far above his usual on {string.Join(", ", far)}"
              + (load.Everyday.EasedDays > 0 ? $", easing the adjusted targets by {Points(load.Everyday.EasedDays)} days" : "");
        return $"{lifting}; {cardio}; {everyday}";
    }

    private static string InjuriesLine(IReadOnlyList<ManagedInjury> injuries) =>
        injuries.Count is 0
            ? "none active"
            : string.Join("; ", injuries.Select(injury =>
                $"{injury.Name} since {injury.Since:yyyy-MM-dd}"
                + (injury.Families.Count > 0 ? $", pausing {string.Join(" and ", injury.Families.Select(PatternWords.Of))}" : ", no family named")
                + string.Concat(injury.Preceding.Select(trend =>
                    $", {PatternWords.Of(trend.Family)} {Points(trend.Recent)} hard sets a week in the 4 weeks before it vs {Points(trend.Earlier)} in the 8 before"))));

    /// <summary>"31 vs a usual 24 (1.29x)", or that nothing before could be read, each figure in the words given.</summary>
    private static string Ratio(LoadRatio ratio, Func<double, string> figure) => ratio switch
    {
        LoadRatio.Compared compared =>
            $"{figure(compared.Acute)} vs a usual {figure(compared.Chronic)}"
            + (compared.Chronic > 0 ? $" ({compared.Ratio.ToString("0.00", CultureInfo.InvariantCulture)}x)" : ""),
        LoadRatio.NothingBefore nothing => $"{figure(nothing.Acute)}, nothing in the 4 weeks before",
        _ => throw new UnreachableException(),
    };

    private static string RampWord(RampStanding standing) => standing switch
    {
        RampStanding.Unjudged => "unjudged",
        RampStanding.Within => "within his usual",
        RampStanding.Steep => "steep, 1.3x or more",
        RampStanding.Spike => "a spike, 1.5x or more",
        _ => throw new UnreachableException(),
    };

    /// <summary>
    /// "adjusted 84 of 100; 5 of 7 days available (non-training 2026-11-03 travel, …); adjusted
    /// targets: push 3/6 hard sets, …", or that it is the score when the days asked for nothing less.
    /// </summary>
    private static string AdjustedLine(TrainingScore score)
    {
        var expected = score.Expectations;
        var off = expected.Availability.Unavailable;
        var same = score.Adjusted.Value == score.Value && score.Adjusted.OutOf == score.OutOf;
        if (off.Count is 0 && same) return "adjusted: the same as the score, no day marked non-training";

        var targets = expected.Families
            .Select(family => $"{PatternWords.Of(family.Family)} {Points(family.Sets.Done)}/{Points(family.Sets.Adjusted.Target)} hard sets")
            .Concat(expected.Cardio.Zones.Select(zone => $"{BandWords(zone.Band)} {Points(zone.Minutes.Done)}/{Points(zone.Minutes.Adjusted.Target)} min"))
            .Append($"sessions {Points(expected.Sessions.Done)}/{Points(expected.Sessions.Adjusted.Target)}");
        var days = off.Count is 0
            ? "every day of this week available, days marked off in the weeks before"
            : $"{expected.Availability.AvailableDays} of {expected.Availability.Days.Count} days available (non-training "
              + string.Join(", ", off.Select(day => $"{day.Day:yyyy-MM-dd}{(day.Note.Length > 0 ? $" {day.Note}" : "")}")) + ")";
        return $"adjusted {score.Adjusted.Value} of {score.Adjusted.OutOf} (every target prorated to the days available, less half a day for each day of everyday load far above usual once there are two; a suggested deload lowers a family, an injury pauses it); {days}; adjusted targets: {string.Join(", ", targets)}";
    }

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
        LiftProgress.FirstBack back => $"first in 12 wk, e1RM {Points(back.Best)} {Unit(back.Unit)}{SystemLoad(back.OnSystemLoad)}",
        LiftProgress.Compared compared =>
            $"{StandingWord(compared.Standing)}, e1RM {Points(compared.Best)} vs {Points(compared.PriorBest)} {Unit(compared.Unit)}{SystemLoad(compared.OnSystemLoad)}",
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
        var paused = volume.Families.Where(family => family.Paused).Select(family => family.Family).ToHashSet();
        var patterns = volume.Patterns
            .Where(pattern => pattern.Target is { } target && pattern.HardSets < target)
            .Where(pattern => MovementPatterns.FamilyOf(pattern.Pattern) is not { } family || !paused.Contains(family))
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

    private static string SystemLoad(bool onSystemLoad) => onSystemLoad ? " system load" : "";

    private static string Unit(WeightUnit unit) => unit switch
    {
        WeightUnit.Pounds => "lb",
        WeightUnit.Kilograms => "kg",
        _ => throw new UnreachableException(),
    };

    private static string Points(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
