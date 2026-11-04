using System.Text.Json.Serialization;
using lionheart.Model.Analysis;

namespace lionheart.Model.Training.Sets;

/// <summary>
/// One performed set, in the richest shape the log supports. A flat ordered list of these
/// lives on the session; movement grouping is derivable from consecutive MovementDataIds.
/// Looseness is a smaller shape (<see cref="FreeformSet"/>), never nullable columns.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LiftSet), "lift")]
[JsonDerivedType(typeof(DistanceTimeSet), "distanceTime")]
[JsonDerivedType(typeof(TrailRunSet), "trailRun")]
[JsonDerivedType(typeof(EnduranceSet), "endurance")]
[JsonDerivedType(typeof(FreeformSet), "freeform")]
public abstract record SetEntry
{
    public required Utterance Source { get; init; }

    /// <summary>Per-set closed-vocab facets (cheap); embeddings and valence stay session-level.</summary>
    public required IReadOnlyList<Facet> Facets { get; init; }
}

// Each measure is guarded in a redeclared member. Every positional member is redeclared, not
// only the guarded ones: System.Text.Json writes non-redeclared positional members first, so a
// partial redeclaration would silently reorder the stored payload (pinned in StorageFormatTests).

/// <remarks>
/// <see cref="Kind"/> is the last member so a stored lift without one still reads, as
/// <see cref="SetKind.Working"/>: the serializer leaves an absent constructor parameter at its
/// default, and Working is the enum's zero.
/// </remarks>
public sealed record LiftSet(
    Guid MovementDataId,
    int Reps,
    double Weight,
    WeightUnit Unit,
    Rpe Rpe,
    SetKind Kind) : SetEntry
{
    public Guid MovementDataId { get; init; } = MovementDataId;
    public int Reps { get; init; } = Reps;
    public double Weight { get; init; } = Require.Finite(Weight, nameof(Weight));
    public WeightUnit Unit { get; init; } = Require.ClosedEnum(Unit, nameof(Unit));
    public Rpe Rpe { get; init; } = Rpe;
    public SetKind Kind { get; init; } = Require.ClosedEnum(Kind, nameof(Kind));
}

public sealed record DistanceTimeSet(
    Guid MovementDataId,
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    Rpe Rpe) : SetEntry
{
    public Guid MovementDataId { get; init; } = MovementDataId;
    public double Distance { get; init; } = Require.Finite(Distance, nameof(Distance));
    public DistanceUnit Unit { get; init; } = Require.ClosedEnum(Unit, nameof(Unit));
    public TimeSpan Duration { get; init; } = Duration;
    public Rpe Rpe { get; init; } = Rpe;
}

public sealed record TrailRunSet(
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    double ElevationGainMeters,
    Rpe Rpe) : SetEntry
{
    public double Distance { get; init; } = Require.Finite(Distance, nameof(Distance));
    public DistanceUnit Unit { get; init; } = Require.ClosedEnum(Unit, nameof(Unit));
    public TimeSpan Duration { get; init; } = Duration;

    /// <summary>
    /// The climb as stated: the athlete's own figure, or, for a run imported from Apple Health, its
    /// total ascent. Its sign is not policed, because a figure an athlete states may
    /// be a net gain, which a descent-dominant route makes negative.
    /// </summary>
    public double ElevationGainMeters { get; init; } = Require.Finite(ElevationGainMeters, nameof(ElevationGainMeters));

    public Rpe Rpe { get; init; } = Rpe;
}

/// <summary>
/// A ride, a hike, a walk or an indoor run: an endurance session that names no movement, measured
/// the way a trail run is.
/// </summary>
/// <remarks>
/// Its own shape rather than a trail run carrying an activity, because a ride is not a run: what
/// it competes for records on, if anything, follows from <see cref="Activity"/>
/// (<c>RecordCandidates</c>), and an outdoor run stays a <see cref="TrailRunSet"/>.
/// </remarks>
public sealed record EnduranceSet(
    EnduranceActivity Activity,
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    double ElevationGainMeters,
    Rpe Rpe) : SetEntry
{
    public EnduranceActivity Activity { get; init; } = Require.ClosedEnum(Activity, nameof(Activity));
    public double Distance { get; init; } = Require.Finite(Distance, nameof(Distance));
    public DistanceUnit Unit { get; init; } = Require.ClosedEnum(Unit, nameof(Unit));
    public TimeSpan Duration { get; init; } = Duration;

    /// <summary>The climb as stated, its sign unpoliced for the reason a trail run's is.</summary>
    public double ElevationGainMeters { get; init; } = Require.Finite(ElevationGainMeters, nameof(ElevationGainMeters));

    public Rpe Rpe { get; init; } = Rpe;
}

/// <summary>
/// A first-class loose log — structure lives in <see cref="SetEntry.Source"/>'s raw text
/// and the facets. Upgrading to a richer type later is a re-parse, never a data loss.
/// </summary>
public sealed record FreeformSet : SetEntry;
