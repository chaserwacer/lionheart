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
