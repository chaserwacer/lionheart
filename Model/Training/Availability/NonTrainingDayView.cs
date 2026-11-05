namespace lionheart.Model.Training.Availability;

/// <summary>A non-training day on the wire: the statement less its owner, whom every route already knows.</summary>
public sealed record NonTrainingDayView(Guid NonTrainingDayId, NonTrainingSchedule Schedule, string Note, DateTime MarkedAt)
{
    public static NonTrainingDayView Of(NonTrainingDay day) => new(day.NonTrainingDayId, day.Schedule, day.Note, day.MarkedAt);
}
