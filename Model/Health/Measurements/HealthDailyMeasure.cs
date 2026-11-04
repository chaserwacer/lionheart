using lionheart.Functional;

namespace lionheart.Model.Health.Measurements;

/// <summary>One day's readings of a figure, reduced to their mean, extremes and number.</summary>
/// <remarks>
/// A day rather than each reading because the phone takes some of these dozens of times a day —
/// a walking speed per walk — and every reader asks about days and weeks. The extremes keep what a
/// mean hides: a day of HRV readings from 20 to 90 is not a day at 55.
/// </remarks>
public sealed record DayAggregate
{
    private DayAggregate(double mean, double minimum, double maximum, int count) =>
        (Mean, Minimum, Maximum, Count) = (mean, minimum, maximum, count);

    public double Mean { get; private init; }
    public double Minimum { get; private init; }
    public double Maximum { get; private init; }

    /// <summary>How many readings the day holds; at least one.</summary>
    public int Count { get; private init; }

    /// <summary>What an entity holds between EF constructing it and setting its stored value.</summary>
    internal static DayAggregate Placeholder { get; } = new(0, 0, 0, 1);

    /// <summary>A day's readings of the metric, or why these numbers cannot be one.</summary>
    public static Result<DayAggregate, Problem> Of(HealthMetric metric, double mean, double minimum, double maximum, int count)
    {
        var (lowest, highest) = HealthMetricBounds.Of(metric);

        return count < 1
            ? Refused(nameof(Count), "A day holds at least one reading.")
            : !(double.IsFinite(mean) && double.IsFinite(minimum) && double.IsFinite(maximum))
                ? Refused(nameof(Mean), "A reading is a finite number.")
            : !(minimum <= mean && mean <= maximum)
                ? Refused(nameof(Mean), "A day's mean lies between its lowest and highest readings.")
            : minimum < lowest || maximum > highest
                ? Refused(nameof(Mean), $"A {metric} reading lies between {lowest} and {highest}.")
            : Result<DayAggregate, Problem>.Success(new DayAggregate(mean, minimum, maximum, count));
    }

    private static Result<DayAggregate, Problem> Refused(string field, string message) =>
        Result<DayAggregate, Problem>.Failure(new Problem.Invalid(field, message));
}

/// <summary>
/// What one of Apple's devices measured of one figure on one calendar day.
/// </summary>
/// <remarks>
/// Keyed by athlete, figure, device and day: the phone re-reads the last week on every sync, since
/// the watch delivers a night's readings late and Health adds an estimate after the fact, so a day
/// offered again restates itself rather than adding a second. An entity rather than a record for
/// that reason.
/// </remarks>
public sealed class HealthDailyMeasure
{
    private HealthDailyMeasure() { }

    public Guid UserId { get; private init; }
    public HealthMetric Metric { get; private init; }
    public MeasuringDevice Device { get; private init; }
    public DateOnly Day { get; private init; }

    /// <remarks>
    /// A placeholder for EF, which sets a complex property after constructing the entity;
    /// <see cref="Measured"/> is the only way a real one is made.
    /// </remarks>
    public DayAggregate Aggregate { get; private set; } = DayAggregate.Placeholder;

    public static HealthDailyMeasure Measured(Guid userId, MeasuredDay day) =>
        new() { UserId = userId, Metric = day.Metric, Device = day.Device, Day = day.Day, Aggregate = day.Aggregate };

    /// <summary>Takes the day's readings as they now stand.</summary>
    public void Restate(DayAggregate aggregate) => Aggregate = aggregate;
}

/// <summary>A day of one figure as a device measured it, read and checked, before it is stored.</summary>
public sealed record MeasuredDay(HealthMetric Metric, MeasuringDevice Device, DateOnly Day, DayAggregate Aggregate);
