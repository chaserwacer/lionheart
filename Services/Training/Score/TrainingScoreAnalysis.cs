using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Expectations;
using lionheart.Services.Training.Focus;

namespace lionheart.Services.Training.Score;

/// <summary>The training score and the adjusted score as a function of the week's expectations.</summary>
/// <remarks>
/// Pure, so the weights and standards in <see cref="ScoreRules"/> are tested and calibrated against
/// the athlete's history without a database. Every target comes from
/// <see cref="WeekExpectationsAnalysis"/>; the score reads each one's raw target and the adjusted
/// score its adjusted one, through the same arithmetic. Every measurement pays on a concave curve
/// capped at its target, so no part pays for another.
/// </remarks>
public static class TrainingScoreAnalysis
{
    public static TrainingScore Score(ExpectationInputs inputs, DateOnly endingOn)
    {
        var priorities = PriorityAnalysis.Read(inputs.Log, inputs.Pins, endingOn);
        var expectations = WeekExpectationsAnalysis.Of(inputs, priorities, endingOn);
        var blocks = Blocks(inputs, expectations);
        var progress = Progress(priorities);

        var raw = Read(expectations, blocks, progress, expectation => expectation.Raw);
        var adjusted = Read(expectations, blocks, progress, expectation => expectation.Adjusted);

        return new TrainingScore(
            expectations.Window,
            raw.Value,
            raw.OutOf,
            raw.Unknown,
            raw.Lifting,
            raw.Cardio,
            progress,
            raw.Consistency,
            new AdjustedScore(adjusted.Value, adjusted.OutOf, expectations.Availability.AvailableDays, adjusted.Parts),
            expectations);
    }

    /// <summary>The window's sessions and the three seven-day blocks before it, newest first.</summary>
    private static IReadOnlyList<(DayRange Window, Expectation Sessions)> Blocks(ExpectationInputs inputs, WeekExpectations expectations) =>
    [
        (expectations.Window, expectations.Sessions),
        .. WeekExpectationsAnalysis.Before(expectations.Window)
            .Take(ScoreRules.ConsistencyBlocks - 1)
            .Select(block => (block, WeekExpectationsAnalysis.Sessions(inputs.Log, inputs.Availability, block))),
    ];

    /// <summary>The four parts against one choice of target, and what they come to.</summary>
    private sealed record Reading(LiftingPart Lifting, CardioPart Cardio, ProgressPart Progress, ConsistencyPart Consistency)
    {
        public IReadOnlyList<AdjustedPart> Parts =>
        [
            new(ScorePart.Lifting, Lifting.Points, Lifting.Families.Sum(family => family.OutOf)),
            new(ScorePart.Cardio, Cardio.Points, Cardio.Points is null ? Cardio.OutOf : Cardio.Zones.Sum(zone => zone.OutOf)),
            new(ScorePart.Progress, Progress.Points, Progress.OutOf),
            new(ScorePart.Consistency, Consistency.Points, Consistency.Blocks.Sum(block => block.OutOf)),
        ];

        public IReadOnlyList<ScorePart> Unknown => [.. Parts.Where(part => part.Points is null).Select(part => part.Part)];

        public int Value => Round(Parts.Sum(part => part.Points ?? 0));

        public int OutOf => Round(Parts.Where(part => part.Points is not null).Sum(part => part.OutOf));

        private static int Round(double points) => (int)Math.Round(points, MidpointRounding.AwayFromZero);
    }

    private static Reading Read(
        WeekExpectations expectations,
        IReadOnlyList<(DayRange Window, Expectation Sessions)> blocks,
        ProgressPart progress,
        Func<Expectation, WeeklyTarget> target) =>
        new(Lifting(expectations.Families, target), Cardio(expectations.Cardio, target), progress, Consistency(blocks, target));

    private static LiftingPart Lifting(IReadOnlyList<FamilyExpectation> expected, Func<Expectation, WeeklyTarget> target)
    {
        var families = Shared(
            [.. expected.Select(family => new FamilyTarget(family.Family, (int)family.Sets.Done, target(family.Sets), family.LastTrained))],
            ScoreRules.LiftingPoints,
            family => Progression.Share(family.HardSets, family.Target),
            family => Asks(family.Target));
        return new LiftingPart(families.Sum(family => family.Points), ScoreRules.LiftingPoints, families);
    }

    private static CardioPart Cardio(CardioExpectations expected, Func<Expectation, WeeklyTarget> target)
    {
        if (expected.Measure is not CardioMeasure.Measured) return new CardioPart(null, ScoreRules.CardioPoints, expected.Measure, []);

        IReadOnlyList<Scored<ZoneTarget>> bands =
        [
            .. expected.Zones.Select(zone =>
            {
                var measured = new ZoneTarget(zone.Band, zone.Minutes.Done, target(zone.Minutes));
                var points = zone.Band is ZoneBand.Zone2 ? ScoreRules.Zone2Points : ScoreRules.QualityPoints;
                return Asks(measured.Target)
                    ? new Scored<ZoneTarget>(measured, points * Progression.Share(measured.Minutes, measured.Target), points)
                    : new Scored<ZoneTarget>(measured, 0, 0);
            }),
        ];
        return new CardioPart(bands.Sum(band => band.Points), ScoreRules.CardioPoints, expected.Measure, bands);
    }

    private static ProgressPart Progress(IReadOnlyList<FamilyPriority> families)
    {
        var scored = Shared(families, ScoreRules.ProgressPoints, family => ShareOf(family.Lift), _ => true);
        return new ProgressPart(scored.Sum(family => family.Points), ScoreRules.ProgressPoints, scored);
    }

    private static ConsistencyPart Consistency(
        IReadOnlyList<(DayRange Window, Expectation Sessions)> blocks, Func<Expectation, WeeklyTarget> target)
    {
        var scored = Shared(
            blocks,
            ScoreRules.ConsistencyPoints,
            block => Progression.Share(block.Sessions.Done, target(block.Sessions).Target),
            block => Asks(target(block.Sessions)));

        return new ConsistencyPart(
            scored.Sum(block => block.Points),
            ScoreRules.ConsistencyPoints,
            [.. scored.Select(block => new Scored<SessionBlock>(
                new SessionBlock(block.Of.Window, (int)block.Of.Sessions.Done), block.Points, block.OutOf))],
            ScoreRules.SessionsPerWeek);
    }

    /// <summary>Whether a target asks for anything: a block with every day marked off asks for nothing.</summary>
    private static bool Asks(WeeklyTarget target) => target.Committed > 0;

    /// <summary>
    /// A part's points split evenly across its measurements, each paying its share of its slice; a
    /// measurement that asks for nothing takes its slice out of reach.
    /// </summary>
    private static IReadOnlyList<Scored<T>> Shared<T>(
        IReadOnlyList<T> measurements, int points, Func<T, double> share, Func<T, bool> asks)
    {
        var each = (double)points / measurements.Count;
        return
        [
            .. measurements.Select(measurement => asks(measurement)
                ? new Scored<T>(measurement, each * share(measurement), each)
                : new Scored<T>(measurement, 0, 0)),
        ];
    }

    /// <summary>A priority lift's share of its points: a new best pays whole, anything else a fraction.</summary>
    public static double ShareOf(PriorityLift lift) => lift switch
    {
        PriorityLift.NoLift => 0,
        PriorityLift.Chosen chosen => chosen.Progress switch
        {
            LiftProgress.NotTrained => 0,
            LiftProgress.NoEstimate => ScoreRules.BelowShare,
            LiftProgress.FirstBack => ScoreRules.FirstBackShare,
            LiftProgress.Compared compared => compared.Standing switch
            {
                ProgressStanding.LifetimeBest or ProgressStanding.NewBest => ScoreRules.NewBestShare,
                ProgressStanding.Held => ScoreRules.HeldShare,
                ProgressStanding.Below => ScoreRules.BelowShare,
                _ => throw new System.Diagnostics.UnreachableException(),
            },
            _ => throw new System.Diagnostics.UnreachableException(),
        },
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}
