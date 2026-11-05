using System.Globalization;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Expectations;

namespace lionheart.Services.Training.Load;

/// <summary>
/// A week's load as a function of the log, the cardio and the everyday load: how the window
/// stands against the four weeks before it, and whether its calendar week is offered a deload.
/// </summary>
/// <remarks>
/// <para>
/// Pure, so every threshold is a test. Every ramp is a <see cref="LoadRatio"/> against the same
/// four seven-day blocks the targets grow from, judged steep at 1.3 and a spike at 1.5 once the
/// rise is more than the measure's noise: six hard sets, thirty cardio minutes, three miles.
/// </para>
/// <para>
/// A deload is suggested for a calendar week after a run of building weeks before its Monday. A
/// building week met each family's raw target and held at least 0.9 times the hard sets of the
/// four weeks before it — the load kept up, not a lighter week. Four in a row that strained him
/// — a steep build, its hard sets 1.3 times the four weeks before on average, or tiredness
/// climbing: last week's fatigue or difficulty rated 4 of 5 or more on average, or fatigue up a
/// whole point over the run — or six in a row whatever else, suggests a deload: the
/// four-to-six-week accumulation blocks a deload usually closes (Bell et al. 2023). The
/// suggestion asks each family for 60 % of its target, the same weights; a week of no more than
/// 70 % of his usual hard sets reads as following it.
/// </para>
/// </remarks>
public static class LoadAnalysis
{
    /// <summary>The least share of the four weeks before that a building week holds.</summary>
    public const double BuildingRatio = 0.9;

    /// <summary>Building weeks that suggest a deload when the build was steep or tiredness is climbing.</summary>
    public const int StrainingRun = 4;

    /// <summary>Building weeks that suggest a deload whatever the ratings.</summary>
    public const int LongRun = 6;

    /// <summary>An average fatigue or difficulty rating, of five, that reads tired.</summary>
    public const double TiredRating = 4;

    /// <summary>The share of each family's target a deload week asks.</summary>
    public const double DeloadShare = 0.6;

    /// <summary>The most of his usual week's hard sets a week holds and still reads as a deload.</summary>
    public const double DeloadCeilingShare = 0.7;

    public const double LeastHardSetIncrease = 6;
    public const double LeastCardioMinutesIncrease = 30;

    /// <summary>
    /// The least usual week of cardio a rise is judged against: an hour. Under it a week of cardio
    /// is a start, not a spike, and holding it back would only keep him off cardio; running is
    /// judged from nothing, because a first week of miles is the injury risk the ramp is for.
    /// </summary>
    public const double LeastCardioMinutesChronic = 60;

    /// <summary>Three miles, in meters.</summary>
    public const double LeastRunningMetersIncrease = 3 * 1609.344;

    /// <summary>How much of a day each far-above day of everyday load takes off the week's adjusted targets.</summary>
    public const double EasedDayShare = 0.5;

    /// <summary>The fewest far-above days in a window that ease its targets; one big day moves only the next session.</summary>
    public const int FewestFarAboveDaysToEase = 2;

    /// <summary>The most building weeks counted back, which is all a reason needs to say.</summary>
    private const int MostWeeksCounted = 12;

    public static WeekLoad Of(ExpectationInputs inputs, HardSetCounts sets, DateOnly endingOn)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        var hardSets = HardSetRatio(sets, window);
        return new WeekLoad(
            new LiftingLoad(hardSets, hardSets.Standing(LeastHardSetIncrease), Deload(inputs.Log, sets, endingOn)),
            Cardio(inputs.CardioOf, window),
            Everyday(inputs.Everyday, window));
    }

    /// <summary>The Monday of the calendar week a day falls in.</summary>
    public static DateOnly WeekStarting(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>Every hard set of a block against the four blocks before it.</summary>
    public static LoadRatio HardSetRatio(HardSetCounts sets, DayRange block) =>
        LoadRatio.Of(sets.All(block), ChronicBlocks(block).Select(before => (double?)sets.All(before)));

    /// <summary>The window's cardio minutes and running distance against the four blocks before; unread when its cardio is.</summary>
    public static CardioLoad Cardio(Func<DayRange, CardioMeasure> cardioOf, DayRange window)
    {
        if (cardioOf(window) is not CardioMeasure.Measured measured) return new CardioLoad.Unread();

        var before = ChronicBlocks(window).Select(cardioOf).Select(block => block as CardioMeasure.Measured).ToList();
        var minutes = LoadRatio.Of(measured.Minutes, before.Select(block => block?.Minutes));
        var running = LoadRatio.Of(measured.RunningMeters, before.Select(block => block?.RunningMeters));
        return new CardioLoad.Read(
            new LoadRamp(minutes, minutes.Standing(LeastCardioMinutesIncrease, LeastCardioMinutesChronic)),
            new LoadRamp(running, running.Standing(LeastRunningMetersIncrease)));
    }

    /// <summary>The window's days of everyday load, and the half-days the far-above ones ease, once there are two.</summary>
    public static EverydayWeek Everyday(EverydayLoad everyday, DayRange window)
    {
        var days = everyday.Between(window.From, window.To);
        var farAbove = days.Count(day => day.IsFarAbove);
        return new EverydayWeek(days, farAbove >= FewestFarAboveDaysToEase ? EasedDayShare * farAbove : 0);
    }

    // ---- Deload ----

    /// <summary>A week's figures as a building week is judged: whether every family met its raw target, and its hard sets against the four before.</summary>
    private sealed record WeekRead(DayRange Window, bool EveryFamilyMet, LoadRatio HardSets)
    {
        public bool Building => EveryFamilyMet && HardSets is LoadRatio.Compared { Ratio: >= BuildingRatio };
    }

    /// <summary>Whether the calendar week of the day is offered a deload, decided from the weeks before its Monday.</summary>
    public static Deload Deload(TrainingLog log, HardSetCounts sets, DateOnly day)
    {
        var monday = WeekStarting(day);
        var run = Enumerable.Range(0, MostWeeksCounted)
            .Select(back => Read(sets, VolumeTargets.WindowEndingOn(monday.AddDays(-1 - 7 * back))))
            .TakeWhile(week => week.Building)
            .ToList();
        var strain = Strain(log, run).ToList();

        if (run.Count < LongRun && (run.Count < StrainingRun || strain.Count is 0))
            return new Deload.NotSuggested(run.Count, NotYet(run.Count, strain.Count > 0));

        var usual = ChronicBlocks(new DayRange(monday, monday.AddDays(6))).Average(block => sets.All(block));
        var ceiling = Math.Floor(DeloadCeilingShare * usual);
        var soFar = sets.All(new DayRange(monday, day));
        return new Deload.Suggested(
            monday,
            run.Count,
            ceiling,
            soFar,
            [
                $"{run.Count} building weeks in a row: each met every family's target and held your usual hard sets or more.",
                .. strain,
                $"A deload is a suggestion: {DeloadShare * 100:0} % of each family's target, the same weights — {Figure(ceiling)} hard sets at most.",
                soFar <= ceiling
                    ? $"{soFar} hard sets so far this week: within the deload."
                    : $"{soFar} hard sets so far this week, more than a deload's {Figure(ceiling)}: fine, it was a suggestion.",
            ]);
    }

    private static WeekRead Read(HardSetCounts sets, DayRange window) => new(
        window,
        MovementPatterns.Judged.All(family => sets.Of(family, window) >= WeekExpectationsAnalysis.FamilyTarget(sets, family, window).Target),
        HardSetRatio(sets, window));

    /// <summary>
    /// What says the run strained him: a steep build, last week's fatigue or difficulty rated high,
    /// or fatigue climbing over the run.
    /// </summary>
    private static IEnumerable<string> Strain(TrainingLog log, IReadOnlyList<WeekRead> run)
    {
        if (run.Count is 0) yield break;

        var build = run.Select(week => week.HardSets).OfType<LoadRatio.Compared>().Where(week => week.Chronic > 0).ToList();
        if (build.Count == run.Count && build.Average(week => week.Ratio) is var steep && steep >= LoadRatio.SteepRatio)
            yield return $"A steep build: hard sets {steep.ToString("0.0", CultureInfo.InvariantCulture)}× the four weeks before on average over the run.";

        var last = Ratings(log, run[0].Window).ToList();
        if (Mean(last.Select(rating => rating.AccumulatedFatigue)) is double fatigue and >= TiredRating)
            yield return $"Accumulated fatigue rated {Figure(fatigue)} of 5 on average last week.";
        if (Mean(last.Select(rating => rating.DifficultyRating)) is double difficulty and >= TiredRating)
            yield return $"Sessions rated {Figure(difficulty)} of 5 for difficulty on average last week.";
        if (run.Count > 1
            && Mean(Ratings(log, run[^1].Window).Select(rating => rating.AccumulatedFatigue)) is { } first
            && Mean(last.Select(rating => rating.AccumulatedFatigue)) is { } latest
            && latest - first >= 1)
            yield return $"Accumulated fatigue up from {Figure(first)} to {Figure(latest)} of 5 over the run.";
    }

    private static IEnumerable<PerceivedEffortRatings> Ratings(TrainingLog log, DayRange window) =>
        log.Between(window.From, window.To).Select(session => session.Effort).OfType<EffortReported>().Select(effort => effort.Ratings);

    private static double? Mean(IEnumerable<int?> ratings)
    {
        var rated = ratings.OfType<int>().ToList();
        return rated.Count is 0 ? null : rated.Average();
    }

    private static IReadOnlyList<string> NotYet(int building, bool strained) => building switch
    {
        0 => ["Last week was not a building week, so nothing has accumulated to deload from."],
        _ => [$"{building} building week{(building is 1 ? "" : "s")} in a row: a deload is suggested after {StrainingRun} of a steep build or climbing tiredness{(strained ? " (as these are)" : "")}, or after {LongRun}."],
    };

    /// <summary>The four seven-day blocks before a block, newest first: the chronic load it is read against.</summary>
    private static IEnumerable<DayRange> ChronicBlocks(DayRange block) => WeekExpectationsAnalysis.Before(block).Take(Progression.ChronicWeeks);

    private static string Figure(double value) => ExpectationReasons.Figure(value);
}
