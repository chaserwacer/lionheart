using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Contexts;

public interface IContextService
{
    Task<Result<ContextView, Problem>> CreateExertionAsync(IdentityUser user, CreateExertionContextRequest request, CancellationToken ct);

    Task<Result<ContextView, Problem>> CreateStateAsync(IdentityUser user, CreateStateContextRequest request, CancellationToken ct);

    Task<Result<ContextView, Problem>> UpdateNotesAsync(IdentityUser user, Guid contextId, ContextNotesRequest request, CancellationToken ct);

    Task<Result<ContextView, Problem>> GetAsync(IdentityUser user, Guid contextId, CancellationToken ct);

    Task<Result<IReadOnlyList<ContextView>, Problem>> GetWindowAsync(IdentityUser user, TimeWindow window, CancellationToken ct);

    Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid contextId, CancellationToken ct);
}

/// <summary>
/// Contexts — everything non-session with performance relevance. What kind of context it is lives
/// in the decoration's facets, so the only structural distinction here is the one the domain makes:
/// exertion carries effort, a state does not.
/// </summary>
/// <remarks>
/// Contexts are decorated at creation (docs/data-remodel.md), but the interpretation engine
/// decorates *text*: a context entered as structure has no utterance to parse, and nothing else can
/// annotate one. Both creates below therefore write NotDecorated, and such a context stays out of
/// AnalyzableEntry until the re-decoration path exists.
/// </remarks>
public sealed class ContextService : IContextService
{
    private readonly ModelContext _context;
    private readonly TimeProvider _clock;

    public ContextService(ModelContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<Result<ContextView, Problem>> CreateExertionAsync(
        IdentityUser user, CreateExertionContextRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => request.Period.ToPeriod()
                .Map(period => new ExertionContext(
                    PerceivedEffortCommand.ToEffortState(request.Effort, _clock.GetUtcNow().UtcDateTime))
                {
                    ContextId = NextId(),
                    UserId = userId,
                    Period = period,
                    Notes = request.Notes,
                    Decoration = new NotDecorated(),
                }))
            .BindAsync(context => AddAsync(context, ct));

    public Task<Result<ContextView, Problem>> CreateStateAsync(
        IdentityUser user, CreateStateContextRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => request.Period.ToPeriod()
                .Map(period => new StateContext
                {
                    ContextId = NextId(),
                    UserId = userId,
                    Period = period,
                    Notes = request.Notes,
                    Decoration = new NotDecorated(),
                }))
            .BindAsync(context => AddAsync(context, ct));

    public Task<Result<ContextView, Problem>> UpdateNotesAsync(
        IdentityUser user, Guid contextId, ContextNotesRequest request, CancellationToken ct) =>
        OwnedAsync(user, contextId, ct)
            .BindAsync(context => ReplaceAsync(context, context with { Notes = request.Notes }, ct));

    public Task<Result<ContextView, Problem>> GetAsync(
        IdentityUser user, Guid contextId, CancellationToken ct) =>
        OwnedAsync(user, contextId, ct).MapAsync(ContextView.Of);

    public Task<Result<IReadOnlyList<ContextView>, Problem>> GetWindowAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct) =>
        window.Ordered()
            .MapAsync(ordered => InWindowAsync(Guid.Parse(user.Id), ordered, ct))
            .MapAsync(Views);

    public Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid contextId, CancellationToken ct) =>
        OwnedAsync(user, contextId, ct).BindAsync(context => RemoveAsync(context, ct));

    // ---- Guard ----

    /// <summary>
    /// The context, or a refusal. Ownership is in the lookup: another athlete's context reads as
    /// missing, so 404 and 403 never distinguish a real id from an imagined one.
    /// </summary>
    private async Task<Result<Context, Problem>> OwnedAsync(
        IdentityUser user, Guid contextId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var context = await _context.Contexts
            .FirstOrDefaultAsync(c => c.ContextId == contextId && c.UserId == userId, ct);

        return context is null
            ? Result<Context, Problem>.Failure(new Problem.NotFound("Context not found."))
            : Result<Context, Problem>.Success(context);
    }

    private Guid NextId() => Guid.CreateVersion7(_clock.GetUtcNow());

    // ---- Commit ----

    private async Task<Result<ContextView, Problem>> AddAsync(Context context, CancellationToken ct)
    {
        _context.Contexts.Add(context);
        await _context.SaveChangesAsync(ct);

        return Result<ContextView, Problem>.Success(ContextView.Of(context));
    }

    /// <summary>Remove-then-add for the reason given on <see cref="Training.Sessions.TrainingSessionService"/>.</summary>
    private async Task<Result<ContextView, Problem>> ReplaceAsync(
        Context previous, Context next, CancellationToken ct)
    {
        _context.Contexts.Remove(previous);
        _context.Contexts.Add(next);
        await _context.SaveChangesAsync(ct);

        return Result<ContextView, Problem>.Success(ContextView.Of(next));
    }

    private async Task<Result<Unit, Problem>> RemoveAsync(Context context, CancellationToken ct)
    {
        _context.Contexts.Remove(context);
        await _context.StageProjectionRemovalAsync(context.ContextId, ct);
        await _context.SaveChangesAsync(ct);

        return Result<Unit, Problem>.Success(Unit.Value);
    }

    // ---- Reads ----

    private static IReadOnlyList<ContextView> Views(IEnumerable<Context> contexts) =>
        [.. contexts.Select(ContextView.Of)];

    /// <summary>
    /// Every context carries a <see cref="Period"/>, so the stamped StartTime column answers the
    /// whole window in one indexed query — unlike sessions, whose planned states have no clock
    /// time to stamp.
    /// </summary>
    private async Task<IReadOnlyList<Context>> InWindowAsync(
        Guid userId, TimeWindow window, CancellationToken ct)
    {
        var (fromUtc, toUtc) = (window.From.UtcDateTime, window.To.UtcDateTime);

        return await _context.Contexts
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Where(c => EF.Property<DateTime>(c, "StartTime") >= fromUtc
                        && EF.Property<DateTime>(c, "StartTime") <= toUtc)
            .OrderBy(c => EF.Property<DateTime>(c, "StartTime"))
            .ToListAsync(ct);
    }
}
