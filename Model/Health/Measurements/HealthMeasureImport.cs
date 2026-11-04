using lionheart.Functional;

namespace lionheart.Model.Health.Measurements;

/// <summary>Days of Apple Health figures the phone read, offered to be kept.</summary>
/// <remarks>
/// The phone reduces Health's readings to days before sending (a walking speed is read per walk,
/// and six years of them are hundreds of thousands of samples) and sends only what Apple's own
/// devices measured: another app's copy — Oura's HRV is RMSSD written under Health's SDNN type —
/// is not the same figure. Everything else about a day is decided here.
/// </remarks>
public sealed record HealthMeasuresImport(IReadOnlyList<MeasuredDayCommand> Days);

/// <summary>
/// One day of one figure as the phone sends it: flat scalars, read into a <see cref="MeasuredDay"/>
/// by <see cref="Stated"/>, for the reason <see cref="HealthWorkoutImport"/> gives.
/// </summary>
/// <param name="Day">The calendar day on the phone's clock the readings were taken on.</param>
public sealed record MeasuredDayCommand(
    HealthMetric Metric,
    MeasuringDevice Device,
    DateOnly Day,
    double Mean,
    double Minimum,
    double Maximum,
    int Count)
{
    public Result<MeasuredDay, Problem> Stated() =>
        !Enum.IsDefined(Metric) || !Enum.IsDefined(Device)
            ? Result<MeasuredDay, Problem>.Failure(new Problem.Invalid(nameof(Metric), "Not a figure Lionheart keeps from Health."))
            : DayAggregate.Of(Metric, Mean, Minimum, Maximum, Count)
                .Map(aggregate => new MeasuredDay(Metric, Device, Day, aggregate));
}

/// <summary>What became of a batch of days offered.</summary>
/// <param name="Recorded">Days new to Lionheart.</param>
/// <param name="Restated">Days held already whose readings have grown or changed.</param>
/// <param name="Unchanged">Days held already exactly as offered.</param>
/// <param name="Unreadable">Days whose numbers cannot be readings of their figure, and were left out.</param>
public sealed record HealthMeasuresImportReport(int Recorded, int Restated, int Unchanged, int Unreadable);

/// <summary>One figure as one device measured it, day by day, oldest first.</summary>
public sealed record HealthMeasureSeries(HealthMetric Metric, MeasuringDevice Device, IReadOnlyList<MeasuredDayView> Days);

public sealed record MeasuredDayView(DateOnly Day, double Mean, double Minimum, double Maximum, int Count)
{
    public static MeasuredDayView Of(HealthDailyMeasure measure) =>
        new(measure.Day, measure.Aggregate.Mean, measure.Aggregate.Minimum, measure.Aggregate.Maximum, measure.Aggregate.Count);
}
