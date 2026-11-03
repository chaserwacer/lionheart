using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Focus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Focus;

public interface IFocusPinService
{
    /// <summary>Every pin the athlete has made, the ones still pinned first.</summary>
    Task<Result<IReadOnlyList<FocusPinView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct);

    Task<Result<FocusPinView, Problem>> PinAsync(IdentityUser user, PinFocusRequest request, CancellationToken ct);

    Task<Result<FocusPinView, Problem>> ReviseAsync(
        IdentityUser user, Guid focusPinId, ReviseFocusRequest request, CancellationToken ct);

    Task<Result<FocusPinView, Problem>> UnpinAsync(
        IdentityUser user, Guid focusPinId, UnpinFocusRequest request, CancellationToken ct);

    /// <summary>Removes a pin made by mistake. Letting a focus go is <see cref="UnpinAsync"/>.</summary>
    Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid focusPinId, CancellationToken ct);
}

/// <summary>
/// The athlete's focus list. Guard → Stage → Commit, and another athlete's pin or movement is a
/// 404, the same rules <see cref="Programs.TrainingProgramService"/> follows.
/// </summary>
public sealed class FocusPinService : IFocusPinService
{
    private readonly ModelContext _context;
    private readonly TimeProvider _clock;

    public FocusPinService(ModelContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <remarks>
    /// Read on the identity's own id rather than through <c>AthleteIdAsync</c>, so an account with
    /// no profile yet reads an empty list rather than a conflict — the rule every read here follows.
    /// </remarks>
    public async Task<Result<IReadOnlyList<FocusPinView>, Problem>> GetAllAsync(
        IdentityUser user, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var pins = await _context.FocusPins
            .AsNoTracking()
            .Where(pin => pin.UserId == userId)
            .ToListAsync(ct);

        return Result<IReadOnlyList<FocusPinView>, Problem>.Success([.. Listed(pins).Select(FocusPinView.Of)]);
    }

    public Task<Result<FocusPinView, Problem>> PinAsync(
        IdentityUser user, PinFocusRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => request.ToTerms()
                .BindAsync(terms => OwnMovementAsync(userId, request.MovementDataId, ct)
                    .MapAsync(movementDataId => NewPin(userId, movementDataId, terms))))
            .BindAsync(pin => AddAsync(pin, ct));

    public Task<Result<FocusPinView, Problem>> ReviseAsync(
        IdentityUser user, Guid focusPinId, ReviseFocusRequest request, CancellationToken ct) =>
        OwnedAsync(user, focusPinId, ct)
            .BindAsync(pin => request.ToTerms().Bind(pin.Revise))
            .BindAsync(pin => CommitAsync(pin, ct));

    public Task<Result<FocusPinView, Problem>> UnpinAsync(
        IdentityUser user, Guid focusPinId, UnpinFocusRequest request, CancellationToken ct) =>
        OwnedAsync(user, focusPinId, ct)
            .BindAsync(pin => pin.Unpin(request.On))
            .BindAsync(pin => CommitAsync(pin, ct));

    public Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid focusPinId, CancellationToken ct) =>
        OwnedAsync(user, focusPinId, ct).BindAsync(pin => RemoveAsync(pin, ct));

    // ---- Guard ----

    /// <summary>
    /// The pin, or a refusal. Ownership is part of the lookup, so another athlete's pin is
    /// indistinguishable from one that does not exist.
    /// </summary>
    private async Task<Result<FocusPin, Problem>> OwnedAsync(IdentityUser user, Guid focusPinId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var pin = await _context.FocusPins
            .FirstOrDefaultAsync(p => p.FocusPinId == focusPinId && p.UserId == userId, ct);

        return pin is null
            ? Result<FocusPin, Problem>.Failure(new Problem.NotFound("Focus pin not found."))
            : Result<FocusPin, Problem>.Success(pin);
    }

    /// <summary>The movement, if it is this athlete's — the foreign key alone would accept anyone's.</summary>
    private async Task<Result<Guid, Problem>> OwnMovementAsync(Guid userId, Guid movementDataId, CancellationToken ct)
    {
        var exists = await _context.MovementDatas
            .AnyAsync(movement => movement.MovementDataID == movementDataId && movement.UserID == userId, ct);

        return exists
            ? Result<Guid, Problem>.Success(movementDataId)
            : Result<Guid, Problem>.Failure(new Problem.NotFound("Movement not found."));
    }

    /// <summary>
    /// Pinned first, then the latest horizon first.
    /// </summary>
    /// <remarks>
    /// Ordered here rather than in SQL because the status is a JSON column. The pinning instant and
    /// then the id break ties, so two pins over one horizon never come back in two orders.
    /// </remarks>
    private static IEnumerable<FocusPin> Listed(IEnumerable<FocusPin> pins) =>
        pins
            .OrderByDescending(pin => pin.IsPinned)
            .ThenByDescending(pin => pin.Terms.Horizon.Starts)
            .ThenByDescending(pin => pin.PinnedAt)
            .ThenByDescending(pin => pin.FocusPinId);

    // ---- Stage ----

    /// <summary>The id and the pinning instant come from one read of the clock, so they agree.</summary>
    private FocusPin NewPin(Guid userId, Guid movementDataId, FocusTerms terms)
    {
        var now = _clock.GetUtcNow();
        return FocusPin.Pin(Guid.CreateVersion7(now), userId, movementDataId, terms, now.UtcDateTime);
    }

    // ---- Commit ----

    private Task<Result<FocusPinView, Problem>> AddAsync(FocusPin pin, CancellationToken ct)
    {
        _context.FocusPins.Add(pin);
        return CommitAsync(pin, ct);
    }

    private async Task<Result<FocusPinView, Problem>> CommitAsync(FocusPin pin, CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
        return Result<FocusPinView, Problem>.Success(FocusPinView.Of(pin));
    }

    private async Task<Result<Unit, Problem>> RemoveAsync(FocusPin pin, CancellationToken ct)
    {
        _context.FocusPins.Remove(pin);
        await _context.SaveChangesAsync(ct);
        return Result<Unit, Problem>.Success(Unit.Value);
    }
}
