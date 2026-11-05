using lionheart.Data;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Balance;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Availability;

/// <summary>An athlete's availability over a range, untracked, for every reading that prorates to it.</summary>
public static class AvailabilityReads
{
    /// <remarks>
    /// Every non-training day is read rather than those starting in the range, because a weekly
    /// one marked months ago still covers this week; an athlete holds a handful of them. Completed
    /// sessions are found by the stamped UTC start a day either side of the range, then placed on
    /// the athlete's own day, which is what a session's day is.
    /// </remarks>
    public static async Task<AvailabilityCalendar> AvailabilityAsync(
        this ModelContext context, Guid userId, DayRange range, CancellationToken ct)
    {
        var marked = await context.NonTrainingDays.AsNoTracking().Where(day => day.UserId == userId).ToListAsync(ct);
        var planned = await context.Sessions.OfType<PlannedTrainingSession>()
            .AsNoTracking()
            .Where(session => session.UserId == userId && session.PlannedFor >= range.From && session.PlannedFor <= range.To)
            .Select(session => session.PlannedFor)
            .ToListAsync(ct);

        var from = range.From.ToDateTime(TimeOnly.MinValue).AddDays(-1);
        var to = range.To.ToDateTime(TimeOnly.MinValue).AddDays(2);
        var completed = await context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(session => session.UserId == userId
                              && EF.Property<DateTime>(session, "StartTime") >= from
                              && EF.Property<DateTime>(session, "StartTime") < to)
            .ToListAsync(ct);
        var trained = completed
            .Where(session => !TrainedSessions.OnlyWalking(session.Sets))
            .Select(session => session.Date)
            .Where(day => day >= range.From && day <= range.To);

        return AvailabilityAnalysis.Of(marked, planned.ToHashSet(), trained.ToHashSet(), range);
    }
}
