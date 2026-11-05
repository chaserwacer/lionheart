using lionheart.Data;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Expectations;

/// <summary>An athlete's injuries as the expectations read them, untracked.</summary>
public static class InjuryReads
{
    /// <remarks>
    /// Every injury, resolved ones too: a handful per athlete, and which apply to a day is the
    /// analysis's call. A check-in's day is its stamped UTC creation, close enough to his own day for
    /// "named by then".
    /// </remarks>
    public static async Task<IReadOnlyList<InjuryRecord>> InjuryRecordsAsync(this ModelContext context, Guid userId, CancellationToken ct) =>
        [.. (await context.Injuries.AsNoTracking()
                .Where(injury => injury.UserID == userId)
                .Include(injury => injury.InjuryEvents)
                .ToListAsync(ct))
            .Select(injury => new InjuryRecord(
                injury.InjuryID,
                injury.Name,
                injury.InjuryDate,
                InjuryStanding.Of(injury.IsActive, injury.ResolvedOn),
                injury.Families,
                [.. injury.InjuryEvents
                    .SelectMany(check => check.MovementIDs.Select(movement => (Movement: movement, Day: DateOnly.FromDateTime(check.CreationTime))))
                    .GroupBy(named => named.Movement)
                    .Select(named => new NamedMovement(named.Key, named.Min(each => each.Day)))],
                injury.InjuryEvents.OrderByDescending(check => check.CreationTime).Select(check => (int?)check.PainLevel).FirstOrDefault()))];
}
