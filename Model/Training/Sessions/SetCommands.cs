using System.Text.Json.Serialization;
using lionheart.Functional;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Sessions;

/// <summary>
/// Guards for values a client states, as refusals rather than throws.
/// </summary>
/// <remarks>
/// <see cref="Require"/> throws, which is right where it sits — a bad value arriving from a JSON
/// storage column is corruption, and corruption should stop the read. A bad value arriving from a
/// client is an ordinary refusal, and the throw would land inside the domain constructor, past the
/// point where the endpoint could answer 400. These check the same contracts one step earlier.
/// </remarks>
internal static class Stated
{
    /// <summary>An RPE the athlete stated, or none at all — absence is <see cref="Rpe.Unrated"/>.</summary>
    public static Result<Rpe, Problem> Rating(double? value, string field) => value switch
    {
        null => Result<Rpe, Problem>.Success(Rpe.Unrated),
        >= 0 and <= 10 => Result<Rpe, Problem>.Success(Rpe.Of(value.Value)),
        _ => Result<Rpe, Problem>.Failure(new Problem.Invalid(field, "RPE must be within [0, 10]."))
    };

    /// <summary>
    /// System.Text.Json writes any integer into an enum without checking that it names a member,
    /// so an out-of-range unit reaches the domain constructor unless it is stopped here.
    /// </summary>
    public static Result<TEnum, Problem> ClosedEnum<TEnum>(TEnum value, string field)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value)
            ? Result<TEnum, Problem>.Success(value)
            : Result<TEnum, Problem>.Failure(
                new Problem.Invalid(field, $"'{value}' is not a {typeof(TEnum).Name}."));
}

/// <summary>
/// A performed set as a client may state it: the numbers, and nothing the server owns.
/// </summary>
/// <remarks>
/// <see cref="SetEntry.Source"/> and <see cref="SetEntry.Facets"/> are deliberately absent. A set
/// entered through a form is <see cref="Formed"/> by definition, and facets are canonical
/// vocabulary that only the interpretation engine's resolve stage may assign — a client that could
/// state its own facets could widen the query surface every analytic depends on.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LiftSetCommand), "lift")]
[JsonDerivedType(typeof(DistanceTimeSetCommand), "distanceTime")]
[JsonDerivedType(typeof(TrailRunSetCommand), "trailRun")]
[JsonDerivedType(typeof(FreeformSetCommand), "freeform")]
public abstract record SetCommand
{
    private protected SetCommand() { }

    public abstract Result<SetEntry, Problem> ToEntry();
}

/// <remarks>
/// <see cref="Kind"/> defaults to <see cref="SetKind.Working"/>, so a client that does not know
/// about set kinds logs working sets, which is what every set it sent before meant.
/// </remarks>
public sealed record LiftSetCommand(
    Guid MovementDataId,
    int Reps,
    double Weight,
    WeightUnit Unit,
    double? Rpe,
    SetKind Kind = SetKind.Working) : SetCommand
{
    public override Result<SetEntry, Problem> ToEntry() =>
        Stated.ClosedEnum(Unit, nameof(Unit))
            .Bind(unit => Stated.ClosedEnum(Kind, nameof(Kind))
                .Bind(kind => Stated.Rating(Rpe, nameof(Rpe))
                    .Map(rpe => (SetEntry)new LiftSet(MovementDataId, Reps, Weight, unit, rpe, kind)
                    {
                        Source = new Formed(),
                        Facets = [],
                    })));
}

public sealed record DistanceTimeSetCommand(
    Guid MovementDataId,
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    double? Rpe) : SetCommand
{
    public override Result<SetEntry, Problem> ToEntry() =>
        Stated.ClosedEnum(Unit, nameof(Unit))
            .Bind(unit => Stated.Rating(Rpe, nameof(Rpe))
                .Map(rpe => (SetEntry)new DistanceTimeSet(MovementDataId, Distance, unit, Duration, rpe)
                {
                    Source = new Formed(),
                    Facets = [],
                }));
}

public sealed record TrailRunSetCommand(
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    double ElevationGainMeters,
    double? Rpe) : SetCommand
{
    public override Result<SetEntry, Problem> ToEntry() =>
        Stated.ClosedEnum(Unit, nameof(Unit))
            .Bind(unit => Stated.Rating(Rpe, nameof(Rpe))
                .Map(rpe => (SetEntry)new TrailRunSet(Distance, unit, Duration, ElevationGainMeters, rpe)
                {
                    Source = new Formed(),
                    Facets = [],
                }));
}

/// <summary>A set the athlete described instead of measuring. The words are the record.</summary>
public sealed record FreeformSetCommand(string Text) : SetCommand
{
    public override Result<SetEntry, Problem> ToEntry() =>
        Result<SetEntry, Problem>.Success(new FreeformSet
        {
            // Confidence describes structure lifted from the text, and nothing was lifted here —
            // the athlete chose to log loosely rather than a parser failing to break it down.
            Source = new Texted(Text, 0),
            Facets = [],
        });
}

/// <summary>
/// A prescribed set as a client may state it. Nothing on a prescription is server-owned, so this
/// mirrors <see cref="SetPrescription"/> field for field; it exists to check the unit and target
/// RPE one step before the domain constructor throws over them.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LiftPrescriptionCommand), "lift")]
[JsonDerivedType(typeof(DistanceTimePrescriptionCommand), "distanceTime")]
[JsonDerivedType(typeof(FreeformPrescriptionCommand), "freeform")]
public abstract record SetPrescriptionCommand
{
    private protected SetPrescriptionCommand() { }

    public abstract Result<SetPrescription, Problem> ToPrescription();
}

public sealed record LiftPrescriptionCommand(
    Guid MovementDataId,
    int TargetReps,
    double TargetWeight,
    WeightUnit Unit,
    double? TargetRpe) : SetPrescriptionCommand
{
    public override Result<SetPrescription, Problem> ToPrescription() =>
        Stated.ClosedEnum(Unit, nameof(Unit))
            .Bind(unit => Stated.Rating(TargetRpe, nameof(TargetRpe))
                .Map(rpe => (SetPrescription)
                    new LiftPrescription(MovementDataId, TargetReps, TargetWeight, unit, rpe)));
}

public sealed record DistanceTimePrescriptionCommand(
    Guid MovementDataId,
    double TargetDistance,
    DistanceUnit Unit,
    TimeSpan TargetDuration) : SetPrescriptionCommand
{
    public override Result<SetPrescription, Problem> ToPrescription() =>
        Stated.ClosedEnum(Unit, nameof(Unit))
            .Map(unit => (SetPrescription)
                new DistanceTimePrescription(MovementDataId, TargetDistance, unit, TargetDuration));
}

public sealed record FreeformPrescriptionCommand(string Text) : SetPrescriptionCommand
{
    public override Result<SetPrescription, Problem> ToPrescription() =>
        Result<SetPrescription, Problem>.Success(new FreeformPrescription(Text));
}
