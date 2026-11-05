using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.GoingIn;
using lionheart.Services.Chat.Today;
using lionheart.Services.Training.Balance;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.GoingIn;

public interface IGoingInService
{
    /// <summary>How ready the athlete is for a session now, on his own clock.</summary>
    Task<Result<GoingInState, Problem>> GetAsync(IdentityUser user, CancellationToken ct);
}

/// <summary>
/// Reads what the chat's briefing reads, the week's volume and the active injuries, and hands them to
/// <see cref="GoingInAnalysis"/>.
/// </summary>
/// <remarks>
/// The nights, readiness, wellness, plans and training are the briefing's facts rather than reads of
/// its own, so the rating and what the chat says about today cannot disagree. Two things the
/// briefing does not hold are read here: the muscles each set trained, from the volume service, and
/// the movements an injury's check-ins name. Reading the facts also reads the week means, which the
/// rating does not use; for one athlete opening one surface that costs a fraction of a second, and
/// keeping one reading of today is worth it.
/// </remarks>
public sealed class GoingInService(ModelContext context, ITodayFacts today, ITrainingVolumeService volume)
    : IGoingInService
{
    private readonly ModelContext _context = context;
    private readonly ITodayFacts _today = today;
    private readonly ITrainingVolumeService _volume = volume;

    public Task<Result<GoingInState, Problem>> GetAsync(IdentityUser user, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .MapAsync(async userId => (Facts: await _today.ReadAsync(user, ct), Injuries: await InjuriesAsync(userId, ct)))
            .BindAsync(read => _volume.GetVolumeAsync(user, read.Facts.Now.Today, ct)
                .MapAsync(week => GoingInAnalysis.Of(read.Facts, week.Muscles, read.Injuries)));

    /// <summary>
    /// Active injuries with the movements their check-ins name, newest check-in's pain first in
    /// line: the movements are what the logging surface marks.
    /// </summary>
    private async Task<IReadOnlyList<InjuryNote>> InjuriesAsync(Guid userId, CancellationToken ct) =>
        [.. (await _context.Injuries.AsNoTracking()
                .Where(injury => injury.UserID == userId && injury.IsActive)
                .Include(injury => injury.InjuryEvents)
                .OrderBy(injury => injury.InjuryDate)
                .ToListAsync(ct))
            .Select(injury => new InjuryNote(
                injury.InjuryID,
                injury.Name,
                injury.InjuryEvents.OrderByDescending(e => e.CreationTime).Select(e => (int?)e.PainLevel).FirstOrDefault(),
                [.. injury.InjuryEvents.SelectMany(e => e.MovementIDs).Distinct()]))];
}
