using lionheart.Data;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Cardio;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Load;

/// <summary>An athlete's everyday load over a range, untracked, for every reader that weighs it.</summary>
public static class EverydayLoadReads
{
    /// <remarks>
    /// Reads the range and the 90 days before it, which its usual is drawn from. Training is every
    /// logged session with a start and an end, and every ring workout that could be a cardio
    /// session — one the athlete entered, or one its heart rate might admit — without reading the
    /// heart rate: a game of catch the ring guessed, housework and short walks stay everyday load,
    /// and a detected walk whose heart rate did not show training is left out of both, the cost of
    /// not reading a year of heart rate to say so. Sessions and workouts are found by their stamped
    /// UTC start a day either side of the span.
    /// </remarks>
    public static async Task<EverydayLoad> EverydayAsync(
        this ModelContext context, Guid userId, DayRange range, DateOnly today, CancellationToken ct)
    {
        var first = range.From.AddDays(-EverydayLoadAnalysis.UsualDays);
        var activity = await context.OuraDailyActivity
            .AsNoTracking()
            .Where(day => day.UserId == userId && day.Day >= first && day.Day <= range.To)
            .ToListAsync(ct);

        var from = first.ToDateTime(TimeOnly.MinValue).AddDays(-1);
        var to = range.To.ToDateTime(TimeOnly.MinValue).AddDays(2);
        var logged = await context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(session => session.UserId == userId
                              && EF.Property<DateTime>(session, "StartTime") >= from
                              && EF.Property<DateTime>(session, "StartTime") < to)
            .ToListAsync(ct);
        var ringWorkouts = await context.OuraWorkouts
            .AsNoTracking()
            .Where(workout => workout.UserId == userId
                              && EF.Property<DateTime>(workout, "StartTime") >= from
                              && EF.Property<DateTime>(workout, "StartTime") < to)
            .ToListAsync(ct);

        var gathered = CardioAnalysis.Gather(ringWorkouts, logged);
        IReadOnlyList<TrainingSpan> training =
        [
            .. logged.Select(session => SessionMatching.Span(session.Period)).OfType<(DateTimeOffset Start, DateTimeOffset End)>()
                .Select(span => new TrainingSpan(span.Start, span.End)),
            .. gathered.Sessions.Concat(gathered.Candidates).Select(session => new TrainingSpan(session.Start, session.End)),
        ];

        return EverydayLoadAnalysis.Of(
            [.. activity
                .Where(day => day.Reading.Timeline is not null)
                .Select(day => new ActivityDay(day.Day, day.Reading.Timeline!, day.Reading.NonWearTimeSeconds))],
            training,
            range,
            today);
    }
}
