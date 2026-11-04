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
/// their targets, capped at the target, so no part pays for another.
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
        var families = TrainingVolumeAnalysis.Families([.. history.Where(placed => placed.Day >= window.From)], history);
        return new LiftingPart(
            ScoreRules.LiftingPoints * families.Average(family => Share(family.HardSets, family.Target)),
            ScoreRules.LiftingPoints,
            families);
    }

    private static CardioPart Cardio(CardioMeasure measure) => measure switch
    {
        CardioMeasure.Measured measured => new CardioPart(
            ScoreRules.Zone2Points * Share(measured.Zone2Minutes, measured.Zone2Target)
            + ScoreRules.HighIntensityPoints * Share(measured.HighIntensityMinutes, measured.HighIntensityTarget),
            ScoreRules.CardioPoints,
            measure),
        CardioMeasure.Unknown => new CardioPart(null, ScoreRules.CardioPoints, measure),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static ProgressPart Progress(IReadOnlyList<FamilyPriority> families) =>
        new(
            ScoreRules.ProgressPoints * families.Average(family => ShareOf(family.Lift)),
            ScoreRules.ProgressPoints,
            families);

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
        var blocks = Enumerable.Range(0, ScoreRules.ConsistencyBlocks)
            .Select(block => endingOn.AddDays(-7 * block))
            .Select(blockEnd => log.Between(blockEnd.AddDays(-6), blockEnd).Count())
            .ToList();

        return new ConsistencyPart(
            ScoreRules.ConsistencyPoints * blocks.Average(sessions => Share(sessions, ScoreRules.SessionsPerWeek)),
            ScoreRules.ConsistencyPoints,
            blocks,
            ScoreRules.SessionsPerWeek);
    }

    private static double Share(double measured, double target) => target <= 0 ? 0 : Math.Min(1, measured / target);
}
