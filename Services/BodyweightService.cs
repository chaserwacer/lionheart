using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.BodyMeasurements;
using lionheart.Services.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

public interface IBodyweightService
{
    Task<Result<BodyweightView, Problem>> LogAsync(
        IdentityUser user, DateOnly day, BodyweightRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<BodyweightView>, Problem>> GetRangeAsync(
        IdentityUser user, DayRange range, CancellationToken ct);

    Task<Result<BodyweightView, Problem>> GetLatestAsync(IdentityUser user, CancellationToken ct);

    Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, DateOnly day, CancellationToken ct);
}

/// <summary>
/// The athlete's dated bodyweight: one measurement per day, logged again to restate it.
/// </summary>
/// <remarks>
/// A reader that needs the bodyweight on a day — the system load of a weighted dip months ago —
/// reads a range and takes the newest measurement on or before that day. That rule belongs to the
/// reader, so nothing here answers for a day that has no measurement of its own.
/// </remarks>
public sealed class BodyweightService : IBodyweightService
{
    private readonly ModelContext _context;

    public BodyweightService(ModelContext context) => _context = context;

    public Task<Result<BodyweightView, Problem>> LogAsync(
        IdentityUser user, DateOnly day, BodyweightRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => Bodyweight.Of(request.Weight, request.Unit)
                .MapAsync(bodyweight => LoggedAsync(userId, day, bodyweight, ct)));

    public Task<Result<IReadOnlyList<BodyweightView>, Problem>> GetRangeAsync(
        IdentityUser user, DayRange range, CancellationToken ct) =>
        range.Ordered().MapAsync(ordered => InRangeAsync(Guid.Parse(user.Id), ordered, ct));

    public async Task<Result<BodyweightView, Problem>> GetLatestAsync(IdentityUser user, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var latest = await _context.BodyweightMeasurements
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Day)
            .FirstOrDefaultAsync(ct);

        return latest is null
            ? Result<BodyweightView, Problem>.Failure(new Problem.NotFound("No bodyweight has been logged."))
            : Result<BodyweightView, Problem>.Success(BodyweightView.Of(latest));
    }

    public Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, DateOnly day, CancellationToken ct) =>
        MeasuredOnAsync(Guid.Parse(user.Id), day, ct).BindAsync(measurement => RemoveAsync(measurement, ct));

    // ---- Guard ----

    private async Task<Result<BodyweightMeasurement, Problem>> MeasuredOnAsync(
        Guid userId, DateOnly day, CancellationToken ct)
    {
        var measurement = await _context.BodyweightMeasurements
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Day == day, ct);

        return measurement is null
            ? Result<BodyweightMeasurement, Problem>.Failure(new Problem.NotFound($"No bodyweight is logged for {day:yyyy-MM-dd}."))
            : Result<BodyweightMeasurement, Problem>.Success(measurement);
    }

    // ---- Stage and commit ----

    private async Task<BodyweightView> LoggedAsync(
        Guid userId, DateOnly day, Bodyweight bodyweight, CancellationToken ct)
    {
        var measurement = await _context.StageBodyweightAsync(userId, day, bodyweight, ct);
        await _context.StageRecentStateInvalidationAsync(userId, ct);
        await _context.SaveChangesAsync(ct);
        return BodyweightView.Of(measurement);
    }

    private async Task<Result<Unit, Problem>> RemoveAsync(BodyweightMeasurement measurement, CancellationToken ct)
    {
        _context.BodyweightMeasurements.Remove(measurement);
        await _context.StageRecentStateInvalidationAsync(measurement.UserId, ct);
        await _context.SaveChangesAsync(ct);

        return Result<Unit, Problem>.Success(Unit.Value);
    }

    // ---- Reads ----

    private async Task<IReadOnlyList<BodyweightView>> InRangeAsync(Guid userId, DayRange range, CancellationToken ct) =>
    [
        .. (await _context.BodyweightMeasurements
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.Day >= range.From && m.Day <= range.To)
                .OrderBy(m => m.Day)
                .ToListAsync(ct))
            .Select(BodyweightView.Of)
    ];
}
