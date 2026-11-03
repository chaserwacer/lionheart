using lionheart.Data;
using lionheart.Model.Training.Focus;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Focus;

/// <summary>Every pin an athlete has made, untracked, for the readings that weigh them.</summary>
public static class FocusPinReads
{
    public static async Task<IReadOnlyList<FocusPin>> PinsOfAsync(this ModelContext context, Guid userId, CancellationToken ct) =>
        await context.FocusPins.AsNoTracking().Where(pin => pin.UserId == userId).ToListAsync(ct);
}
