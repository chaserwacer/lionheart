using lionheart.Data;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Sessions;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Availability;

/// <summary>An athlete's availability over a range, untracked, for every reading that prorates to it.</summary>
public static class AvailabilityReads
{
    /// <remarks>
    /// Every non-training day is read rather than those starting in the range, because a weekly
    /// one marked months ago still covers this week; an athlete holds a handful of them.
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

        return AvailabilityAnalysis.Of(marked, planned.ToHashSet(), range);
    }
}
