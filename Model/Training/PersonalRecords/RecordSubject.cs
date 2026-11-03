using System.Text.Json.Serialization;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// What a record is held against.
/// </summary>
/// <remarks>
/// A lift and a treadmill interval are both performed <em>on</em> a movement, so their records
/// hang off its <see cref="MovementData"/> identity. A trail run is not: the set carries no
/// movement at all, and its records are not the records a measured interval sets. Running 8 km
/// over 600 m of climb is not beaten by 10 km on the flat, so ranking the two against each other
/// would report a loss of fitness as a gain. One subject each, ranked separately.
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
