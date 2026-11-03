using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Routines;

public interface IRoutineService
{
    Task<Result<RoutineView, Problem>> DefineAsync(IdentityUser user, DefineRoutineRequest request, CancellationToken ct);

    Task<Result<RoutineView, Problem>> FromSessionAsync(
        IdentityUser user, Guid sessionId, RoutineFromSessionRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<RoutineView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct);

    Task<Result<RoutineView, Problem>> GetAsync(IdentityUser user, Guid routineId, CancellationToken ct);

    Task<Result<RoutineView, Problem>> ReviseAsync(
        IdentityUser user, Guid routineId, DefineRoutineRequest request, CancellationToken ct);

    Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid routineId, CancellationToken ct);
}

/// <summary>
/// The athlete's routines. Same Guard → Stage → Commit shape as the session and program services,
/// and the same rule that someone else's routine, session or movement is not found rather than
/// forbidden.
/// </summary>
public sealed class RoutineService : IRoutineService
{
    private readonly ModelContext _context;
    private readonly TimeProvider _clock;

    public RoutineService(ModelContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<Result<RoutineView, Problem>> DefineAsync(
        IdentityUser user, DefineRoutineRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => Content(request)
                .BindAsync(content => MovementsOwnedAsync(userId, content, ct))
                .MapAsync(content => Routine.Define(NextId(), userId, content, Now())))
            .BindAsync(routine => AddAsync(routine, ct));

    public Task<Result<RoutineView, Problem>> FromSessionAsync(
        IdentityUser user, Guid sessionId, RoutineFromSessionRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => OwnedSessionAsync(userId, sessionId, ct)
                .BindAsync(Completed)
                .BindAsync(session => Trained(request.Name, session)
                    .BindAsync(content => MovementsOwnedAsync(userId, content, ct))
                    .MapAsync(content => Routine.Define(NextId(), userId, content, Now()))
                    .BindAsync(routine => AddFromSessionAsync(routine, session, ct))));

    public async Task<Result<IReadOnlyList<RoutineView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var routines = await _context.Routines
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.RoutineId)
            .ToListAsync(ct);
        var lastSessions = await LastSessionsAsync(userId, ct);

        return Result<IReadOnlyList<RoutineView>, Problem>.Success(
            [.. routines.Select(routine => RoutineView.Of(routine, lastSessions.GetValueOrDefault(routine.RoutineId)))]);
    }

    public Task<Result<RoutineView, Problem>> GetAsync(IdentityUser user, Guid routineId, CancellationToken ct) =>
        OwnedRoutineAsync(user, routineId, ct).MapAsync(routine => ViewAsync(routine, ct));

    public Task<Result<RoutineView, Problem>> ReviseAsync(
        IdentityUser user, Guid routineId, DefineRoutineRequest request, CancellationToken ct) =>
        Content(request)
            .BindAsync(content => OwnedRoutineAsync(user, routineId, ct)
                .BindAsync(routine => MovementsOwnedAsync(routine.UserId, content, ct)
                    .BindAsync(owned => RevisedAsync(routine, owned, ct))));

    public Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid routineId, CancellationToken ct) =>
        OwnedRoutineAsync(user, routineId, ct).BindAsync(routine => RemoveAsync(routine, ct));

    // ---- Guard ----

    private static Result<RoutineContent, Problem> Content(DefineRoutineRequest request) =>
        RoutineContent.Of(request.Name, request.Notes, request.Movements);

    /// <summary>
    /// What a completed session trained, as a routine's content: its movements in the order they
    /// first appear, each with the sets that were not warm-ups.
    /// </summary>
    private static Result<RoutineContent, Problem> Trained(string name, CompletedTrainingSession session) =>
        RoutineMovement.TrainedIn(session.Sets) is { Count: > 0 } movements
            ? RoutineContent.Of(name, string.Empty, movements)
            : Result<RoutineContent, Problem>.Failure(new Problem.Invalid(
                "SessionId", "This session has no working sets on a movement to make a routine from."));

    private static Result<CompletedTrainingSession, Problem> Completed(TrainingSession session) =>
        session is CompletedTrainingSession completed
            ? Result<CompletedTrainingSession, Problem>.Success(completed)
            : Result<CompletedTrainingSession, Problem>.Failure(new Problem.Conflict(
                "Only a completed session can be saved as a routine: what it trained is not settled until then."));

    /// <summary>
    /// Every movement the routine names is one of the athlete's, or the whole request is refused —
    /// with the same not-found answer whether a movement does not exist or is someone else's.
    /// </summary>
    private async Task<Result<RoutineContent, Problem>> MovementsOwnedAsync(
        Guid userId, RoutineContent content, CancellationToken ct)
    {
        var named = content.Movements.Select(movement => movement.MovementDataId).ToList();
        var owned = await _context.MovementDatas
            .CountAsync(movement => movement.UserID == userId && named.Contains(movement.MovementDataID), ct);

        return owned == named.Count
            ? Result<RoutineContent, Problem>.Success(content)
            : Result<RoutineContent, Problem>.Failure(new Problem.NotFound("Movement not found."));
    }

    private async Task<Result<Routine, Problem>> OwnedRoutineAsync(
        IdentityUser user, Guid routineId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var routine = await _context.Routines
            .FirstOrDefaultAsync(r => r.RoutineId == routineId && r.UserId == userId, ct);

        return routine is null
            ? Result<Routine, Problem>.Failure(new Problem.NotFound("Routine not found."))
            : Result<Routine, Problem>.Success(routine);
    }

    private async Task<Result<TrainingSession, Problem>> OwnedSessionAsync(
        Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.TrainingSessionId == sessionId && s.UserId == userId, ct);

        return session is null
            ? Result<TrainingSession, Problem>.Failure(new Problem.NotFound("Training session not found."))
            : Result<TrainingSession, Problem>.Success(session);
    }

    /// <summary>Version 7 for the index locality the session service gives its own ids.</summary>
    private Guid NextId() => Guid.CreateVersion7(_clock.GetUtcNow());

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    // ---- Commit ----

    /// <summary>A routine just defined has never been trained, so it has no last session.</summary>
    private async Task<Result<RoutineView, Problem>> AddAsync(Routine routine, CancellationToken ct)
    {
        _context.Routines.Add(routine);
        await _context.SaveChangesAsync(ct);

        return Result<RoutineView, Problem>.Success(RoutineView.Of(routine, null));
    }

    /// <summary>
    /// Saving an ad hoc session as a routine is the athlete saying that session was this routine,
    /// so the session takes it as its origin in the same save, and the routine has a last session
    /// from the start. A session already trained from another routine keeps that origin: it was
    /// that routine, whatever this one was copied from.
    /// </summary>
    private async Task<Result<RoutineView, Problem>> AddFromSessionAsync(
        Routine routine, CompletedTrainingSession session, CancellationToken ct)
    {
        _context.Routines.Add(routine);

        var last = session.Origin is RoutineOrigin.AdHoc
            ? StageOrigin(session, new RoutineOrigin.FromRoutine(routine.RoutineId))
            : null;

        await _context.SaveChangesAsync(ct);

        return Result<RoutineView, Problem>.Success(RoutineView.Of(routine, last));
    }

    /// <summary>Remove-then-add for the reason given on <see cref="Sessions.TrainingSessionService"/>.</summary>
    private CompletedTrainingSession StageOrigin(CompletedTrainingSession session, RoutineOrigin origin)
    {
        var next = session with { Origin = origin };
        _context.Sessions.Remove(session);
        _context.Sessions.Add(next);
        return next;
    }

    private async Task<Result<RoutineView, Problem>> RevisedAsync(
        Routine routine, RoutineContent content, CancellationToken ct)
    {
        routine.Revise(content);
        await _context.SaveChangesAsync(ct);

        return Result<RoutineView, Problem>.Success(await ViewAsync(routine, ct));
    }

    /// <summary>Sessions trained from the routine keep its id: they were trained from it.</summary>
    private async Task<Result<Unit, Problem>> RemoveAsync(Routine routine, CancellationToken ct)
    {
        _context.Routines.Remove(routine);
        await _context.SaveChangesAsync(ct);

        return Result<Unit, Problem>.Success(Unit.Value);
    }

    // ---- Reads ----

    private async Task<RoutineView> ViewAsync(Routine routine, CancellationToken ct)
    {
        RoutineOrigin origin = new RoutineOrigin.FromRoutine(routine.RoutineId);
        var last = await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(s => s.UserId == routine.UserId && s.Origin == origin)
            .OrderByDescending(s => EF.Property<DateTime>(s, "StartTime"))
            .FirstOrDefaultAsync(ct);

        return RoutineView.Of(routine, last);
    }

    /// <summary>
    /// Each routine's most recent completed session, by the stamped StartTime.
    /// </summary>
    /// <remarks>
    /// Picked from a projection of the ids and times first, then only the winners are read whole,
    /// so listing routines does not deserialize every set the athlete has trained from one.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Guid, CompletedTrainingSession>> LastSessionsAsync(
        Guid userId, CancellationToken ct)
    {
        RoutineOrigin adHoc = new RoutineOrigin.AdHoc();
        var trained = await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Origin != adHoc)
            .Select(s => new { s.TrainingSessionId, s.Origin, StartTime = EF.Property<DateTime>(s, "StartTime") })
            .ToListAsync(ct);

        var latest = trained
            .GroupBy(s => s.Origin)
            .Select(sessions => sessions.MaxBy(s => s.StartTime)!.TrainingSessionId)
            .ToList();

        return await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(s => latest.Contains(s.TrainingSessionId))
            .ToDictionaryAsync(s => RoutineIdOf(s.Origin), ct);
    }

    /// <summary>The routine of a session read as trained from one; the query above admits no other.</summary>
    private static Guid RoutineIdOf(RoutineOrigin origin) => origin switch
    {
        RoutineOrigin.FromRoutine fromRoutine => fromRoutine.RoutineId,
        _ => throw new UnreachableException(),
    };
}
