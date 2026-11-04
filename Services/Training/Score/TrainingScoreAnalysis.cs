using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Focus;

namespace lionheart.Services.Training.Score;

/// <summary>The training score as a function of the log, the pins and the cardio of each window.</summary>
/// <remarks>
/// Pure, so the weights and standards in <see cref="ScoreRules"/> are tested and calibrated against
/// the athlete's history without a database. Each volume measurement is read against a target grown
/// from the four weeks before it (<see cref="Progression"/>); every measurement pays on a concave
/// curve capped at its target, so no part pays for another.
/// </remarks>
public static class TrainingScoreAnalysis
{
    /// <param name="cardioOf">The cardio of any seven-day window: the score's own, and the four before it for the targets.</param>
    public static TrainingScore Score(
        TrainingLog log, IReadOnlyList<FocusPin> pins, Func<DayRange, CardioMeasure> cardioOf, DateOnly endingOn)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        var lifting = Lifting(log, window);
        var cardioPart = Cardio(cardioOf(window), Before(window).Select(cardioOf).ToList());
        var progress = Progress(PriorityAnalysis.Read(log, pins, endingOn));
        var consistency = Consistency(log, endingOn);

        var unknown = cardioPart.Points is null ? new[] { ScorePart.Cardio } : [];
        var earned = lifting.Points + (cardioPart.Points ?? 0) + progress.Points + consistency.Points;
        var possible = lifting.OutOf + (cardioPart.Points is null ? 0 : cardioPart.OutOf) + progress.OutOf + consistency.OutOf;

        return new TrainingScore(
            window,
            (int)Math.Round(earned, MidpointRounding.AwayFromZero),
            possible,
            unknown,
            lifting,
            cardioPart,
            progress,
            consistency);
    }

    /// <summary>The four seven-day blocks before a window, newest first: what its targets grow from.</summary>
    public static IEnumerable<DayRange> Before(DayRange window) =>
        Enumerable.Range(1, Progression.ChronicWeeks)
            .Select(block => new DayRange(window.From.AddDays(-7 * block), window.To.AddDays(-7 * block)));

    private static LiftingPart Lifting(TrainingLog log, DayRange window)
    {
        var history = TrainingVolumeAnalysis.Placed(log, DateOnly.MinValue, window.To).ToList();
        IReadOnlyList<FamilyVolume> In(DayRange range) =>
            TrainingVolumeAnalysis.Families([.. history.Where(placed => placed.Day >= range.From && placed.Day <= range.To)], history);

        var before = Before(window).Select(In).ToList();
        var families = Shared(
            [
                .. In(window).Select(family => new FamilyTarget(
                    family.Family,
                    family.HardSets,
                    Progression.Of(
                        ScoreRules.FamilySets,
                        [.. before.Select(block => (double?)block.Single(earlier => earlier.Family == family.Family).HardSets)]),
                    family.LastTrained))
            ],
            ScoreRules.LiftingPoints,
            family => Progression.Share(family.HardSets, family.Target));
        return new LiftingPart(families.Sum(family => family.Points), ScoreRules.LiftingPoints, families);
    }

    private static CardioPart Cardio(CardioMeasure measure, IReadOnlyList<CardioMeasure> before)
    {
        if (measure is not CardioMeasure.Measured measured) return new CardioPart(null, ScoreRules.CardioPoints, measure, []);

        var earlier = before.Select(block => block as CardioMeasure.Measured).ToList();
        Scored<ZoneTarget> Band(ZoneBand band, double minutes, Standard standard, Func<CardioMeasure.Measured, double> figure, int points)
        {
            var target = new ZoneTarget(band, minutes, Progression.Of(standard, [.. earlier.Select(block => block is null ? (double?)null : figure(block))]));
            return new Scored<ZoneTarget>(target, points * Progression.Share(minutes, target.Target), points);
        }

        IReadOnlyList<Scored<ZoneTarget>> bands =
        [
            Band(ZoneBand.Zone2, measured.Zone2Minutes, ScoreRules.Zone2, block => block.Zone2Minutes, ScoreRules.Zone2Points),
            Band(ZoneBand.Quality, measured.QualityMinutes, ScoreRules.Quality, block => block.QualityMinutes, ScoreRules.QualityPoints),
        ];
        return new CardioPart(bands.Sum(band => band.Points), ScoreRules.CardioPoints, measure, bands);
    }

    private static ProgressPart Progress(IReadOnlyList<FamilyPriority> families)
    {
        var scored = Shared(families, ScoreRules.ProgressPoints, family => ShareOf(family.Lift));
        return new ProgressPart(scored.Sum(family => family.Points), ScoreRules.ProgressPoints, scored);
    }

    /// <summary>A part's points split evenly across its measurements, each paying its share of its slice.</summary>
    private static IReadOnlyList<Scored<T>> Shared<T>(IReadOnlyList<T> measurements, int points, Func<T, double> share)
    {
        var each = (double)points / measurements.Count;
        return [.. measurements.Select(measurement => new Scored<T>(measurement, each * share(measurement), each))];
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

    private static ConsistencyPart Consistency(TrainingLog log, DateOnly endingOn)
    {
        var blocks = Shared(
            [
                .. Enumerable.Range(0, ScoreRules.ConsistencyBlocks)
                    .Select(block => endingOn.AddDays(-7 * block))
                    .Select(blockEnd => new DayRange(blockEnd.AddDays(-6), blockEnd))
                    .Select(window => new SessionBlock(window, log.Between(window.From, window.To).Count(session => session.Trained)))
            ],
            ScoreRules.ConsistencyPoints,
            block => Progression.Share(block.Sessions, ScoreRules.SessionsPerWeek));

        return new ConsistencyPart(
            blocks.Sum(block => block.Points), ScoreRules.ConsistencyPoints, blocks, ScoreRules.SessionsPerWeek);
    }
}
