using lionheart.Functional;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.Health.Measurements;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Balance;

namespace lionheart.Services.Health.Measurements;

/// <summary>Which of Health's weigh-ins become bodyweights, and in what unit.</summary>
/// <remarks>
/// Pure, so the rules are tested without a database:
/// <list type="bullet">
/// <item><b>One per day, the day's first.</b> A morning weigh-in is the one comparable from day to
/// day, and Health often holds the same weighing twice — the scale's own entry and an app's copy of
/// it — minutes apart.</item>
/// <item><b>A day already measured is kept.</b> What the athlete logged in Lionheart, or a scan
/// measured, is his own statement for that day; Health only fills days that have none.</item>
/// <item><b>In the athlete's own unit.</b> Health holds mass without a unit and the phone reads it in
/// kilograms. A bodyweight adds to a set's plate only in the set's unit, since weights never convert
/// once logged, so a weigh-in is stated once, here, in the unit his bodyweight is kept in.</item>
/// </list>
/// </remarks>
public static class WeighInPlan
{
    private const double PoundsPerKilogram = 2.20462262185;

    /// <summary>A tenth: what a bathroom scale shows.</summary>
    private const int Decimals = 1;

    /// <summary>What the batch adds, and how many of its days were already measured or unreadable.</summary>
    public sealed record Planned(IReadOnlyList<BodyweightOnDay> Added, int DaysAlreadyMeasured, int Unreadable);

    public static Planned Of(IReadOnlyList<WeighInCommand> weighIns, IReadOnlySet<DateOnly> measuredDays, WeightUnit unit)
    {
        var read = weighIns.Partition(weighIn => InUnit(weighIn, unit));
        var firstOfEachDay = read.Delivered
            .GroupBy(weighIn => weighIn.Day)
            .Select(day => day.MinBy(weighIn => weighIn.At)!)
            .ToList();

        return new Planned(
            [.. firstOfEachDay
                .Where(weighIn => !measuredDays.Contains(weighIn.Day))
                .OrderBy(weighIn => weighIn.Day)
                .Select(weighIn => new BodyweightOnDay(weighIn.Day, weighIn.Bodyweight))],
            firstOfEachDay.Count(weighIn => measuredDays.Contains(weighIn.Day)),
            read.Refused.Count);
    }

    /// <summary>
    /// The unit his bodyweight is kept in: the newest measurement's, else the one most of his lifts
    /// are logged in, else kilograms.
    /// </summary>
    public static WeightUnit UnitFor(BodyweightOnDay? newest, IEnumerable<WeightUnit> liftUnits) =>
        newest?.Bodyweight.Unit
        ?? liftUnits.GroupBy(unit => unit).OrderByDescending(group => group.Count()).Select(group => (WeightUnit?)group.Key).FirstOrDefault()
        ?? WeightUnit.Kilograms;

    /// <summary>
    /// How many of these sets read a different bodyweight under <paramref name="after"/> than under
    /// <paramref name="before"/> — the sets whose system load the import changed.
    /// </summary>
    public static int Reweighed(
        IEnumerable<(DateOnly Day, WeightUnit Unit)> setsAddingToBodyweight,
        IReadOnlyList<BodyweightOnDay> before,
        IReadOnlyList<BodyweightOnDay> after) =>
        setsAddingToBodyweight.Count(set =>
            TrainingLog.BodyweightOn(before, set.Day, set.Unit) != TrainingLog.BodyweightOn(after, set.Day, set.Unit));

    private sealed record ReadWeighIn(DateOnly Day, DateTimeOffset At, Bodyweight Bodyweight);

    private static Result<ReadWeighIn, Problem> InUnit(WeighInCommand weighIn, WeightUnit unit) =>
        Bodyweight.Of(Math.Round(unit is WeightUnit.Pounds ? weighIn.Kilograms * PoundsPerKilogram : weighIn.Kilograms, Decimals), unit)
            .Map(bodyweight => new ReadWeighIn(weighIn.Day, weighIn.At, bodyweight));
}
