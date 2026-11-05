using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;

namespace lionheart.Services.Training.Availability;

/// <summary>Each day of a range read from the athlete's non-training days and his planned and trained sessions.</summary>
/// <remarks>Pure, so the precedence between the statements is tested without a database.</remarks>
public static class AvailabilityAnalysis
{
    /// <param name="trained">The days a session was completed on, walks aside.</param>
    public static AvailabilityCalendar Of(
        IReadOnlyList<NonTrainingDay> marked, IReadOnlySet<DateOnly> planned, IReadOnlySet<DateOnly> trained, DayRange range) =>
        new(range, [.. Enumerable.Range(0, range.Days).Select(range.From.AddDays)
            .Select(day => trained.Contains(day) ? new DayAvailability.Trained(day) : DayOf(marked, planned, day))]);

    /// <summary>
    /// A date marked off, else a planned session, else a weekday marked off, else open. Where two
    /// statements of one kind cover the day, the one marked first names it.
    /// </summary>
    private static DayAvailability DayOf(IReadOnlyList<NonTrainingDay> marked, IReadOnlySet<DateOnly> planned, DateOnly day)
    {
        var covering = marked.Where(statement => statement.Covers(day)).OrderBy(statement => statement.MarkedAt).ToList();
        var once = covering.FirstOrDefault(statement => !statement.Schedule.Repeats);
        var weekly = covering.FirstOrDefault(statement => statement.Schedule.Repeats);

        return (once, planned.Contains(day), weekly) switch
        {
            ({ } date, _, _) => NonTraining(day, date),
            (null, true, _) => new DayAvailability.Planned(day),
            (null, false, { } weekday) => NonTraining(day, weekday),
            (null, false, null) => new DayAvailability.Open(day),
        };
    }

    private static DayAvailability.NonTraining NonTraining(DateOnly day, NonTrainingDay statement) =>
        new(day, statement.NonTrainingDayId, statement.Note, statement.Schedule.Repeats);
}
