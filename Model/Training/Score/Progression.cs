using System.Text.Json.Serialization;
using lionheart.Model.Training.Load;

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
public sealed record WeeklyTarget(double Target, double Committed, double Floor, double? Chronic)
{
    /// <summary>How the target came from the chronic figure: the ramp, or held or lowered after a spike.</summary>
    public TargetGrowth Growth { get; init; } = new TargetGrowth.Ramped();
}

/// <summary>
/// Whether a target grew by the ramp, or was held back because the last week ran far above the
/// four before it.
/// </summary>
/// <remarks>
/// A spike lifts the chronic figure the target grows from, so without this the week after a spike
/// would be asked for more on the strength of the spike itself. Held and lowered are two cases
/// because they answer two sizes of spike, and the reason says which.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Ramped), "ramped")]
[JsonDerivedType(typeof(HeldAfterSteepWeek), "heldAfterSteepWeek")]
[JsonDerivedType(typeof(LoweredAfterSpike), "loweredAfterSpike")]
public abstract record TargetGrowth
{
    private protected TargetGrowth() { }

    /// <summary>The chronic figure plus the ramp, between the floor and the committed standard.</summary>
    public sealed record Ramped : TargetGrowth;

    /// <summary>The last week ran 1.3 to 1.5 times the four before it: last week's own target, not grown again.</summary>
    public sealed record HeldAfterSteepWeek(LoadRatio.Compared LastWeek) : TargetGrowth;

    /// <summary>The last week ran 1.5 times the four before it or more: back to the usual of those four, without the ramp.</summary>
    public sealed record LoweredAfterSpike(LoadRatio.Compared LastWeek) : TargetGrowth;
}

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

    /// <summary>
    /// This week's target from the weekly figures of the blocks before, newest first, the unread
    /// ones absent: the first four are the chronic load; a fifth lets the newest be judged against
    /// the four before it, so a spike last week is not built on.
    /// </summary>
    public static WeeklyTarget Of(Standard standard, IReadOnlyList<double?> before)
    {
        var read = before.Take(ChronicWeeks).OfType<double>().ToList();
        if (read.Count is 0) return new WeeklyTarget(standard.Committed, standard.Committed, standard.Floor, null);

        var chronic = read.Average();
        var ramped = Bounded(Grown(chronic, standard), standard);
        var target = new WeeklyTarget(ramped, standard.Committed, standard.Floor, chronic);
        if (before is not [{ } lastWeek, ..]
            || LoadRatio.Of(lastWeek, before.Skip(1).Take(ChronicWeeks)) is not LoadRatio.Compared ratio)
            return target;

        return ratio.Standing(SpikeLeastIncrease(standard)) switch
        {
            RampStanding.Steep when Bounded(Grown(ratio.Chronic, standard), standard) is var held && held < ramped =>
                target with { Target = held, Growth = new TargetGrowth.HeldAfterSteepWeek(ratio) },
            RampStanding.Spike when Bounded(Stepped(ratio.Chronic, standard), standard) is var lowered && lowered < ramped =>
                target with { Target = lowered, Growth = new TargetGrowth.LoweredAfterSpike(ratio) },
            _ => target,
        };
    }

    /// <summary>
    /// The least rise in a week that can read as a spike: half the floor — two hard sets, half an
    /// hour of zone 2, five quality minutes — so a small week rising to a slightly less small one
    /// is noise, not load.
    /// </summary>
    public static double SpikeLeastIncrease(Standard standard) => standard.Floor / 2;

    private static double Grown(double chronic, Standard standard) => Stepped(chronic * (1 + Ramp), standard);

    // The epsilon absorbs floating-point excess: 100 × 1.1 is 110.00000000000001, which would
    // otherwise round up a whole step to 115.
    private static double Stepped(double figure, Standard standard) =>
        Math.Ceiling(figure / standard.Step - 1e-9) * standard.Step;

    private static double Bounded(double target, Standard standard) => Math.Clamp(target, standard.Floor, standard.Committed);

    /// <summary>A standard that does not grow: its committed figure is the target every week.</summary>
    public static WeeklyTarget Fixed(Standard standard) =>
        new(standard.Committed, standard.Committed, standard.Floor, null);

    /// <summary>
    /// A target fitted to the days of the week the athlete had: every figure scaled by the share of
    /// the seven days available and rounded up to its step, so a short week still asks for its
    /// share and never less. The chronic figure is what it grew from, and stays.
    /// </summary>
    /// <param name="availableDays">Whole days, less half a day for each day of everyday load far above his usual.</param>
    public static WeeklyTarget Prorated(WeeklyTarget target, Standard standard, double availableDays)
    {
        // Max, because a whole week marked off rounds -1e-9 up to negative zero, which prints "-0".
        double Fit(double figure) =>
            Math.Max(0, Math.Ceiling(figure * availableDays / WeekDays / standard.Step - 1e-9) * standard.Step);

        return availableDays >= WeekDays
            ? target
            : target with { Target = Fit(target.Target), Committed = Fit(target.Committed), Floor = Fit(target.Floor) };
    }

    public const int WeekDays = 7;

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
