using System.Globalization;
using lionheart.Model;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Expectations;
using lionheart.Services.Training.Focus;

namespace lionheart.Services.Training.Load;

/// <summary>
/// The calendar week's one piece of advice, as a function of the weeks before its Monday.
/// </summary>
/// <remarks>
/// <para>
/// Pure, and read only from what was settled by the Monday — the week that ended the day before,
/// the injuries dated on or before the Monday, the deload offered for the week — so every day of
/// the week reads the same advice. The first rule that holds wins:
/// </para>
/// <list type="number">
/// <item>An active injury touching a family: hold it while it is managed.</item>
/// <item>A deload suggested for the week.</item>
/// <item>Last week's running, then its cardio minutes, steep or a spike over the four weeks before.</item>
/// <item>Two or more days of everyday load far above his usual last week.</item>
/// <item>Last week's lifting at 90 % of its targets or more with cardio at half or less: more cardio; or the reverse.</item>
/// <item>A priority lift trained in each of the last four weeks without a new best.</item>
/// <item>Otherwise keep building, the target furthest behind last week named.</item>
/// </list>
/// </remarks>
public static class WeeklyAdviceAnalysis
{
    /// <summary>The share of its targets a part of the week meets to count as done in full.</summary>
    public const double MetShare = 0.9;

    /// <summary>The share at or under which the other part counts as neglected.</summary>
    public const double ShortShare = 0.5;

    /// <summary>Weeks a priority lift is trained without a new best before it reads stalled.</summary>
    public const int StalledWeeks = 4;

    public static WeeklyAdvice Of(ExpectationInputs inputs, HardSetCounts sets, Deload deload, DateOnly day)
    {
        var monday = LoadAnalysis.WeekStarting(day);
        var lastWeek = VolumeTargets.WindowEndingOn(monday.AddDays(-1));
        var injuries = InjuryManagement.Managed(inputs.Injuries, inputs.Log, sets, monday);

        return Injury(monday, injuries)
               ?? Deloading(monday, deload)
               ?? Ramp(monday, LoadAnalysis.Cardio(inputs.CardioOf, lastWeek))
               ?? Everyday(monday, LoadAnalysis.Everyday(inputs.Everyday, lastWeek))
               ?? Balance(monday, sets, inputs.CardioOf, lastWeek)
               ?? Stalled(monday, inputs.Log, inputs.Pins)
               ?? Building(monday, sets, inputs.CardioOf, lastWeek);
    }

    private static WeeklyAdvice? Injury(DateOnly monday, IReadOnlyList<ManagedInjury> injuries) =>
        injuries.FirstOrDefault(injury => injury.Families.Count > 0) is { } injury
            ? new WeeklyAdvice.ManageInjury(
                monday,
                injury.Name,
                injury.Families,
                $"Hold {Families(injury.Families)} while {injury.Name} settles",
                [
                    .. injury.Preceding.Where(trend => trend.Earlier > 0 && Math.Abs(trend.Change) >= 0.1).Select(Lead),
                    $"{Sentence(Families(injury.Families))} target{(injury.Families.Count is 1 ? " is" : "s are")} paused while it is managed; train {(injury.Families.Count is 1 ? "it" : "them")} at what you have been doing, not more.",
                    "Once it is resolved, the targets grow back 10 % a week from what you did meanwhile.",
                ])
            : null;

    private static WeeklyAdvice? Deloading(DateOnly monday, Deload deload) => deload switch
    {
        Deload.Suggested suggested => new WeeklyAdvice.TakeDeload(
            monday,
            suggested.BuildingWeeks,
            "Deload week: fewer sets, the same weights",
            [.. suggested.Reasons.Take(suggested.Reasons.Count - 1)]),
        Deload.NotSuggested => null,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static WeeklyAdvice? Ramp(DateOnly monday, CardioLoad cardio)
    {
        if (cardio is not CardioLoad.Read read) return null;

        if (read.RunningMeters is { Standing: RampStanding.Steep or RampStanding.Spike, Ratio: LoadRatio.Compared running })
            return new WeeklyAdvice.EaseRamp(
                monday,
                RampedMeasure.Running,
                running,
                "Hold your running this week",
                [
                    $"Last week's running, {Imperial.Miles(running.Acute)}, was {ExpectationReasons.Ratio(running)} the 4 weeks before it ({Imperial.Miles(running.Chronic)} a week).",
                    "Run about last week's distance rather than more, so the legs catch up with it; rides and rows still count toward zone 2.",
                ]);

        if (read.Minutes is { Standing: RampStanding.Steep or RampStanding.Spike, Ratio: LoadRatio.Compared minutes })
            return new WeeklyAdvice.EaseRamp(
                monday,
                RampedMeasure.CardioMinutes,
                minutes,
                "Hold your cardio this week",
                [
                    $"Last week's cardio, {Figure(minutes.Acute)} min, was {ExpectationReasons.Ratio(minutes)} the 4 weeks before it ({Figure(minutes.Chronic)} min a week).",
                    "Keep this week's cardio near last week's rather than adding to it; this week's targets do not build on the spike.",
                ]);

        return null;
    }

    private static WeeklyAdvice? Everyday(DateOnly monday, EverydayWeek everyday) =>
        everyday.FarAboveDays >= LoadAnalysis.FewestFarAboveDaysToEase
            ? new WeeklyAdvice.EaseAfterEverydayLoad(
                monday,
                everyday.FarAboveDays,
                "Ease into the week",
                [
                    $"{everyday.FarAboveDays} days last week were far above your usual activity, the biggest {Figure(everyday.Days.Max(day => day.Ratio))}× your usual day.",
                    "They earned no training credit, but the fatigue is real: keep the first sessions of the week easy.",
                ])
            : null;

    private static WeeklyAdvice? Balance(DateOnly monday, HardSetCounts sets, Func<DayRange, CardioMeasure> cardioOf, DayRange lastWeek)
    {
        if (cardioOf(lastWeek) is not CardioMeasure.Measured measured) return null;

        var lifting = LiftingShare(sets, lastWeek);
        var cardio = CardioShare(measured, cardioOf, lastWeek);
        var shares = $"Last week's lifting met {Percent(lifting)} of its targets and its cardio {Percent(cardio)}.";
        return (lifting, cardio) switch
        {
            ( >= MetShare, <= ShortShare) => new WeeklyAdvice.EmphasiseCardio(
                monday,
                "More cardio this week",
                [shares, "Lifting can hold at last week's sets; put the extra session into zone 2 or intervals."]),
            ( <= ShortShare, >= MetShare) => new WeeklyAdvice.EmphasiseLifting(
                monday,
                "More lifting this week",
                [shares, "Cardio can hold at last week's minutes; put the extra session into lifting."]),
            _ => null,
        };
    }

    private static WeeklyAdvice? Stalled(DateOnly monday, TrainingLog log, IReadOnlyList<FocusPin> pins)
    {
        var weeks = Enumerable.Range(0, StalledWeeks)
            .Select(back => PriorityAnalysis.Read(log, pins, monday.AddDays(-1 - 7 * back)))
            .ToList();
        var stalled = weeks[0]
            .Select(priority => priority.Lift)
            .OfType<PriorityLift.Chosen>()
            .FirstOrDefault(lift => weeks.All(week => week.Any(priority =>
                priority.Lift is PriorityLift.Chosen { Progress: LiftProgress.Compared { Standing: ProgressStanding.Held or ProgressStanding.Below } } chosen
                && chosen.MovementDataId == lift.MovementDataId)));
        if (stalled is not { Progress: LiftProgress.Compared compared }) return null;

        var family = weeks[0].First(priority => priority.Lift == stalled).Family;
        return new WeeklyAdvice.PriorityStalled(
            monday,
            family,
            stalled.Name,
            $"{stalled.Name} has stalled: change one thing",
            [
                $"Trained in each of the last {StalledWeeks} weeks without passing its 12-week best estimate of {Imperial.Weight(compared.PriorBest, compared.Unit)}.",
                "Try a different rep range or a close variation for a few weeks, or a lighter week before the next attempt.",
            ]);
    }

    private static WeeklyAdvice Building(DateOnly monday, HardSetCounts sets, Func<DayRange, CardioMeasure> cardioOf, DayRange lastWeek)
    {
        var behind = Behind(sets, cardioOf, lastWeek).MinBy(measure => measure.Share);
        return new WeeklyAdvice.KeepBuilding(
            monday,
            "Keep building",
            behind is { Share: < 1 } furthest
                ? [$"Furthest behind last week: {furthest.Words}.", "This week's targets grow 10 % from the last four weeks; close that gap first."]
                : ["Last week met every target.", "This week's targets grow 10 % from the last four weeks."]);
    }

    /// <summary>Each family's and band's share of last week's raw target, in words.</summary>
    private static IEnumerable<(double Share, string Words)> Behind(HardSetCounts sets, Func<DayRange, CardioMeasure> cardioOf, DayRange lastWeek)
    {
        foreach (var family in MovementPatterns.Judged)
        {
            var target = FamilyTarget(sets, family, lastWeek);
            var done = sets.Of(family, lastWeek);
            yield return (Share(done, target), $"{PatternWords.Of(family)}, {done} of {Figure(target)} hard sets");
        }

        if (cardioOf(lastWeek) is not CardioMeasure.Measured measured) yield break;
        var zone2 = BandTarget(cardioOf, lastWeek, ScoreRules.Zone2, block => block.Zone2Minutes);
        yield return (Share(measured.Zone2Minutes, zone2), $"zone 2, {Figure(measured.Zone2Minutes)} of {Figure(zone2)} min");
        var quality = BandTarget(cardioOf, lastWeek, ScoreRules.Quality, block => block.QualityMinutes);
        yield return (Share(measured.QualityMinutes, quality), $"quality, {Figure(measured.QualityMinutes)} of {Figure(quality)} min");
    }

    private static double LiftingShare(HardSetCounts sets, DayRange week) =>
        MovementPatterns.Judged.Average(family => Share(sets.Of(family, week), FamilyTarget(sets, family, week)));

    private static double CardioShare(CardioMeasure.Measured measured, Func<DayRange, CardioMeasure> cardioOf, DayRange week) =>
        (Share(measured.Zone2Minutes, BandTarget(cardioOf, week, ScoreRules.Zone2, block => block.Zone2Minutes))
         + Share(measured.QualityMinutes, BandTarget(cardioOf, week, ScoreRules.Quality, block => block.QualityMinutes))) / 2;

    private static double FamilyTarget(HardSetCounts sets, PatternFamily family, DayRange week) =>
        Progression.Of(ScoreRules.FamilySets, [.. WeekExpectationsAnalysis.Before(week).Select(block => (double?)sets.Of(family, block))]).Target;

    private static double BandTarget(
        Func<DayRange, CardioMeasure> cardioOf, DayRange week, Standard standard, Func<CardioMeasure.Measured, double> figure) =>
        Progression.Of(standard, [.. WeekExpectationsAnalysis.Before(week)
            .Select(cardioOf)
            .Select(block => block is CardioMeasure.Measured measured ? figure(measured) : (double?)null)]).Target;

    private static double Share(double done, double target) => target > 0 ? Math.Min(1, done / target) : 1;

    /// <summary>"Push volume rose 40 % in the 4 weeks before it: 11 hard sets a week against 8."</summary>
    private static string Lead(FamilyTrend trend) =>
        $"{Sentence(PatternWords.Of(trend.Family))} volume {(trend.Change > 0 ? "rose" : "fell")} {Math.Abs(trend.Change) * 100:0} % in the {InjuryManagement.RecentWeeks} weeks before it: {Figure(trend.Recent)} hard sets a week against {Figure(trend.Earlier)} in the {InjuryManagement.EarlierWeeks} before.";

    private static string Families(IReadOnlyList<PatternFamily> families) => string.Join(" and ", families.Select(PatternWords.Of));

    private static string Sentence(string words) => words.Length is 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];

    private static string Percent(double share) => $"{share * 100:0} %";

    private static string Figure(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
