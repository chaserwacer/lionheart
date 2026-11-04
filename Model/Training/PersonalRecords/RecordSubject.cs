using System.Text.Json.Serialization;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// What a record is held against.
/// </summary>
/// <remarks>
/// A lift and a treadmill interval are both performed <em>on</em> a movement, so their records
/// hang off its <see cref="MovementData"/> identity. A trail run is not: the set names no
/// movement at all, so there is nothing to hang one off, and the running it describes is not the
/// running a measured interval describes — a route's distance is whatever the route was, where an
/// interval's is a number the athlete chose. One subject each, so a season of trail runs ranks
/// against itself rather than against the treadmill.
/// <para>
/// Within trail running, terrain is not part of the identity: two routes of the same distance and
/// different climb hold one record between them, and the harder one takes it only by being
/// quicker. Keying on climb would need a bracket nobody logs deliberately, so the honest reading
/// is that these are an athlete's trail-running records rather than a given route's.
/// </para>
/// <para>
/// Riding and hiking are subjects of their own for the same reason: a ride or a hike recorded
/// along a route names no movement, and a season of rides ranks against itself, not against the
/// runs. An indoor ride or run, and any walk, has no subject at all (<c>RecordCandidates</c>).
/// </para>
/// <para>
/// Cases rather than a nullable movement id: a record with no movement is a trail-running, riding
/// or hiking record, which is a fact about the record rather than a missing field.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Movement), "movement")]
[JsonDerivedType(typeof(TrailRunning), "trailRunning")]
[JsonDerivedType(typeof(Riding), "riding")]
[JsonDerivedType(typeof(Hiking), "hiking")]
public abstract record RecordSubject
{
    private protected RecordSubject() { }

    public sealed record Movement : RecordSubject
    {
        public Movement(Guid movementDataId)
        {
            // The movement-less subjects are stored as sentinel ids; letting one in here would
            // rehydrate as the wrong subject and merge two unrelated sets of records.
            if (IsSentinel(movementDataId))
                throw new ArgumentException(
                    "A movement subject needs a real movement id; use TrailRunning, Riding or Hiking.",
                    nameof(movementDataId));

            MovementDataId = movementDataId;
        }

        public Guid MovementDataId { get; }

        /// <summary>
        /// Whether an id is one the subjects that name no movement are stored as: Guid.Empty for
        /// trail running, and the next two ids up for riding and hiking. No generated id is either.
        /// </summary>
        public static bool IsSentinel(Guid id) => id == Guid.Empty || id == RidingSentinel || id == HikingSentinel;

        public static readonly Guid RidingSentinel = new("00000000-0000-0000-0000-000000000001");
        public static readonly Guid HikingSentinel = new("00000000-0000-0000-0000-000000000002");
    }

    /// <summary>Trail running as a whole: every outdoor run, whatever its surface.</summary>
    public sealed record TrailRunning : RecordSubject;

    /// <summary>Riding outdoors as a whole, on any bike.</summary>
    public sealed record Riding : RecordSubject;

    /// <summary>Hiking as a whole.</summary>
    public sealed record Hiking : RecordSubject;

    /// <summary>What the subject is called: the movement's own name, or the activity's.</summary>
    public string Name(Func<Guid, string> movementNamed) => this switch
    {
        Movement movement => movementNamed(movement.MovementDataId),
        TrailRunning => "trail running",
        Riding => "riding",
        Hiking => "hiking",
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}
