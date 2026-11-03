using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Programs;
using lionheart.Model.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Programs;

public interface ITrainingProgramService
{
    Task<Result<TrainingProgramView, Problem>> PlanAsync(IdentityUser user, PlanProgramRequest request, CancellationToken ct);

    Task<Result<TrainingProgramView, Problem>> ActivateAsync(IdentityUser user, Guid programId, CancellationToken ct);

    Task<Result<TrainingProgramView, Problem>> CompleteAsync(IdentityUser user, Guid programId, CompleteProgramRequest request, CancellationToken ct);

    Task<Result<TrainingProgramView, Problem>> UpdateAsync(IdentityUser user, Guid programId, UpdateProgramRequest request, CancellationToken ct);

    Task<Result<TrainingProgramView, Problem>> GetAsync(IdentityUser user, Guid programId, CancellationToken ct);

    Task<Result<IReadOnlyList<TrainingProgramView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct);

    Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid programId, CancellationToken ct);
}

/// <summary>
/// Training programs, whose lifecycle is Planned → Active → Completed. Same Guard → Stage → Commit
/// shape as <see cref="Sessions.TrainingSessionService"/>, and the same 404-for-someone-else's-row
/// rule.
/// </summary>
public sealed class TrainingProgramService : ITrainingProgramService
{
    private readonly ModelContext _context;
    private readonly TimeProvider _clock;

    public TrainingProgramService(ModelContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<Result<TrainingProgramView, Problem>> PlanAsync(
        IdentityUser user, PlanProgramRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => Ordered(request.StartDate, request.EndDate)
                .Map(_ => new PlannedTrainingProgram
                {
                    TrainingProgramId = Guid.CreateVersion7(_clock.GetUtcNow()),
                    UserId = userId,
                    Title = request.Title,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Tags = request.Tags,
                }))
            .BindAsync(program => AddAsync(program, ct));

    public Task<Result<TrainingProgramView, Problem>> ActivateAsync(
        IdentityUser user, Guid programId, CancellationToken ct) =>
        OwnedAsync(user, programId, ct)
            .BindAsync(program => Activated(program)
                .BindAsync(activated => ReplaceAsync(program, activated, ct)));

    public Task<Result<TrainingProgramView, Problem>> CompleteAsync(
        IdentityUser user, Guid programId, CompleteProgramRequest request, CancellationToken ct) =>
        OwnedAsync(user, programId, ct)
            .BindAsync(program => Completed(program, request.Summary)
                .BindAsync(completed => ReplaceAsync(program, completed, ct)));

    public Task<Result<TrainingProgramView, Problem>> UpdateAsync(
        IdentityUser user, Guid programId, UpdateProgramRequest request, CancellationToken ct) =>
        OwnedAsync(user, programId, ct)
            .BindAsync(program => Ordered(request.StartDate, request.EndDate)
                // Describing a program says nothing about whether it has started, so `with` on the
                // abstract base keeps whichever lifecycle state it is already in.
                .Map(_ => program with
                {
                    Title = request.Title,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Tags = request.Tags,
                })
                .BindAsync(updated => ReplaceAsync(program, updated, ct)));

    public Task<Result<TrainingProgramView, Problem>> GetAsync(
        IdentityUser user, Guid programId, CancellationToken ct) =>
        OwnedAsync(user, programId, ct).MapAsync(TrainingProgramView.Of);

    public async Task<Result<IReadOnlyList<TrainingProgramView>, Problem>> GetAllAsync(
        IdentityUser user, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var programs = await _context.Programs
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(ct);

        return Result<IReadOnlyList<TrainingProgramView>, Problem>.Success(
            [.. programs.Select(TrainingProgramView.Of)]);
    }

    public Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid programId, CancellationToken ct) =>
        OwnedAsync(user, programId, ct)
            .BindAsync(program => UnreferencedAsync(program, ct))
            .BindAsync(program => RemoveAsync(program, ct));

    // ---- Guard ----

    private async Task<Result<TrainingProgram, Problem>> OwnedAsync(
        IdentityUser user, Guid programId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var program = await _context.Programs
            .FirstOrDefaultAsync(p => p.TrainingProgramId == programId && p.UserId == userId, ct);

        return program is null
            ? Result<TrainingProgram, Problem>.Failure(new Problem.NotFound("Training program not found."))
            : Result<TrainingProgram, Problem>.Success(program);
    }

    /// <summary>
    /// A session's membership is a program id with no relational foreign key behind it (the
    /// Standalone sentinel could not satisfy one), so nothing at the database level stops a delete
    /// from leaving sessions pointing at a program that no longer exists. Refusing is the honest
    /// answer: it says what is in the way instead of silently rewriting the athlete's sessions.
    /// </summary>
    private async Task<Result<TrainingProgram, Problem>> UnreferencedAsync(
        TrainingProgram program, CancellationToken ct)
    {
        // Membership crosses the converter as a whole record, so the match is made on the
        // rehydrated fill-state rather than in SQL; the projection still reads one column.
        var memberships = await _context.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == program.UserId)
            .Select(s => s.Membership)
            .ToListAsync(ct);

        var sessions = memberships.Count(m =>
            m is InProgram inProgram && inProgram.TrainingProgramId == program.TrainingProgramId);

        return sessions is 0
            ? Result<TrainingProgram, Problem>.Success(program)
            : Result<TrainingProgram, Problem>.Failure(new Problem.Conflict(
                $"This program still has {sessions} session(s). Move or delete them first."));
    }

    private static Result<Unit, Problem> Ordered(DateOnly startDate, DateOnly endDate) =>
        endDate < startDate
            ? Result<Unit, Problem>.Failure(
                new Problem.Invalid(nameof(PlanProgramRequest.EndDate), "The program ends before it starts."))
            : Result<Unit, Problem>.Success(Unit.Value);

    // ---- Stage ----

    private static Result<TrainingProgram, Problem> Activated(TrainingProgram program) =>
        program is PlannedTrainingProgram planned
            ? Result<TrainingProgram, Problem>.Success(new ActiveTrainingProgram
            {
                TrainingProgramId = planned.TrainingProgramId,
                UserId = planned.UserId,
                Title = planned.Title,
                StartDate = planned.StartDate,
                EndDate = planned.EndDate,
                Tags = planned.Tags,
            })
            : Refusal(program, "activated", "planned");

    private static Result<TrainingProgram, Problem> Completed(TrainingProgram program, string summary) =>
        program is ActiveTrainingProgram active
            ? Result<TrainingProgram, Problem>.Success(new CompletedTrainingProgram(summary)
            {
                TrainingProgramId = active.TrainingProgramId,
                UserId = active.UserId,
                Title = active.Title,
                StartDate = active.StartDate,
                EndDate = active.EndDate,
                Tags = active.Tags,
            })
            : Refusal(program, "completed", "active");

    private static Result<TrainingProgram, Problem> Refusal(TrainingProgram program, string action, string from) =>
        Result<TrainingProgram, Problem>.Failure(new Problem.Conflict(
            $"A {Lifecycle(program)} program cannot be {action}; only a {from} program can."));

    private static string Lifecycle(TrainingProgram program) => program switch
    {
        PlannedTrainingProgram => "planned",
        ActiveTrainingProgram => "active",
        CompletedTrainingProgram => "completed",
        _ => throw new UnreachableException()
    };

    // ---- Commit ----

    private async Task<Result<TrainingProgramView, Problem>> AddAsync(TrainingProgram program, CancellationToken ct)
    {
        _context.Programs.Add(program);
        await _context.SaveChangesAsync(ct);

        return Result<TrainingProgramView, Problem>.Success(TrainingProgramView.Of(program));
    }

    /// <summary>Remove-then-add for the reason given on <see cref="Sessions.TrainingSessionService"/>.</summary>
    private async Task<Result<TrainingProgramView, Problem>> ReplaceAsync(
        TrainingProgram previous, TrainingProgram next, CancellationToken ct)
    {
        _context.Programs.Remove(previous);
        _context.Programs.Add(next);
        await _context.SaveChangesAsync(ct);

        return Result<TrainingProgramView, Problem>.Success(TrainingProgramView.Of(next));
    }

    private async Task<Result<Unit, Problem>> RemoveAsync(TrainingProgram program, CancellationToken ct)
    {
        _context.Programs.Remove(program);
        await _context.SaveChangesAsync(ct);

        return Result<Unit, Problem>.Success(Unit.Value);
    }
}
