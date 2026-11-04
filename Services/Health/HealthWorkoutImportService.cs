using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Health;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Profile;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Health;

public interface IHealthWorkoutImportService
{
    Task<Result<HealthImportOutcome, Problem>> ImportAsync(IdentityUser user, HealthWorkoutImport workout, CancellationToken ct);
}

/// <summary>
/// Turns an Apple Health workout the phone read into training: a completed session, its recording,
/// and the records it set — or an answer saying why not.
/// </summary>
/// <remarks>
/// Idempotent on the workout's Health UUID, which the session's source carries and a unique index
/// holds: a workout offered again answers <see cref="HealthImportOutcome.AlreadyImported"/> and
/// writes nothing, so the phone can resend whatever it is unsure of.
/// <para>
/// Records follow the rule an import of history has to. A run newer than every record it contests
/// is judged against what stands, as a run logged live would be. One that predates a standing
/// record it contests — the first sync backfilling a year while a later trail run is already in the
/// log — is placed by replaying the athlete's records over his whole history in the same
/// transaction, since judging it against a later record would claim it beat something that came
/// after it.
/// </para>
/// </remarks>
public sealed class HealthWorkoutImportService : IHealthWorkoutImportService
{
    /// <summary>How far before a workout a logged session may start and still hold it: no session runs a day.</summary>
    private static readonly TimeSpan LongestSession = TimeSpan.FromDays(1);

    private readonly ModelContext _context;
    private readonly TimeProvider _clock;
    private readonly IPersonalRecordDetector _records;
    private readonly PersonalRecordRebuild _rebuild;

    public HealthWorkoutImportService(
        ModelContext context, TimeProvider clock, IPersonalRecordDetector records, PersonalRecordRebuild rebuild)
    {
        _context = context;
        _clock = clock;
        _records = records;
        _rebuild = rebuild;
    }

    public Task<Result<HealthImportOutcome, Problem>> ImportAsync(
        IdentityUser user, HealthWorkoutImport workout, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => workout.Stated().Map(stated => (userId, stated)))
            .BindAsync(offered => OutcomeAsync(offered.userId, offered.stated, ct));

    // ---- Guard and stage ----

    private async Task<Result<HealthImportOutcome, Problem>> OutcomeAsync(
        Guid userId, StatedHealthWorkout workout, CancellationToken ct) =>
        await ImportedAsAsync(userId, workout.Source.WorkoutId, ct) is { } existing
            ? Answer(new HealthImportOutcome.AlreadyImported(existing))
            : HealthWorkoutDecisions.Decide(workout, await LoggedAroundAsync(userId, workout.Period, ct), NewIdentity(userId)) switch
            {
                HealthWorkoutDecision.Import import => await CommitAsync(import, ct),
                HealthWorkoutDecision.Excluded excluded => Answer(
                    new HealthImportOutcome.NotImported(excluded.Reason, excluded.MatchedSessionId)),
                _ => throw new System.Diagnostics.UnreachableException(),
            };

    private Task<Guid?> ImportedAsAsync(Guid userId, Guid workoutId, CancellationToken ct) =>
        _context.Sessions
            .Where(session => session.UserId == userId
                              && EF.Property<Guid?>(session, RemodelConfiguration.HealthWorkoutIdColumn) == workoutId)
            .Select(session => (Guid?)session.TrainingSessionId)
            .FirstOrDefaultAsync(ct);

    /// <summary>Every completed session that could hold the workout: one that starts before it ends and less than a day before it starts.</summary>
    private async Task<IReadOnlyList<CompletedTrainingSession>> LoggedAroundAsync(
        Guid userId, Model.Analysis.Interval period, CancellationToken ct)
    {
        var (from, to) = ((period.Start - LongestSession).UtcDateTime, period.End.UtcDateTime);
        return await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .Where(session => EF.Property<DateTime>(session, "StartTime") >= from
                              && EF.Property<DateTime>(session, "StartTime") <= to)
            .ToListAsync(ct);
    }

    private NewSessionIdentity NewIdentity(Guid userId)
    {
        var now = _clock.GetUtcNow();
        return new NewSessionIdentity(Guid.CreateVersion7(now), userId, now.UtcDateTime);
    }

    // ---- Commit ----

    private async Task<Result<HealthImportOutcome, Problem>> CommitAsync(
        HealthWorkoutDecision.Import import, CancellationToken ct) =>
        await InOrderAsync(import.Session, ct)
            ? await SavedAsync(import, detectRecords: true, ct)
            : await _context.InTransactionAsync(
                () => _rebuild.ReplayableAsync(import.Session.UserId, ct)
                    .BindAsync(_ => SavedAsync(import, detectRecords: false, ct))
                    .BindAsync(outcome => _rebuild.ReplayAsync(import.Session.UserId, ct).MapAsync(_ => outcome))
                    .MapErrorAsync(ReplayRefusal),
                ct);

    /// <summary>
    /// Whether no standing record the run contests was set at or after its start, so detecting its
    /// records against what stands gives the chains a live log would have.
    /// </summary>
    private async Task<bool> InOrderAsync(CompletedTrainingSession run, CancellationToken ct)
    {
        var contested = RecordCandidates.From(run).Select(candidate => candidate.Key).ToHashSet();
        if (contested.Count is 0) return true;

        var start = run.Period.StartTimeUtc;
        var later = await _context.PersonalRecords
            .AsNoTracking()
            .Where(record => record.UserId == run.UserId
                             && EF.Property<bool>(record, RemodelConfiguration.RecordIsStandingColumn)
                             && record.AchievedAt >= start)
            .ToListAsync(ct);

        return !later.Any(record => contested.Contains(record.Key));
    }

    /// <summary>
    /// The one save: the session, its recording, its records when detected here, and the chat
    /// card's Recent State marked stale, since it states the latest runs.
    /// </summary>
    private async Task<Result<HealthImportOutcome, Problem>> SavedAsync(
        HealthWorkoutDecision.Import import, bool detectRecords, CancellationToken ct)
    {
        if (detectRecords) await _records.StageAsync([import.Session], ct);
        _context.Sessions.Add(import.Session);
        _context.WorkoutRecordings.Add(import.Recording);
        await _context.StageRecentStateInvalidationAsync(import.Session.UserId, ct);

        try
        {
            await _context.SaveChangesAsync(ct);
            return Answer(new HealthImportOutcome.Imported(TrainingSessionView.Of(import.Session)));
        }
        catch (DbUpdateException e) when (e.IsRefusedBy(RemodelConfiguration.HealthWorkoutIndexColumns))
        {
            // Another request imported the same workout between this one's check and its save — the
            // phone resending a page it was unsure of. That import stands, and this one is its echo.
            _context.ChangeTracker.Clear();
            return await ImportedAsAsync(import.Session.UserId, import.Source.WorkoutId, ct) is { } existing
                ? Answer(new HealthImportOutcome.AlreadyImported(existing))
                : throw new InvalidOperationException("A unique refusal named a Health workout no session holds.", e);
        }
    }

    /// <summary>The replay's refusal, said about the run rather than about record ids.</summary>
    private static Problem ReplayRefusal(Problem problem) => problem switch
    {
        Problem.Conflict => new Problem.Conflict(
            "This run is older than a standing trail-running record, so placing it means re-detecting every "
            + "record from the whole history — which would award a record you withdrew all over again. It was "
            + "not imported."),
        _ => problem,
    };

    private static Result<HealthImportOutcome, Problem> Answer(HealthImportOutcome outcome) =>
        Result<HealthImportOutcome, Problem>.Success(outcome);
}
