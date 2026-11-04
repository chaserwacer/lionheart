using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Health.Import;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Sessions;

public interface ITrainingSessionService
{
    Task<Result<TrainingSessionView, Problem>> PlanAsync(IdentityUser user, PlanSessionRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> StartUnplannedAsync(IdentityUser user, StartUnplannedSessionRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> LogCompletedAsync(IdentityUser user, LogCompletedSessionRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> StartAsync(IdentityUser user, Guid sessionId, StartSessionRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> CompleteAsync(IdentityUser user, Guid sessionId, CompleteSessionRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> SkipAsync(IdentityUser user, Guid sessionId, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> ReplaceSetsAsync(IdentityUser user, Guid sessionId, ReplaceSetsRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> ReplacePrescriptionsAsync(IdentityUser user, Guid sessionId, ReplacePrescriptionsRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> ReviseAsync(IdentityUser user, Guid sessionId, ReviseSessionRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> UpdateNotesAsync(IdentityUser user, Guid sessionId, SessionNotesRequest request, CancellationToken ct);

    Task<Result<TrainingSessionView, Problem>> GetAsync(IdentityUser user, Guid sessionId, CancellationToken ct);

    Task<Result<IReadOnlyList<TrainingSessionView>, Problem>> GetWindowAsync(IdentityUser user, TimeWindow window, CancellationToken ct);

    Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid sessionId, CancellationToken ct);
}

/// <summary>
/// The structured write path for training sessions — the other first-class one alongside the
/// interpretation engine. Every command runs Guard → Stage → Commit: refuse before touching
/// anything, build the next immutable record, then save once.
/// </summary>
public sealed class TrainingSessionService : ITrainingSessionService
{
    private readonly ModelContext _context;
    private readonly TimeProvider _clock;
    private readonly IPersonalRecordDetector _records;
    private readonly PersonalRecordRebuild _rebuild;

    public TrainingSessionService(
        ModelContext context, TimeProvider clock, IPersonalRecordDetector records, PersonalRecordRebuild rebuild)
    {
        _context = context;
        _clock = clock;
        _records = records;
        _rebuild = rebuild;
    }

    public Task<Result<TrainingSessionView, Problem>> PlanAsync(
        IdentityUser user, PlanSessionRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => MembershipAsync(userId, request.TrainingProgramId, ct)
                .BindAsync(membership => request.Prescriptions
                    .Traverse(prescription => prescription.ToPrescription())
                    .Map(prescriptions => NewPlanned(userId, request, membership, prescriptions)))
                .BindAsync(session => AddAsync(session, ct)));

    public Task<Result<TrainingSessionView, Problem>> StartUnplannedAsync(
        IdentityUser user, StartUnplannedSessionRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => PlacementAsync(userId, request.TrainingProgramId, request.RoutineId, ct)
                .MapAsync(placement => NewActive(userId, request, placement))
                .BindAsync(session => AddAsync(session, ct)));

    public Task<Result<TrainingSessionView, Problem>> LogCompletedAsync(
        IdentityUser user, LogCompletedSessionRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => PlacementAsync(userId, request.TrainingProgramId, request.RoutineId, ct)
                .BindAsync(placement => request.Sets.Traverse(set => set.ToEntry())
                    .Bind(sets => request.Period.ToPeriod()
                        .Map(period => NewCompleted(userId, request, placement, sets, period))))
                .BindAsync(session => AddCompletedAsync(session, ct)));

    public Task<Result<TrainingSessionView, Problem>> StartAsync(
        IdentityUser user, Guid sessionId, StartSessionRequest request, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct)
            .BindAsync(session => Started(session, request.StartedAt)
                .BindAsync(started => ReplaceAsync(session, started, ct)));

    public Task<Result<TrainingSessionView, Problem>> CompleteAsync(
        IdentityUser user, Guid sessionId, CompleteSessionRequest request, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct)
            .BindAsync(session => request.Period.ToPeriod()
                .Bind(period => Completed(session, period, Effort(request.Effort)))
                .BindAsync(completed => ReplaceWithCompletedAsync(session, completed, ct)));

    public Task<Result<TrainingSessionView, Problem>> SkipAsync(
        IdentityUser user, Guid sessionId, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct)
            .BindAsync(session => Skipped(session)
                .BindAsync(skipped => ReplaceAsync(session, skipped, ct)));

    public Task<Result<TrainingSessionView, Problem>> ReplaceSetsAsync(
        IdentityUser user, Guid sessionId, ReplaceSetsRequest request, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct)
            .BindAsync(session => request.Sets.Traverse(set => set.ToEntry())
                .Bind(sets => WithSets(session, sets))
                .BindAsync(updated => ReplaceAsync(session, updated, ct)));

    public Task<Result<TrainingSessionView, Problem>> ReplacePrescriptionsAsync(
        IdentityUser user, Guid sessionId, ReplacePrescriptionsRequest request, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct)
            .BindAsync(session => request.Prescriptions
                .Traverse(prescription => prescription.ToPrescription())
                .Bind(prescriptions => WithPrescriptions(session, prescriptions))
                .BindAsync(updated => ReplaceAsync(session, updated, ct)));

    public Task<Result<TrainingSessionView, Problem>> ReviseAsync(
        IdentityUser user, Guid sessionId, ReviseSessionRequest request, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct)
            .BindAsync(session => Revisable(session)
                .Bind(completed => request.Sets.Traverse(set => set.ToEntry())
                    .Bind(sets => request.Period.ToPeriod()
                        .Map(period => SessionRevision.Of(completed, sets, period, request.Notes))))
                .BindAsync(revision => CommitRevisionAsync(session, revision, ct)));

    public Task<Result<TrainingSessionView, Problem>> UpdateNotesAsync(
        IdentityUser user, Guid sessionId, SessionNotesRequest request, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct)
            // Notes are commentary, so every lifecycle state accepts them; `with` on the abstract
            // base keeps whichever concrete state the session is already in.
            .BindAsync(session => ReplaceAsync(session, session with { Notes = request.Notes }, ct));

    public Task<Result<TrainingSessionView, Problem>> GetAsync(
        IdentityUser user, Guid sessionId, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct).MapAsync(TrainingSessionView.Of);

    public Task<Result<IReadOnlyList<TrainingSessionView>, Problem>> GetWindowAsync(
        IdentityUser user, TimeWindow window, CancellationToken ct) =>
        window.Ordered()
            .MapAsync(ordered => InWindowAsync(Guid.Parse(user.Id), ordered, ct))
            .MapAsync(Views);

    public Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid sessionId, CancellationToken ct) =>
        OwnedAsync(user, sessionId, ct).BindAsync(session => RemoveAsync(session, ct));

    // ---- Guard ----

    /// <summary>
    /// The session, or a refusal. Ownership is part of the lookup rather than a check after it:
    /// another athlete's session has to be indistinguishable from one that does not exist, or the
    /// difference between 404 and 403 tells a stranger which ids are real.
    /// </summary>
    private async Task<Result<TrainingSession, Problem>> OwnedAsync(
        IdentityUser user, Guid sessionId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.TrainingSessionId == sessionId && s.UserId == userId, ct);

        return session is null
            ? Result<TrainingSession, Problem>.Failure(new Problem.NotFound("Training session not found."))
            : Result<TrainingSession, Problem>.Success(session);
    }

    /// <summary>
    /// Turns the wire's optional program id back into the domain's fill-state, refusing a program
    /// that is not this athlete's — the same 404-not-403 rule as sessions themselves.
    /// </summary>
    private async Task<Result<ProgramMembership, Problem>> MembershipAsync(
        Guid userId, Guid? trainingProgramId, CancellationToken ct)
    {
        if (trainingProgramId is not { } programId)
        {
            return Result<ProgramMembership, Problem>.Success(new Standalone());
        }

        var exists = await _context.Programs
            .AnyAsync(p => p.TrainingProgramId == programId && p.UserId == userId, ct);

        return exists
            ? Result<ProgramMembership, Problem>.Success(new InProgram(programId))
            : Result<ProgramMembership, Problem>.Failure(new Problem.NotFound("Training program not found."));
    }

    /// <summary>Where a session the athlete starts or logs directly sits: its program, and its routine.</summary>
    private Task<Result<Placement, Problem>> PlacementAsync(
        Guid userId, Guid? trainingProgramId, Guid? routineId, CancellationToken ct) =>
        MembershipAsync(userId, trainingProgramId, ct)
            .MapAsync(async membership => new Placement(membership, await OriginAsync(userId, routineId, ct)));

    /// <summary>
    /// The routine a session was trained from, or ad hoc when the id is not one of the athlete's
    /// routines. Dropped rather than refused, unlike a program: a workout started offline from a
    /// routine deleted in the meantime still happened, and its sets are real whatever became of
    /// the routine. Someone else's routine reads as no routine, so nothing about it is disclosed.
    /// </summary>
    private async Task<RoutineOrigin> OriginAsync(Guid userId, Guid? routineId, CancellationToken ct) =>
        routineId is { } id && await _context.Routines.AnyAsync(r => r.RoutineId == id && r.UserId == userId, ct)
            ? new RoutineOrigin.FromRoutine(id)
            : new RoutineOrigin.AdHoc();

    private sealed record Placement(ProgramMembership Membership, RoutineOrigin Origin);

    // ---- Stage: creation ----
    //
    // The server states the four members a client may not: the id, the owner, the authorship, and
    // when the row was written. A client able to state Provenance could mark its own entry
    // AiAssisted — or hide that the engine wrote one — and make authorship unauditable.

    private PlannedTrainingSession NewPlanned(
        Guid userId,
        PlanSessionRequest request,
        ProgramMembership membership,
        IReadOnlyList<SetPrescription> prescriptions) =>
        new(request.PlannedFor, prescriptions)
        {
            TrainingSessionId = NextId(),
            UserId = userId,
            Notes = request.Notes,
            Provenance = Provenance.User,
            Membership = membership,
            // Planning names no routine; a routine's sessions are started or logged from it.
            Origin = new RoutineOrigin.AdHoc(),
            Source = new SessionSource.AthleteLog(),
            CreatedAt = Now(),
        };

    private ActiveTrainingSession NewActive(
        Guid userId, StartUnplannedSessionRequest request, Placement placement) =>
        new(request.StartedAt, [])
        {
            TrainingSessionId = NextId(),
            UserId = userId,
            Notes = request.Notes,
            Provenance = Provenance.User,
            Membership = placement.Membership,
            Origin = placement.Origin,
            Source = new SessionSource.AthleteLog(),
            CreatedAt = Now(),
        };

    private CompletedTrainingSession NewCompleted(
        Guid userId,
        LogCompletedSessionRequest request,
        Placement placement,
        IReadOnlyList<SetEntry> sets,
        Period period) =>
        new(sets, period, Effort(request.Effort), new NotDecorated())
        {
            TrainingSessionId = NextId(),
            UserId = userId,
            Notes = request.Notes,
            Provenance = Provenance.User,
            Membership = placement.Membership,
            Origin = placement.Origin,
            Source = new SessionSource.AthleteLog(),
            CreatedAt = Now(),
        };

    // ---- Stage: lifecycle transitions ----
    //
    // Each one answers for every source state, so a transition asked for from the wrong one is a
    // refusal the caller can read rather than an exception or a silent no-op. Completed and
    // Skipped are terminal, so they refuse everything.

    private static Result<TrainingSession, Problem> Started(TrainingSession session, DateTimeOffset startedAt) =>
        session is PlannedTrainingSession planned
            ? Result<TrainingSession, Problem>.Success(new ActiveTrainingSession(startedAt, [])
            {
                TrainingSessionId = planned.TrainingSessionId,
                UserId = planned.UserId,
                Notes = planned.Notes,
                Provenance = planned.Provenance,
                Membership = planned.Membership,
                Origin = planned.Origin,
                Source = planned.Source,
                CreatedAt = planned.CreatedAt,
            })
            : Refusal(session, "started", "planned");

    private static Result<CompletedTrainingSession, Problem> Completed(
        TrainingSession session, Period period, EffortState effort) =>
        session is ActiveTrainingSession active
            // Sessions are decorated on completion (docs/data-remodel.md), but the interpretation
            // engine decorates text and only ever creates — nothing can annotate an
            // already-structured session yet. Completing therefore lands NotDecorated, and the
            // session stays out of AnalyzableEntry until the re-decoration path exists.
            ? Result<CompletedTrainingSession, Problem>.Success(
                new CompletedTrainingSession(active.Sets, period, effort, new NotDecorated())
                {
                    TrainingSessionId = active.TrainingSessionId,
                    UserId = active.UserId,
                    Notes = active.Notes,
                    Provenance = active.Provenance,
                    Membership = active.Membership,
                    Origin = active.Origin,
                    Source = active.Source,
                    CreatedAt = active.CreatedAt,
                })
            : Refusal<CompletedTrainingSession>(session, "completed", "active");

    private static Result<TrainingSession, Problem> Skipped(TrainingSession session) =>
        session is PlannedTrainingSession planned
            ? Result<TrainingSession, Problem>.Success(new SkippedTrainingSession(planned.PlannedFor)
            {
                TrainingSessionId = planned.TrainingSessionId,
                UserId = planned.UserId,
                Notes = planned.Notes,
                Provenance = planned.Provenance,
                Membership = planned.Membership,
                Origin = planned.Origin,
                Source = planned.Source,
                CreatedAt = planned.CreatedAt,
            })
            : Refusal(session, "skipped", "planned");

    private static Result<TrainingSession, Problem> WithSets(TrainingSession session, IReadOnlyList<SetEntry> sets) =>
        session is ActiveTrainingSession active
            ? Result<TrainingSession, Problem>.Success(active with { Sets = sets })
            : Refusal(session, "given sets", "active");

    private static Result<CompletedTrainingSession, Problem> Revisable(TrainingSession session) =>
        session is CompletedTrainingSession completed
            ? Result<CompletedTrainingSession, Problem>.Success(completed)
            : Refusal<CompletedTrainingSession>(session, "revised", "completed");

    private static Result<TrainingSession, Problem> WithPrescriptions(
        TrainingSession session, IReadOnlyList<SetPrescription> prescriptions) =>
        session is PlannedTrainingSession planned
            ? Result<TrainingSession, Problem>.Success(planned with { Prescriptions = prescriptions })
            : Refusal(session, "given prescriptions", "planned");

    private static Result<TrainingSession, Problem> Refusal(
        TrainingSession session, string action, string from) =>
        Refusal<TrainingSession>(session, action, from);

    private static Result<T, Problem> Refusal<T>(TrainingSession session, string action, string from) =>
        Result<T, Problem>.Failure(new Problem.Conflict(
            $"A {Lifecycle(session)} session cannot be {action}; only a {from} session can."));

    private static string Lifecycle(TrainingSession session) => session switch
    {
        PlannedTrainingSession => "planned",
        ActiveTrainingSession => "active",
        CompletedTrainingSession => "completed",
        SkippedTrainingSession => "skipped",
        _ => throw new UnreachableException()
    };

    /// <summary>
    /// Version 7 so successive sessions sort together and inserts land in one region of the index
    /// instead of scattering across it.
    /// </summary>
    private Guid NextId() => Guid.CreateVersion7(_clock.GetUtcNow());

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private EffortState Effort(PerceivedEffortCommand? command) =>
        PerceivedEffortCommand.ToEffortState(command, Now());

    // ---- Commit: one save, at the end ----

    private async Task<Result<TrainingSessionView, Problem>> AddAsync(TrainingSession session, CancellationToken ct)
    {
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync(ct);

        return Result<TrainingSessionView, Problem>.Success(TrainingSessionView.Of(session));
    }

    /// <summary>
    /// Writes the next version of a session over the previous one.
    /// </summary>
    /// <remarks>
    /// Remove-then-add rather than mutate, for two reasons that coincide: the domain records are
    /// immutable, and a lifecycle transition changes the row's TPH discriminator, which EF cannot
    /// do to a tracked entity in place. EF orders the DELETE ahead of the INSERT inside the one
    /// SaveChanges, so the shared key never collides.
    /// </remarks>
    private async Task<Result<TrainingSessionView, Problem>> ReplaceAsync(
        TrainingSession previous, TrainingSession next, CancellationToken ct)
    {
        _context.Sessions.Remove(previous);
        _context.Sessions.Add(next);
        await _context.SaveChangesAsync(ct);

        return Result<TrainingSessionView, Problem>.Success(TrainingSessionView.Of(next));
    }

    /// <summary>
    /// Completing a session also establishes whatever records its sets just set, in the same
    /// save: a record is a fact about a session that happened, so a save that fails must leave
    /// neither the session nor the records it would have produced.
    /// </summary>
    private async Task<Result<TrainingSessionView, Problem>> AddCompletedAsync(
        CompletedTrainingSession completed, CancellationToken ct)
    {
        await _records.StageAsync([completed], ct);
        return await AddAsync(completed, ct);
    }

    /// <inheritdoc cref="AddCompletedAsync"/>
    private async Task<Result<TrainingSessionView, Problem>> ReplaceWithCompletedAsync(
        TrainingSession previous, CompletedTrainingSession completed, CancellationToken ct)
    {
        await _records.StageAsync([completed], ct);
        return await ReplaceAsync(previous, completed, ct);
    }

    /// <summary>
    /// Saves a correction, re-detecting the athlete's records over his whole history in the same
    /// transaction when it reaches them.
    /// </summary>
    /// <remarks>
    /// A rebuild rather than detection on this one session, because a record is judged against what
    /// stood before it: lowering last month's top set can make a later set the record, and moving a
    /// session's day can reorder a chain. The rebuild refuses while a record is withdrawn, since a
    /// replay would award it again, and the correction is refused with it, writing nothing — a
    /// session whose sets no longer agree with its records is the state this prevents.
    /// </remarks>
    private Task<Result<TrainingSessionView, Problem>> CommitRevisionAsync(
        TrainingSession previous, SessionRevision revision, CancellationToken ct) =>
        revision.AffectsRecords
            ? _context.InTransactionAsync(
                () => _rebuild.ReplayableAsync(previous.UserId, ct)
                    .BindAsync(_ => ReplaceReachingRecordsAsync(previous, revision.Revised, ct))
                    .BindAsync(view => _rebuild.ReplayAsync(previous.UserId, ct).MapAsync(_ => view))
                    .MapErrorAsync(RecordsRefusal),
                ct)
            : ReplaceAsync(previous, revision.Revised, ct);

    /// <summary>
    /// Writes a correction that reached the records, taking the analysis projection with the sets
    /// it summarised.
    /// </summary>
    private async Task<Result<TrainingSessionView, Problem>> ReplaceReachingRecordsAsync(
        TrainingSession previous, CompletedTrainingSession revised, CancellationToken ct)
    {
        await _context.StageProjectionRemovalAsync(previous.TrainingSessionId, ct);
        return await ReplaceAsync(previous, revised, ct);
    }

    /// <summary>
    /// The rebuild's refusal, said to the athlete: its own text names records by id, for the
    /// operator running the command.
    /// </summary>
    private static Problem RecordsRefusal(Problem problem) => problem switch
    {
        Problem.Conflict => new Problem.Conflict(
            "Changing a session's sets or start re-detects every personal record from the whole history, "
            + "which would award a record you withdrew all over again, so it is refused while any record is "
            + "withdrawn. The session was not changed; its notes and finish can still be."),
        _ => problem,
    };

    private async Task<Result<Unit, Problem>> RemoveAsync(TrainingSession session, CancellationToken ct)
    {
        _context.Sessions.Remove(session);
        await _context.StageProjectionRemovalAsync(session.TrainingSessionId, ct);
        await _context.StageRecordingRemovalAsync(session.TrainingSessionId, ct);
        await _context.SaveChangesAsync(ct);

        return Result<Unit, Problem>.Success(Unit.Value);
    }

    // ---- Reads ----

    private static IReadOnlyList<TrainingSessionView> Views(IEnumerable<TrainingSession> sessions) =>
        [.. sessions.Select(TrainingSessionView.Of)];

    /// <summary>
    /// Sessions whose own time-truth falls inside the window — each lifecycle state answering with
    /// the column it actually has: the stamped StartTime for a completed session, StartedAt for an
    /// active one, and the planned date for planned and skipped ones, which carry no clock time at
    /// all. One query per state because the predicates differ; at personal-log volume, over
    /// indexed columns, four of those beat reading the table and filtering in app.
    /// </summary>
    private async Task<IReadOnlyList<TrainingSession>> InWindowAsync(
        Guid userId, TimeWindow window, CancellationToken ct)
    {
        var (fromUtc, toUtc) = (window.From.UtcDateTime, window.To.UtcDateTime);
        var (from, to) = (window.From, window.To);
        var (fromDate, toDate) = (DateOnly.FromDateTime(from.DateTime), DateOnly.FromDateTime(to.DateTime));

        var completed = await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Where(s => EF.Property<DateTime>(s, "StartTime") >= fromUtc
                        && EF.Property<DateTime>(s, "StartTime") <= toUtc)
            .ToListAsync(ct);

        // SQLite cannot order DateTimeOffset: EF stores it as TEXT with the offset appended, where
        // a byte-wise comparison is wrong across offsets, so the provider refuses to translate the
        // comparison rather than answering wrongly. An active session is one the athlete is in the
        // middle of, so the whole set is a handful of rows and matching them in app costs nothing.
        var active = (await _context.Sessions.OfType<ActiveTrainingSession>()
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .ToListAsync(ct))
            .Where(s => s.StartedAt >= from && s.StartedAt <= to);

        var planned = await _context.Sessions.OfType<PlannedTrainingSession>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.PlannedFor >= fromDate && s.PlannedFor <= toDate)
            .ToListAsync(ct);

        var skipped = await _context.Sessions.OfType<SkippedTrainingSession>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.PlannedFor >= fromDate && s.PlannedFor <= toDate)
            .ToListAsync(ct);

        return
        [
            .. completed.Cast<TrainingSession>()
                .Concat(active)
                .Concat(planned)
                .Concat(skipped)
                .OrderBy(session => session.Date)
                .ThenBy(session => session.CreatedAt)
        ];
    }
}
