using System.Text.Json.Serialization;
using lionheart.Functional;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// Which chain of records a history read is asking for.
/// </summary>
/// <remarks>
/// An absent <see cref="MovementDataId"/> means trail running — the same flattening of the
/// subject's two cases that <see cref="PersonalRecordView"/> sends out, so a client asking about a
/// record it was given states it exactly as it received it.
/// </remarks>
public sealed record RecordHistoryQuery(Guid? MovementDataId, string Kind, int? Bracket)
{
    public Result<SubjectAndKind, Problem> Asked() =>
        RecordKind.Of(Kind, Bracket).Map(kind => new SubjectAndKind(Subject(), kind));

    private RecordSubject Subject() =>
        MovementDataId is { } movementDataId && movementDataId != Guid.Empty
            ? new RecordSubject.Movement(movementDataId)
            : new RecordSubject.TrailRunning();
}

/// <summary>One record's identity — what a chain of records shares.</summary>
public sealed record SubjectAndKind(RecordSubject Subject, RecordKind Kind);

/// <summary>
/// Which records an established read asks for: those set on one movement, those set inside one
/// window, or both.
/// </summary>
/// <remarks>
/// Each half is optional and an absent half filters nothing — unlike <see cref="RecordHistoryQuery"/>,
/// where an absent movement means trail running. A window bound is an instant, because a record's
/// <see cref="PersonalRecord.AchievedAt"/> is the instant its session started: a session's records
/// are the window from its start to its start.
/// </remarks>
public sealed record EstablishedRecordsQuery(Guid? MovementDataId, DateTimeOffset? From, DateTimeOffset? To)
{
    /// <summary>
    /// This query, or a refusal if its window runs backwards — which matches nothing, so an empty
    /// answer would read as "no records" rather than "an impossible window".
    /// </summary>
    public Result<EstablishedRecordsQuery, Problem> Ordered() =>
        To < From
            ? Result<EstablishedRecordsQuery, Problem>.Failure(
                new Problem.Invalid(nameof(To), "The window's end precedes its start."))
            : Result<EstablishedRecordsQuery, Problem>.Success(this);
}
