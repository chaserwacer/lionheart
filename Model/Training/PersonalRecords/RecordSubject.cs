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
/// Two cases rather than a nullable movement id: a record with no movement is a trail-running
/// record, which is a fact about the record rather than a missing field.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Movement), "movement")]
[JsonDerivedType(typeof(TrailRunning), "trailRunning")]
public abstract record RecordSubject
{
    private protected RecordSubject() { }

    public sealed record Movement : RecordSubject
    {
        public Movement(Guid movementDataId)
        {
            // Guid.Empty is the storage sentinel for trail running; letting it in here would
            // rehydrate as the wrong subject and merge two unrelated sets of records.
            if (movementDataId == Guid.Empty)
                throw new ArgumentException(
                    "A movement subject needs a real movement id; use TrailRunning.",
                    nameof(movementDataId));

            MovementDataId = movementDataId;
        }

        public Guid MovementDataId { get; }
    }

    /// <summary>Trail running as a whole — the one subject whose sets name no movement.</summary>
    public sealed record TrailRunning : RecordSubject;
}
