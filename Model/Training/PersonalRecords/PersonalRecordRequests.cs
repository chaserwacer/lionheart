using System.Text.Json.Serialization;
using lionheart.Functional;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// Which chain of records a history read is asking for.
/// </summary>
/// <remarks>
/// The subject as <see cref="PersonalRecordView"/> sends it out — a movement id, or a
/// <see cref="RecordSubjectKind"/> for a subject that names none — so a client asking about a
/// record it was given states it exactly as it received it. Neither is trail running, as it was
/// before riding and hiking had records.
/// </remarks>
public sealed record RecordHistoryQuery(Guid? MovementDataId, string Kind, int? Bracket, RecordSubjectKind? Subject = null)
{
    public Result<SubjectAndKind, Problem> Asked() =>
        Named().Bind(subject => RecordKind.Of(Kind, Bracket).Map(kind => new SubjectAndKind(subject, kind)));

    private Result<RecordSubject, Problem> Named() => (MovementDataId, Subject) switch
    {
        ({ } id, null or RecordSubjectKind.Movement) when !RecordSubject.Movement.IsSentinel(id) =>
            Result<RecordSubject, Problem>.Success(new RecordSubject.Movement(id)),
        (null, null or RecordSubjectKind.TrailRunning) => Result<RecordSubject, Problem>.Success(new RecordSubject.TrailRunning()),
        (null, RecordSubjectKind.Riding) => Result<RecordSubject, Problem>.Success(new RecordSubject.Riding()),
        (null, RecordSubjectKind.Hiking) => Result<RecordSubject, Problem>.Success(new RecordSubject.Hiking()),
        _ => Result<RecordSubject, Problem>.Failure(new Problem.Invalid(
            nameof(Subject), "A record's subject is a movement id, or riding, hiking or trail running with no id.")),
    };
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
