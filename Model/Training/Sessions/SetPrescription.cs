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

public sealed record LiftPrescription(
    Guid MovementDataId,
    int TargetReps,
    double TargetWeight,
    WeightUnit Unit,
    Rpe TargetRpe) : SetPrescription;

public sealed record DistanceTimePrescription(
    Guid MovementDataId,
    double TargetDistance,
    DistanceUnit Unit,
    TimeSpan TargetDuration) : SetPrescription;

/// <summary>A loose prescription ("hard intervals, coach's choice") — text is the plan.</summary>
public sealed record FreeformPrescription(string Text) : SetPrescription;
