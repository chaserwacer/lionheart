namespace lionheart.Model.Training.Availability;

/// <summary>
/// A day, or a weekday every week, the athlete has said he will not train — travel, a busy day —
/// with what he said about it.
/// </summary>
/// <remarks>
/// A sealed class rather than a record for the reason <see cref="Focus.FocusPin"/> gives: it has
/// identity, and removing it removes this statement and no other. It is never revised; a different
/// schedule or note is a different statement, made after removing this one.
/// </remarks>
public sealed class NonTrainingDay
{
    private NonTrainingDay(Guid nonTrainingDayId, Guid userId, NonTrainingSchedule schedule, string note, DateTime markedAt)
    {
        NonTrainingDayId = nonTrainingDayId;
        UserId = userId;
        Schedule = schedule;
        Note = note;
        MarkedAt = markedAt;
    }

    public const int MostNoteLength = 200;

    public Guid NonTrainingDayId { get; private init; }

    public Guid UserId { get; private init; }

    public NonTrainingSchedule Schedule { get; private init; }

    /// <summary>What the athlete said about it — "travel", "exam" — or empty.</summary>
    public string Note { get; private init; }

    /// <summary>When it was marked: the write instant, unlike the schedule, which is the athlete's days.</summary>
    public DateTime MarkedAt { get; private init; }

    public static NonTrainingDay Mark(
        Guid nonTrainingDayId, Guid userId, NonTrainingSchedule schedule, string note, DateTime markedAt) =>
        new(nonTrainingDayId, userId, schedule, note, markedAt);

    public bool Covers(DateOnly day) => Schedule.Covers(day);
}
