using System.Text.Json.Serialization;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Sessions;

/// <summary>
/// A prescribed set on a planned session — targets only. Actuals live exclusively on
/// performed <see cref="Sets.SetEntry"/>s, so planned and actual never half-null one row.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LiftPrescription), "lift")]
[JsonDerivedType(typeof(DistanceTimePrescription), "distanceTime")]
[JsonDerivedType(typeof(FreeformPrescription), "freeform")]
public abstract record SetPrescription;

// Every positional member is redeclared, not only the guarded ones — see the note in SetEntry.cs.

public sealed record LiftPrescription(
    Guid MovementDataId,
    int TargetReps,
    double TargetWeight,
    WeightUnit Unit,
    Rpe TargetRpe) : SetPrescription
{
    public Guid MovementDataId { get; init; } = MovementDataId;
    public int TargetReps { get; init; } = TargetReps;
    public double TargetWeight { get; init; } = Require.Finite(TargetWeight, nameof(TargetWeight));
    public WeightUnit Unit { get; init; } = Require.ClosedEnum(Unit, nameof(Unit));
    public Rpe TargetRpe { get; init; } = TargetRpe;
}

public sealed record DistanceTimePrescription(
    Guid MovementDataId,
    double TargetDistance,
    DistanceUnit Unit,
    TimeSpan TargetDuration) : SetPrescription
{
    public Guid MovementDataId { get; init; } = MovementDataId;
    public double TargetDistance { get; init; } = Require.Finite(TargetDistance, nameof(TargetDistance));
    public DistanceUnit Unit { get; init; } = Require.ClosedEnum(Unit, nameof(Unit));
    public TimeSpan TargetDuration { get; init; } = TargetDuration;
}

/// <summary>A loose prescription ("hard intervals, coach's choice") — text is the plan.</summary>
public sealed record FreeformPrescription(string Text) : SetPrescription;
