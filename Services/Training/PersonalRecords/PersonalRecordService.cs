using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.Training.PersonalRecords;

public interface IPersonalRecordService
{
    /// <summary>Every record currently standing for the athlete, across every subject.</summary>
    Task<Result<IReadOnlyList<PersonalRecordView>, Problem>> GetStandingAsync(
        IdentityUser user, CancellationToken ct);

    /// <summary>The standing records grouped by what they are held against.</summary>
    Task<Result<IReadOnlyList<SubjectRecordsView>, Problem>> GetBySubjectAsync(
        IdentityUser user, CancellationToken ct);

    /// <summary>One movement's standing records.</summary>
    Task<Result<SubjectRecordsView, Problem>> GetForMovementAsync(
        IdentityUser user, Guid movementDataId, CancellationToken ct);

    /// <summary>
    /// Everything ever recorded for one subject and kind, newest first — the progression, with
    /// what was beaten and what was withdrawn both visible.
    /// </summary>
    Task<Result<IReadOnlyList<PersonalRecordView>, Problem>> GetHistoryAsync(
        IdentityUser user, RecordHistoryQuery query, CancellationToken ct);

    /// <summary>
    /// Withdraws the standing record, putting back whatever it replaced.
    /// </summary>
    Task<Result<RetractionOutcome, Problem>> RetractAsync(
        IdentityUser user, Guid personalRecordId, CancellationToken ct);
}

/// <summary>
/// Reading records, and the one write the athlete makes against them directly.
/// </summary>
/// <remarks>
/// Records are never created here: they are established by a session's sets becoming final, which
/// is <see cref="IPersonalRecordDetector"/>'s job. The only thing an athlete can do to a record
/// from outside a session is say it was not real.
/// </remarks>
[ToolProvider]
public sealed class PersonalRecordService : IPersonalRecordService
{
    private readonly ModelContext _context;
    private readonly TimeProvider _clock;

    public PersonalRecordService(ModelContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    // Reading records cannot refuse — an athlete's own records are either there or they are not —
    // so the reads are total and the Result arrives at the edge, around the two asks that can be
    // wrong: a movement that is not this athlete's, and a kind nobody defined.

    public async Task<Result<IReadOnlyList<PersonalRecordView>, Problem>> GetStandingAsync(
        IdentityUser user, CancellationToken ct) =>
        Result<IReadOnlyList<PersonalRecordView>, Problem>.Success(
            Views(Ordered(await StandingAsync(user, ct))));

    public async Task<Result<IReadOnlyList<SubjectRecordsView>, Problem>> GetBySubjectAsync(
        IdentityUser user, CancellationToken ct) =>
        Result<IReadOnlyList<SubjectRecordsView>, Problem>.Success(
            Grouped(await StandingAsync(user, ct)));

    public Task<Result<SubjectRecordsView, Problem>> GetForMovementAsync(
        IdentityUser user, Guid movementDataId, CancellationToken ct) =>
        OwnMovementAsync(user, movementDataId, ct)
            .MapAsync(async subject => SubjectRecordsView.Of(
                subject,
                [.. (await StandingAsync(user, ct)).Where(record => record.Subject == subject)]));

    public Task<Result<IReadOnlyList<PersonalRecordView>, Problem>> GetHistoryAsync(
        IdentityUser user, RecordHistoryQuery query, CancellationToken ct) =>
        query.Asked()
            .MapAsync(asked => HistoryAsync(Guid.Parse(user.Id), asked, ct));

    public Task<Result<RetractionOutcome, Problem>> RetractAsync(
        IdentityUser user, Guid personalRecordId, CancellationToken ct) =>
        OwnRecordAsync(Guid.Parse(user.Id), personalRecordId, ct)
            // Withdrawing a record that no longer stands collides with the state it is in rather
            // than being a malformed ask: the same request was legitimate while it stood, and a
            // beaten record is history the athlete is not being asked to revise.
            .EnsureAsync(
                record => record.IsStanding,
                _ => new Problem.Conflict("Only the standing record can be retracted."))
            .BindAsync(record => RetractAsync(record, ct));

    // ---- Reads ----

    /// <remarks>
    /// Read on the identity's own id rather than through <c>AthleteIdAsync</c>, which is the rule
    /// every other read in this domain follows: an account with no Lionheart profile yet is
    /// refused by the writes and answered empty by the reads, so a new athlete's records screen
    /// says "no records yet" instead of a conflict they cannot act on from there.
    /// </remarks>
    private async Task<IReadOnlyList<PersonalRecord>> StandingAsync(
        IdentityUser user, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);

        return await _context.PersonalRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId
                             && EF.Property<bool>(record, RemodelConfiguration.RecordIsStandingColumn))
            .ToListAsync(ct);
    }

    /// <remarks>
    /// The kind narrows in SQL and the subject in app. A subject is a fill-state behind a value
    /// converter, and a record's <c>==</c> is an operator call rather than an equality node, so
    /// the predicate would not translate — and the kind columns have already cut the read to one
    /// movement's worth of rows.
    /// </remarks>
    private async Task<IReadOnlyList<PersonalRecordView>> HistoryAsync(
        Guid userId, SubjectAndKind asked, CancellationToken ct)
    {
        var ofThatKind = await _context.PersonalRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId)
            .Where(record => EF.Property<string>(record, RemodelConfiguration.RecordKindColumn) == asked.Kind.Name
                             && EF.Property<int>(record, RemodelConfiguration.RecordBracketColumn) == asked.Kind.Bracket)
            .ToListAsync(ct);

        return Views(NewestFirst([.. ofThatKind.Where(record => record.Subject == asked.Subject)]));
    }

    /// <summary>
    /// A chain newest first, with a successor above the record it beat when the two share an
    /// instant.
    /// </summary>
    /// <remarks>
    /// Two links of one chain carry the same instant whenever one commit holds two sessions with
    /// the same period, and the date then says nothing about which came first. The lineage does:
    /// a record's depth is how many links stand behind it, so the deeper of two tied records is
    /// the one that took the other. The id is no substitute — within one millisecond a version-7
    /// id is random, so it put the beaten record above its successor about a third of the time.
    /// The unit separates the chains of a kind held in both, and the id only breaks ties the
    /// domain has no order for, so a read never comes back in two orders.
    /// </remarks>
    private static IEnumerable<PersonalRecord> NewestFirst(IReadOnlyList<PersonalRecord> chain)
    {
        var byId = chain.ToDictionary(record => record.PersonalRecordId);

        // A pointer walk rather than recursion, and a seen-set because the walk follows stored
        // pointers: a chain that loops is corruption, and it must not hang a read.
        int Depth(PersonalRecord record)
        {
            var seen = new HashSet<Guid> { record.PersonalRecordId };
            var depth = 0;
            while (record.Lineage is RecordLineage.Replaced replaced
                   && byId.TryGetValue(replaced.PersonalRecordId, out var behind)
                   && seen.Add(behind.PersonalRecordId))
            {
                depth++;
                record = behind;
            }

            return depth;
        }

        return chain
            .OrderByDescending(record => record.AchievedAt)
            .ThenByDescending(Depth)
            .ThenBy(record => record.Achievement.HeldIn)
            .ThenByDescending(record => record.PersonalRecordId);
    }

    /// <summary>
    /// The movement as a subject, or a refusal. Ownership is part of the lookup rather than a
    /// check after it: another athlete's movement has to be indistinguishable from one that does
    /// not exist, the same rule sessions follow.
    /// </summary>
    private async Task<Result<RecordSubject, Problem>> OwnMovementAsync(
        IdentityUser user, Guid movementDataId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var exists = await _context.MovementDatas
            .AnyAsync(movement => movement.MovementDataID == movementDataId
                                  && movement.UserID == userId, ct);

        return exists
            ? Result<RecordSubject, Problem>.Success(new RecordSubject.Movement(movementDataId))
            : Result<RecordSubject, Problem>.Failure(new Problem.NotFound("Movement not found."));
    }

    private async Task<Result<PersonalRecord, Problem>> OwnRecordAsync(
        Guid userId, Guid personalRecordId, CancellationToken ct)
    {
        var record = await _context.PersonalRecords
            .FirstOrDefaultAsync(r => r.PersonalRecordId == personalRecordId && r.UserId == userId, ct);

        return record is null
            ? Result<PersonalRecord, Problem>.Failure(new Problem.NotFound("Personal record not found."))
            : Result<PersonalRecord, Problem>.Success(record);
    }

    // ---- The one write ----

    /// <summary>
    /// Steps a subject's record back one link, and answers with whatever stands for it afterwards.
    /// </summary>
    /// <remarks>
    /// Only the standing record can be withdrawn, so the record behind it is ordinarily one the
    /// athlete beat, and it comes back. A record that was the first of its kind has nothing behind
    /// it, which is what <see cref="RetractionOutcome.NothingStands"/> says. The one exception is
    /// handled inside rather than refused: nothing here is the athlete's mistake, so nothing here
    /// may answer with a fault.
    /// </remarks>
    private async Task<Result<RetractionOutcome, Problem>> RetractAsync(
        PersonalRecord record, CancellationToken ct)
    {
        record.Retract(_clock.GetUtcNow().UtcDateTime);

        // Ordinarily the predecessor is beaten and comes back. Two requests racing to succeed the
        // same record leave two successors pointing at it, though — no relational constraint here
        // can refuse that — and withdrawing the first restores it, so withdrawing the second finds
        // it already standing; or already withdrawn, if the athlete went on to retract it. Either
        // way the withdrawal goes ahead and the predecessor is left as it is: restoring a standing
        // record is meaningless, and restoring a withdrawn one would resurrect a number the athlete
        // said was never real.
        var predecessor = await PredecessorAsync(record, ct);
        if (predecessor?.Standing is RecordStanding.Beaten) predecessor.Restore();

        await _context.SaveChangesAsync(ct);

        var stands = predecessor is { IsStanding: true }
            ? predecessor
            : await BestStandingAsync(record.UserId, record.Key, ct);

        return Result<RetractionOutcome, Problem>.Success(
            stands is null
                ? new RetractionOutcome.NothingStands()
                : new RetractionOutcome.Restored(PersonalRecordView.Of(stands)));
    }

    private Task<PersonalRecord?> PredecessorAsync(PersonalRecord record, CancellationToken ct) =>
        record.Lineage is RecordLineage.Replaced replaced
            ? _context.PersonalRecords.FirstOrDefaultAsync(
                r => r.PersonalRecordId == replaced.PersonalRecordId, ct)
            : Task.FromResult<PersonalRecord?>(null);

    /// <summary>
    /// Whatever stands for an identity once its record was withdrawn and nothing came back in its
    /// place — another successor of the same record, left standing by a race.
    /// </summary>
    private async Task<PersonalRecord?> BestStandingAsync(Guid userId, RecordKey key, CancellationToken ct)
    {
        var standing = await _context.PersonalRecords
            .Where(r => r.UserId == userId
                        && EF.Property<bool>(r, RemodelConfiguration.RecordIsStandingColumn))
            .ToListAsync(ct);

        return standing.Where(r => r.Key == key).MaxBy(r => r.Achievement.Rank);
    }

    private static IReadOnlyList<PersonalRecordView> Views(IEnumerable<PersonalRecord> records) =>
        [.. records.Select(PersonalRecordView.Of)];

    /// <summary>
    /// Newest first, and then by kind and bracket.
    /// </summary>
    /// <remarks>
    /// Every record one session set carries that session's instant, so the date alone leaves the
    /// order of a movement's four fresh records undefined — and a rep-max grid reads as a grid
    /// only if its rows come back in rep order.
    /// </remarks>
    private static IEnumerable<PersonalRecord> Ordered(IEnumerable<PersonalRecord> records) =>
        records
            .OrderByDescending(record => record.AchievedAt)
            .ThenBy(record => record.Kind.Name, StringComparer.Ordinal)
            .ThenBy(record => record.Kind.Bracket);

    // ---- Chat tools ----
    //
    // Separate from the routes' own methods because the registry binds a tool's arguments from the
    // model's JSON and injects only the signed-in athlete: a CancellationToken parameter would be
    // bound from JSON and described to the model as if it were an argument. The model is handed
    // the empty answer rather than a refusal it can do nothing about.

    [Tool(Name = "GetPersonalRecords", Description = "Every personal record the athlete currently holds, newest first: heaviest set, best set volume, each rep max, best session volume, longest distance, fastest time at a distance and best pace.")]
    public Task<IReadOnlyList<PersonalRecordView>> GetPersonalRecordsForChatAsync(IdentityUser user) =>
        GetStandingAsync(user, CancellationToken.None).MatchAsync(records => records, _ => []);

    [Tool(Name = "GetPersonalRecordsForMovement", Description = "The records the athlete currently holds on one movement, given its movement data id.")]
    public Task<IReadOnlyList<PersonalRecordView>> GetPersonalRecordsForMovementForChatAsync(
        IdentityUser user, Guid movementDataId) =>
        GetForMovementAsync(user, movementDataId, CancellationToken.None)
            .MatchAsync(subject => subject.Records, _ => []);

    private static IReadOnlyList<SubjectRecordsView> Grouped(IReadOnlyList<PersonalRecord> standing) =>
    [
        .. standing
            .GroupBy(record => record.Subject)
            .Select(subject => SubjectRecordsView.Of(subject.Key, [.. subject]))
            .OrderByDescending(view => view.LastAchievedAt)
    ];
}
