using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.PersonalRecords;

/// <summary>
/// Re-detects an athlete's personal records from scratch over every completed session, oldest
/// first, as if each had been logged live on its day.
/// </summary>
/// <remarks>
/// Detection is incremental: a session's records are judged against what stands when it completes.
/// That is right for training logged as it happens and wrong whenever history arrives out of order
/// — an older session imported after newer ones, or a change to how sets are read. Replaying the
/// whole history through the same <see cref="IPersonalRecordDetector"/> is the one way to get the
/// chains a live log would have produced, so it is what both cases use.
/// <para>
/// <b>A retracted record refuses the rebuild.</b> Retraction is the athlete saying the set behind
/// a record was not real, but the set stays in its session, so a replay would award the record
/// again and silently undo the decision. Keeping it withdrawn needs a rule for which sets a
/// retraction covers, which nothing has decided; until something does, the rebuild names the
/// retracted records and writes nothing.
/// </para>
/// </remarks>
public sealed class PersonalRecordRebuild
{
    private readonly ModelContext _context;
    private readonly IPersonalRecordDetector _detector;

    public PersonalRecordRebuild(ModelContext context, IPersonalRecordDetector detector)
    {
        _context = context;
        _detector = detector;
    }

    /// <summary>
    /// Rebuilds the athlete's records in a transaction of its own, keeping the result only if it
    /// differs from what was there.
    /// </summary>
    /// <remarks>
    /// A rebuild of records that were already right writes nothing, so it leaves every record id
    /// as it was — the ids are what a client holds to retract or read a chain.
    /// </remarks>
    public Task<Result<RecordRebuildReport, Problem>> RebuildAsync(Guid userId, CancellationToken ct) =>
        _context.InTransactionAsync(
            () => _context.KnownAthleteAsync(userId, ct).BindAsync(_ => ReplayAsync(userId, ct)),
            report => report.Changed > 0,
            ct);

    /// <summary>
    /// The athlete, if a replay of his history could run: refused while any record is withdrawn.
    /// </summary>
    /// <remarks>For a caller to check before it stages anything; <see cref="ReplayAsync"/> checks again.</remarks>
    public async Task<Result<Guid, Problem>> ReplayableAsync(Guid userId, CancellationToken ct) =>
        Unretracted(await HeldAsync(userId, ct)).Map(_ => userId);

    /// <summary>
    /// Replaces the athlete's records with the ones a replay of every completed session sets,
    /// inside the caller's transaction.
    /// </summary>
    /// <remarks>
    /// Saves as it goes — the deletion has to reach the database before the detector reads what
    /// stands, or it would judge the replay against the records being replaced — so it is only
    /// all-or-nothing inside a transaction the caller holds, and refuses to run outside one: a
    /// failure between the delete and the save would otherwise leave the athlete with no records.
    /// </remarks>
    public async Task<Result<RecordRebuildReport, Problem>> ReplayAsync(Guid userId, CancellationToken ct)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A record replay deletes before it saves, so it runs only inside the caller's transaction.");

        return await Unretracted(await HeldAsync(userId, ct))
            .MapAsync(before => ReplaceAsync(userId, before, ct));
    }

    private async Task<IReadOnlyList<PersonalRecord>> HeldAsync(Guid userId, CancellationToken ct) =>
        await _context.PersonalRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId)
            .ToListAsync(ct);

    private static Result<IReadOnlyList<PersonalRecord>, Problem> Unretracted(IReadOnlyList<PersonalRecord> held)
    {
        var retracted = held.Where(record => record.Standing is RecordStanding.Retracted).ToList();

        return retracted.Count == 0
            ? Result<IReadOnlyList<PersonalRecord>, Problem>.Success(held)
            : Result<IReadOnlyList<PersonalRecord>, Problem>.Failure(new Problem.Conflict(
                $"{retracted.Count} record(s) were withdrawn by the athlete. Rebuilding would award them again "
                + "from the sets still in their sessions, undoing the withdrawal, so nothing has been written. "
                + "Withdrawn records:\n"
                + string.Join("\n", retracted
                    .OrderBy(record => record.AchievedAt)
                    .Select(record => $"  {Described(record)}"))));
    }

    private static string Described(PersonalRecord record)
    {
        var subject = record.Subject.Name(id => $"movement {id}");
        var bracket = record.Kind.Bracket > 0 ? $" {record.Kind.Bracket}" : string.Empty;

        return $"{subject}: {record.Kind.Name}{bracket}, set {record.AchievedAt:yyyy-MM-dd}";
    }

    private async Task<RecordRebuildReport> ReplaceAsync(
        Guid userId, IReadOnlyList<PersonalRecord> before, CancellationToken ct)
    {
        await _context.PersonalRecords.Where(record => record.UserId == userId).ExecuteDeleteAsync(ct);

        var sessions = await _context.Sessions
            .AsNoTracking()
            .OfType<CompletedTrainingSession>()
            .Where(session => session.UserId == userId)
            .ToListAsync(ct);

        await _detector.StageAsync(sessions, ct);

        var after = _context.ChangeTracker.Entries<PersonalRecord>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

        await _context.SaveChangesAsync(ct);

        return RecordRebuildReport.Between(before, after);
    }
}
