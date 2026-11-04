namespace lionheart.Model.Training.Score;

/// <summary>
/// What a volume measurement is read against this week: a target grown from what the four weeks
/// before held, between a floor and the committed standard.
/// </summary>
/// <param name="Target">This week's target: the chronic weekly figure plus the ramp, rounded up, floored and capped.</param>
/// <param name="Committed">The standard a committed week reaches, which the whole of the points still needs.</param>
/// <param name="Floor">The least the target falls to, so a week of nearly nothing is never a target met.</param>
/// <param name="Chronic">
/// The mean weekly figure of the four seven-day blocks before the window, over the blocks that could
/// be read; absent when none could, and the target is then the committed standard.
/// </param>
public sealed record WeeklyTarget(double Target, double Committed, double Floor, double? Chronic);

/// <summary>A measurement's standard: the committed figure, the floor, and the step targets round up to.</summary>
public sealed record Standard(double Committed, double Floor, double Step);

/// <summary>
/// The progression model the volume parts are read by: chronic load as the basis, a ten per cent
/// ramp, a floor, and half of every measurement's points kept for the committed standard.
/// </summary>
/// <remarks>
/// <para>
/// Tolerance follows what has been done lately, so the target does: the mean of the four weeks
/// before is the chronic load (the 28-day chronic window of Gabbett 2016), and this week's target
/// is ten per cent above it. Ten per cent keeps the week's load at 1.1 times the chronic, inside
/// every safe band the acute:chronic literature proposes, and well under the 30 per cent
/// two-week jumps that preceded injury in novice runners (Nielsen et al. 2014). The ratio's
/// predictive claims are contested (Impellizzeri et al. 2020); it is used here as a ramp, not a
/// risk model.
/// </para>
/// <para>
/// A target that only followed capacity would pay a comeback week of little the same as a
/// committed one. So half of each measurement's points are read against this week's target and
/// half against the committed standard: meeting the ramp pays well, and only the committed figure
/// pays the whole. The floor keeps a week after a lay-off from being met by almost nothing.
/// </para>
/// </remarks>
public static class Progression
{
    public const int ChronicWeeks = 4;

    /// <summary>The growth the target allows over the chronic weekly figure.</summary>
    public const double Ramp = 0.10;

    /// <summary>The share of a measurement's points read against this week's target; the rest against the committed standard.</summary>
    public const double TargetShare = 0.5;

    /// <summary>This week's target from the weekly figures of the blocks before, the unread ones absent.</summary>
    public static WeeklyTarget Of(Standard standard, IReadOnlyList<double?> before)
    {
        var read = before.OfType<double>().ToList();
        if (read.Count is 0) return new WeeklyTarget(standard.Committed, standard.Committed, standard.Floor, null);

        var chronic = read.Average();
        // The epsilon absorbs floating-point excess: 100 × 1.1 is 110.00000000000001, which would
        // otherwise round up a whole step to 115.
        var grown = Math.Ceiling(chronic * (1 + Ramp) / standard.Step - 1e-9) * standard.Step;
        return new WeeklyTarget(Math.Clamp(grown, standard.Floor, standard.Committed), standard.Committed, standard.Floor, chronic);
    }

    /// <summary>
    /// A measurement's share of its points: half on this week's target, half on the committed
    /// standard, each the square root of the fraction reached, capped at the whole.
    /// </summary>
    /// <remarks>
    /// Concave because the returns are: the first four of eight hard sets, or the first hour of
    /// zone 2, buy most of what the whole would (Pelland 2026).
    /// </remarks>
    public static double Share(double measured, WeeklyTarget target) =>
        TargetShare * Share(measured, target.Target) + (1 - TargetShare) * Share(measured, target.Committed);

    public static double Share(double measured, double target) =>
        target <= 0 ? 0 : Math.Sqrt(Math.Clamp(measured / target, 0, 1));
}
