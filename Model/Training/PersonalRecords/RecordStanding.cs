using System.Text.Json.Serialization;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// Where a record sits in the chain of records of its kind on its subject.
/// </summary>
/// <remarks>
/// Three states rather than two. Beaten and retracted both mean "not the current record" and are
/// nothing alike to read: one is the athlete's own progress, the other a number they said was
/// never real. Answering both with one bit leaves a history that cannot say which happened.
/// <para>
/// Only <see cref="Current"/> is the record that stands. Which state a record is in is settled in
/// the same save as its cause, so it cannot drift from the chain it describes.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Current), "standing")]
[JsonDerivedType(typeof(Beaten), "beaten")]
[JsonDerivedType(typeof(Retracted), "retracted")]
public abstract record RecordStanding
{
    private protected RecordStanding() { }

    /// <summary>The current record of its kind on its subject.</summary>
    public sealed record Current : RecordStanding;

    /// <summary>A record the athlete went on to beat. Kept — the progression is the point.</summary>
    public sealed record Beaten(DateTime At) : RecordStanding;

    /// <summary>
    /// A record the athlete withdrew, because the set behind it was not real.
    /// </summary>
    /// <remarks>
    /// Only the standing record can be withdrawn, so a beaten one is never retracted: the history
    /// stays a record of what actually stood, and retracting walks back one link at a time.
    /// </remarks>
    public sealed record Retracted(DateTime At) : RecordStanding;
}

/// <summary>
/// What a record replaced, if anything.
/// </summary>
/// <remarks>
/// The predecessor's own date travels with the pointer, so a client can say how long the last
/// record stood without a second read. Copying it is safe where copying usually is not: a record's
/// achievement date is the instant a session was performed, and that never changes.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FirstOfItsKind), "first")]
[JsonDerivedType(typeof(Replaced), "replaced")]
public abstract record RecordLineage
{
    private protected RecordLineage() { }

    /// <summary>The first record of its kind on its subject — nothing behind it to restore.</summary>
    public sealed record FirstOfItsKind : RecordLineage;

    public sealed record Replaced(Guid PersonalRecordId, DateTime AchievedAt) : RecordLineage;
}
