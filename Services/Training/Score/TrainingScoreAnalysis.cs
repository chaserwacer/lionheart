using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Focus;

namespace lionheart.Services.Training.Score;

/// <summary>The training score as a function of the log, the pins and the window's cardio.</summary>
/// <remarks>
/// Pure, so the weights and targets in <see cref="ScoreRules"/> are tested and calibrated against
/// the athlete's history without a database. Each part's points are its measurements against
/// their targets, on a concave curve capped at the target, so no part pays for another.
/// </remarks>
public static class TrainingScoreAnalysis
{
    public static TrainingScore Score(TrainingLog log, IReadOnlyList<FocusPin> pins, CardioMeasure cardio, DateOnly endingOn)
    {
        var window = VolumeTargets.WindowEndingOn(endingOn);
        var lifting = Lifting(log, window);
        var cardioPart = Cardio(cardio);
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

    private static LiftingPart Lifting(TrainingLog log, DayRange window)
    {
        var history = TrainingVolumeAnalysis.Placed(log, DateOnly.MinValue, window.To).ToList();
        var families = Shared(
            TrainingVolumeAnalysis.Families([.. history.Where(placed => placed.Day >= window.From)], history),
            ScoreRules.LiftingPoints,
            family => Share(family.HardSets, family.Target));
        return new LiftingPart(families.Sum(family => family.Points), ScoreRules.LiftingPoints, families);
    }

    private static CardioPart Cardio(CardioMeasure measure) => measure switch
    {
        CardioMeasure.Measured measured => Zones(
            measure,
            [
                Band(new ZoneTarget(ZoneBand.Zone2, measured.Zone2Minutes, measured.Zone2Target), ScoreRules.Zone2Points),
                Band(new ZoneTarget(ZoneBand.Zones3To5, measured.HighIntensityMinutes, measured.HighIntensityTarget), ScoreRules.HighIntensityPoints),
            ]),
        CardioMeasure.Unknown => new CardioPart(null, ScoreRules.CardioPoints, measure, []),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static CardioPart Zones(CardioMeasure measure, IReadOnlyList<Scored<ZoneTarget>> bands) =>
        new(bands.Sum(band => band.Points), ScoreRules.CardioPoints, measure, bands);

    private static Scored<ZoneTarget> Band(ZoneTarget band, int points) =>
        new(band, points * Share(band.Minutes, band.Target), points);

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
                    .Select(window => new SessionBlock(window, log.Between(window.From, window.To).Count()))
            ],
            ScoreRules.ConsistencyPoints,
            block => Share(block.Sessions, ScoreRules.SessionsPerWeek));

        return new ConsistencyPart(
            blocks.Sum(block => block.Points), ScoreRules.ConsistencyPoints, blocks, ScoreRules.SessionsPerWeek);
    }

    /// <summary>
    /// A measurement's share of its points: the square root of how much of the target it reached,
    /// capped at the whole.
    /// </summary>
    /// <remarks>
    /// Concave because the returns are: the first four of eight hard sets, or the first hour of
    /// zone 2, buy most of what the target would (Pelland 2026), so half the target pays 71 per cent
    /// rather than half. The top is unchanged — only the whole target pays in full.
    /// </remarks>
    public static double Share(double measured, double target) =>
        target <= 0 ? 0 : Math.Sqrt(Math.Clamp(measured / target, 0, 1));
}
