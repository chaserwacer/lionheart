using lionheart.Data;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Profile;

/// <summary>
/// Event-driven invalidation of the athlete context card's volatile Recent State tier, for every
/// write that changes something the tier states.
/// </summary>
/// <remarks>
/// Marking it stale rather than recomputing keeps the write path cheap: the card is rebuilt lazily
/// on its next read (<see cref="IAthleteContextCardService.GetOrBuildCardAsync"/>). Staged into the
/// caller's own save, so it costs no extra round trip and lands only if the write does.
/// </remarks>
internal static class RecentStateInvalidation
{
    extension(ModelContext context)
    {
        public async Task StageRecentStateInvalidationAsync(Guid userId, CancellationToken ct = default)
        {
            var card = await context.AthleteContextCards.FirstOrDefaultAsync(c => c.UserID == userId, ct);
            if (card is not null)
            {
                card.RecentStateAsOf = DateTime.MinValue;
            }
        }
    }
}
