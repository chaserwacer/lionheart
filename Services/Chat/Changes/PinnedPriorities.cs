using lionheart.Data;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Changes;

internal static class PinnedPriorities
{
    extension(ModelContext context)
    {
        /// <summary>
        /// The family's pinned priorities — one, or none, in practice — which a new priority lets go:
        /// read when the card names them and again when it is confirmed.
        /// </summary>
        public async Task<IReadOnlyList<FocusPin>> PinnedPrioritiesAsync(Guid userId, PatternFamily family, CancellationToken ct) =>
            // The status and the role are JSON columns, so which pins lead the family is read here.
            [.. (await context.FocusPins.AsNoTracking().Where(pin => pin.UserId == userId).ToListAsync(ct))
                .Where(pin => pin.IsPinned && pin.Role.IsPriorityFor(family))
                .OrderBy(pin => pin.FocusPinId)];
    }
}
