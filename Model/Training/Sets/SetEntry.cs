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
[JsonDerivedType(typeof(FreeformSet), "freeform")]
public abstract record SetEntry
{
    public required Utterance Source { get; init; }

    /// <summary>Per-set closed-vocab facets (cheap); embeddings and valence stay session-level.</summary>
    public required IReadOnlyList<Facet> Facets { get; init; }
}

public sealed record LiftSet(
    Guid MovementDataId,
    int Reps,
    double Weight,
    WeightUnit Unit,
    Rpe Rpe) : SetEntry;

public sealed record DistanceTimeSet(
    Guid MovementDataId,
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    Rpe Rpe) : SetEntry;

public sealed record TrailRunSet(
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    double ElevationGainMeters,
    Rpe Rpe) : SetEntry;

/// <summary>
/// A first-class loose log — structure lives in <see cref="SetEntry.Source"/>'s raw text
/// and the facets. Upgrading to a richer type later is a re-parse, never a data loss.
/// </summary>
public sealed record FreeformSet : SetEntry;
