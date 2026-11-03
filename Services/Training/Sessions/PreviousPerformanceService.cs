using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Sessions;

public interface IPreviousPerformanceService
{
    Task<Result<IReadOnlyList<PreviousPerformanceView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct);
}

/// <summary>The athlete's previous performance on every movement they have trained.</summary>
/// <remarks>
/// Reads every completed session: the sets live in a JSON column, so which sessions hold a
/// movement cannot be asked of the database. At one athlete's volume that is a few hundred rows.
/// </remarks>
public sealed class PreviousPerformanceService : IPreviousPerformanceService
{
    private readonly ModelContext _context;

    public PreviousPerformanceService(ModelContext context) => _context = context;

    public async Task<Result<IReadOnlyList<PreviousPerformanceView>, Problem>> GetAllAsync(
        IdentityUser user, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var sessions = await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .ToListAsync(ct);

        return Result<IReadOnlyList<PreviousPerformanceView>, Problem>.Success(PreviousPerformances.Of(sessions));
    }
}
