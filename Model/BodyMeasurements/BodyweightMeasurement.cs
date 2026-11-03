using lionheart.Functional;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.BodyMeasurements;

/// <summary>The athlete's weight as a scale read it, in the unit it read.</summary>
/// <remarks>
/// The unit travels with the number for the reason it does on a lift: weights never convert here,
/// and a reader adding bodyweight to a weighted dip has to know both are in the same unit.
/// </remarks>
public sealed record Bodyweight(double Value, WeightUnit Unit)
{
    /// <summary>No person weighs this much in either unit; a number past it is a typing slip.</summary>
    public const double Heaviest = 1000;

    public double Value { get; init; } = Require.Finite(Value, nameof(Value));
    public WeightUnit Unit { get; init; } = Require.ClosedEnum(Unit, nameof(Unit));

    /// <summary>A bodyweight a client states, or why it is not one.</summary>
    public static Result<Bodyweight, Problem> Of(double value, WeightUnit unit) =>
        !Enum.IsDefined(unit)
            ? Result<Bodyweight, Problem>.Failure(new Problem.Invalid(nameof(Unit), $"'{unit}' is not a WeightUnit."))
            : double.IsFinite(value) && value is > 0 and <= Heaviest
                ? Result<Bodyweight, Problem>.Success(new Bodyweight(value, unit))
                : Result<Bodyweight, Problem>.Failure(new Problem.Invalid(
                    "Weight", $"A bodyweight is more than 0 and at most {Heaviest}."));
}

/// <summary>What the athlete weighed on one calendar day.</summary>
/// <remarks>
/// One per day, keyed by the day: weighing again the same day restates the day's measurement
/// rather than adding a second, so "bodyweight on a day" always has one answer. This dated
/// measurement is the source for what the athlete weighs; the profile's undated number is not.
/// <para>
/// An entity rather than a record, since restating a day changes it and it stays that day's
/// measurement.
/// </para>
/// </remarks>
public sealed class BodyweightMeasurement
{
    private BodyweightMeasurement() { }

    public Guid UserId { get; private set; }
    public DateOnly Day { get; private set; }

    /// <remarks>
    /// The initial value is a placeholder for EF, which sets a complex property after constructing
    /// the entity; <see cref="Measured"/> is the only way a real measurement is made.
    /// </remarks>
    public Bodyweight Bodyweight { get; private set; } = new(0, WeightUnit.Pounds);

    public static BodyweightMeasurement Measured(Guid userId, DateOnly day, Bodyweight bodyweight) =>
        new() { UserId = userId, Day = day, Bodyweight = bodyweight };

    public void Restate(Bodyweight bodyweight) => Bodyweight = bodyweight;
}
