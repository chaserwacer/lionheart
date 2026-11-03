using System.Diagnostics;
using System.Text.Json.Serialization;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// An achievement on the wire, carrying the derived figures the domain record deliberately does
/// not store.
/// </summary>
/// <remarks>
/// The sealed shape is the domain's, which is what makes the generated TypeScript and the Swift
/// decoder mirror it. It is restated rather than serialized directly for one reason: an estimated
/// one-rep max, a volume and a pace are all functions of the members beside them, so they are
/// <c>[JsonIgnore]</c>d out of the stored JSON — and a client that re-derived them would be the
/// second place the arithmetic lived. The view is where a derived figure belongs: computed once,
/// per response, from the stored truth.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(HeaviestSetView), "heaviestSet")]
[JsonDerivedType(typeof(BestSetVolumeView), "bestSetVolume")]
[JsonDerivedType(typeof(RepMaxView), "repMax")]
[JsonDerivedType(typeof(BestSessionVolumeView), "bestSessionVolume")]
[JsonDerivedType(typeof(LongestDistanceView), "longestDistance")]
[JsonDerivedType(typeof(FastestTimeView), "fastestTime")]
[JsonDerivedType(typeof(BestPaceView), "bestPace")]
public abstract record AchievementView
{
    private protected AchievementView() { }

    public static AchievementView Of(Achievement achievement) => achievement switch
    {
        Achievement.HeaviestSet heaviest => new HeaviestSetView(
            heaviest.Lift.Weight, heaviest.Lift.Unit, heaviest.Lift.Reps,
            heaviest.Lift.EstimatedOneRepMax),
        Achievement.BestSetVolume best => new BestSetVolumeView(
            best.Lift.Volume, best.Lift.Unit, best.Lift.Weight, best.Lift.Reps),
        Achievement.RepMax repMax => new RepMaxView(
            repMax.Reps, repMax.Lift.Weight, repMax.Lift.Unit, repMax.Lift.EstimatedOneRepMax,
            repMax.Lift.Reps),
        Achievement.BestSessionVolume session => new BestSessionVolumeView(
            session.Total.Volume, session.Total.Unit),
        Achievement.LongestDistance longest => new LongestDistanceView(
            longest.Effort.Distance, longest.Effort.Unit, longest.Effort.Duration),
        Achievement.FastestTime fastest => new FastestTimeView(
            fastest.Effort.BracketMeters, fastest.Effort.Distance, fastest.Effort.Unit,
            fastest.Effort.Duration),
        Achievement.BestPace pace => new BestPaceView(
            pace.Effort.PacePerKilometer, pace.Effort.PacePerMile, pace.Effort.Distance,
            pace.Effort.Unit, pace.Effort.Duration),
        _ => throw new UnreachableException()
    };
}

public sealed record HeaviestSetView(
    double Weight, WeightUnit Unit, int Reps, double EstimatedOneRepMax) : AchievementView;

public sealed record BestSetVolumeView(
    double Volume, WeightUnit Unit, double Weight, int Reps) : AchievementView;

/// <summary>
/// <see cref="Reps"/> is the rep count the record is held at; <see cref="SetReps"/> is how many
/// reps the set holding it had, which is more when the record is the front of a longer set.
/// </summary>
/// <remarks>
/// <see cref="SetReps"/> is last and new beside a <see cref="Reps"/> that keeps its meaning, so a
/// client reading only the bracket still files the record in the right cell. The estimate is the
/// set's, as performed.
/// </remarks>
public sealed record RepMaxView(
    int Reps, double Weight, WeightUnit Unit, double EstimatedOneRepMax, int SetReps) : AchievementView;

public sealed record BestSessionVolumeView(double Volume, WeightUnit Unit) : AchievementView;

public sealed record LongestDistanceView(
    double Distance, DistanceUnit Unit, TimeSpan Duration) : AchievementView;

/// <summary>The bracket is the distance the record is held at, in whole meters.</summary>
public sealed record FastestTimeView(
    int DistanceMeters, double Distance, DistanceUnit Unit, TimeSpan Duration) : AchievementView;

public sealed record BestPaceView(
    TimeSpan PacePerKilometer,
    TimeSpan PacePerMile,
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration) : AchievementView;

/// <summary>Whether a record stands, was beaten, or was withdrawn.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecordStandingView>))]
public enum RecordStandingView
{
    Standing,
    Beaten,
    Retracted
}

/// <summary>
/// One record on the wire.
/// </summary>
/// <remarks>
/// <see cref="MovementDataId"/> is absent on a trail-running record, which is the subject's two
/// cases flattened to the one field a client needs — it already holds the movement catalogue, so
/// the movement itself is deliberately not embedded. The movement is a different aggregate, and
/// loading it per record is three joins a records screen does not need.
/// </remarks>
public sealed record PersonalRecordView(
    Guid PersonalRecordId,
    Guid? MovementDataId,
    AchievementView Achievement,
    DateTime AchievedAt,
    DateTime? PreviousAchievedAt,
    RecordStandingView Standing)
{
    public static PersonalRecordView Of(PersonalRecord record) => new(
        record.PersonalRecordId,
        record.Subject is RecordSubject.Movement movement ? movement.MovementDataId : null,
        AchievementView.Of(record.Achievement),
        record.AchievedAt,
        record.Lineage is RecordLineage.Replaced replaced ? replaced.AchievedAt : null,
        record.Standing switch
        {
            RecordStanding.Current => RecordStandingView.Standing,
            RecordStanding.Beaten => RecordStandingView.Beaten,
            RecordStanding.Retracted => RecordStandingView.Retracted,
            _ => throw new UnreachableException()
        });
}

/// <summary>
/// Every record currently standing on one subject.
/// </summary>
/// <remarks>
/// A list rather than a field per kind. With a rep-max grid a movement carries up to fourteen
/// standing records in each weight unit, and naming each of them would make every new kind a wire
/// break; the client switches on the achievement's own discriminator instead.
/// </remarks>
public sealed record SubjectRecordsView(
    Guid? MovementDataId,
    IReadOnlyList<PersonalRecordView> Records,
    DateTime? LastAchievedAt)
{
    /// <remarks>
    /// Newest first, then by kind and bracket. Every record one session set carries that session's
    /// instant, so the date alone leaves the order of a movement's fresh records undefined — and a
    /// rep-max grid reads as a grid only if its rows arrive in rep order.
    /// </remarks>
    public static SubjectRecordsView Of(RecordSubject subject, IReadOnlyList<PersonalRecord> standing) => new(
        subject is RecordSubject.Movement movement ? movement.MovementDataId : null,
        [
            .. standing
                .OrderByDescending(record => record.AchievedAt)
                .ThenBy(record => record.Kind.Name, StringComparer.Ordinal)
                .ThenBy(record => record.Kind.Bracket)
                .Select(PersonalRecordView.Of)
        ],
        standing.Count == 0 ? null : standing.Max(record => record.AchievedAt));
}

/// <summary>
/// What became of a subject's record once the standing one was withdrawn.
/// </summary>
/// <remarks>
/// A discriminated success rather than a nullable body: a retraction that leaves nothing standing
/// is an outcome the client has to say something about ("no record yet" rather than a blank), and
/// a null in the response slot makes that indistinguishable from a field the server forgot.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Restored), "restored")]
[JsonDerivedType(typeof(NothingStands), "nothingStands")]
public abstract record RetractionOutcome
{
    private protected RetractionOutcome() { }

    /// <summary>
    /// The record standing for this subject and kind once the withdrawn one is gone — ordinarily
    /// the one it had beaten, back again.
    /// </summary>
    public sealed record Restored(PersonalRecordView Record) : RetractionOutcome;

    /// <summary>Nothing stands for this subject and kind once the withdrawn record is gone.</summary>
    public sealed record NothingStands : RetractionOutcome;
}
