using lionheart.Data;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.PersonalRecords;

public interface IPersonalRecordDetector
{
    /// <summary>
    /// Works out which records a set of sessions becoming final established, and stages them —
    /// together with the demotion of whatever they beat — into the caller's unit of work.
    /// </summary>
    /// <remarks>
    /// Staged rather than saved: the caller's save is the one that makes the sessions final, and a
    /// record is a fact about a session that happened. Two saves would leave a window where the
    /// records exist and the sessions do not, or the other way round.
    /// <para>
    /// Takes every session the caller is about to save, not one at a time. What stands has to be
    /// read once for the whole batch: read per session, the second session's read cannot see the
    /// record the first one staged but has not saved, and both would be written standing. One
    /// utterance yielding two sessions on the same movement is enough to reach that, so it is not
    /// a race but an ordinary commit.
    /// </para>
    /// </remarks>
    Task StageAsync(IReadOnlyList<CompletedTrainingSession> sessions, CancellationToken ct);
}

/// <summary>
/// Where records come from: a session's sets becoming final.
/// </summary>
/// <remarks>
/// Completion is the seam because that is when the sets stop changing. An active session's sets
/// are replaced whole on every write, so detecting per set would mean retracting records the
/// athlete had only mistyped, and a record announced before the session was final could be taken
/// away again — the one thing a record must never do. Every path that produces a completed
/// session therefore comes through here: the two session-completion commands and the
/// interpretation engine's commit.
/// <para>
/// Nothing in here is a refusal. A session has already been accepted by the time its sets are
/// final, so there is no answer the athlete could act on — a set naming a movement that is not
/// this athlete's, or naming none at all, simply raises no record, which is the same thing the
/// session write already decided when it took the set without checking.
/// </para>
/// </remarks>
public sealed class PersonalRecordDetector : IPersonalRecordDetector
{
    private readonly ModelContext _context;
    private readonly TimeProvider _clock;

    public PersonalRecordDetector(ModelContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task StageAsync(IReadOnlyList<CompletedTrainingSession> sessions, CancellationToken ct)
    {
        // Grouped by athlete rather than assuming one: every caller today commits one athlete's
        // sessions, and the grouping costs a line instead of an assumption that would go wrong
        // silently.
        foreach (var athlete in sessions.GroupBy(session => session.UserId))
        {
            await StageForAthleteAsync(athlete.Key, [.. athlete], ct);
        }
    }

    private async Task StageForAthleteAsync(
        Guid userId, IReadOnlyList<CompletedTrainingSession> sessions, CancellationToken ct)
    {
        var claimed = await EligibleAsync(userId, sessions, ct);
        if (claimed.Count == 0) return;

        var incumbents = await StandingAsync(userId, ct);
        var awarded = new List<PersonalRecord>();

        // Oldest first, so a batch holding two sessions on one movement chains them the way they
        // happened rather than letting the later read of what stands decide.
        foreach (var session in claimed.OrderBy(session => session.Session.Period.StartTimeUtc))
        {
            foreach (var contested in session.Candidates.GroupBy(candidate => candidate.Key))
            {
                var record = Awarded(userId, contested.Key, [.. contested], incumbents, session.AchievedAt);
                if (record is null) continue;

                // The record this session set is what the next session in the batch has to beat.
                incumbents[contested.Key] = record;
                awarded.Add(record);
            }
        }

        _context.PersonalRecords.AddRange(awarded);
    }

    /// <summary>
    /// Each session's candidates, less any on a movement that is not this athlete's.
    /// </summary>
    /// <remarks>
    /// Sets carry a movement id the session write never checked, so without this a mistyped id
    /// would open a record on a movement that does not exist, or on another athlete's. One query
    /// for the whole batch.
    /// </remarks>
    private async Task<IReadOnlyList<SessionClaims>> EligibleAsync(
        Guid userId, IReadOnlyList<CompletedTrainingSession> sessions, CancellationToken ct)
    {
        var claims = sessions
            .Select(session => new SessionClaims(session, RecordCandidates.From(session)))
            .Where(session => session.Candidates.Count > 0)
            .ToList();

        var named = claims
            .SelectMany(session => session.Candidates)
            .Select(candidate => candidate.Key.Subject)
            .OfType<RecordSubject.Movement>()
            .Select(subject => subject.MovementDataId)
            .Distinct()
            .ToList();

        if (named.Count == 0) return claims;

        var owned = (await _context.MovementDatas
                .Where(movement => movement.UserID == userId && named.Contains(movement.MovementDataID))
                .Select(movement => movement.MovementDataID)
                .ToListAsync(ct))
            .ToHashSet();

        return
        [
            .. claims
                .Select(session => session with
                {
                    Candidates =
                    [
                        .. session.Candidates.Where(candidate =>
                            candidate.Key.Subject is not RecordSubject.Movement movement
                            || owned.Contains(movement.MovementDataId))
                    ]
                })
                .Where(session => session.Candidates.Count > 0)
        ];
    }

    /// <summary>
    /// Every record currently standing for this athlete, keyed by what identifies it.
    /// </summary>
    /// <remarks>
    /// The whole standing set rather than the subjects this batch touched: it is one indexed query
    /// either way, a personal log holds tens of these rather than thousands, and the alternative
    /// is translating a subject's fill-state conversion inside a <c>Contains</c>.
    /// </remarks>
    private async Task<Dictionary<RecordKey, PersonalRecord>> StandingAsync(
        Guid userId, CancellationToken ct)
    {
        var standing = await _context.PersonalRecords
            .Where(record => record.UserId == userId
                             && EF.Property<bool>(record, RemodelConfiguration.RecordIsStandingColumn))
            .ToListAsync(ct);

        // Grouped rather than keyed directly, because two standing records of one identity is a
        // state no relational constraint here can refuse: SQLite checks a unique index one
        // statement at a time, so the index that would carry the invariant rejects every
        // hand-over instead. Reaching it now takes two requests racing, and when it happens the
        // better of the two is the one to beat — throwing would stop the athlete logging training
        // over an anomaly they can see and retract.
        return standing
            .GroupBy(record => record.Key)
            .ToDictionary(
                contested => contested.Key,
                contested => contested.MaxBy(record => record.Achievement.Rank)!);
    }

    /// <summary>
    /// The one record a session sets for an identity, if it set one at all.
    /// </summary>
    /// <remarks>
    /// One record per identity per session, even when several of its sets each beat the standing
    /// one. The sets in between never stood as records — nobody held them for any length of time —
    /// so a row each would read as several records set in the same instant, with no way for a
    /// reader of the history to tell which actually held. The best of them becomes the record and
    /// the rest are simply sets in the session that produced it.
    /// </remarks>
    private PersonalRecord? Awarded(
        Guid userId,
        RecordKey key,
        IReadOnlyList<RecordCandidate> contested,
        IReadOnlyDictionary<RecordKey, PersonalRecord> incumbents,
        DateTime achievedAt)
    {
        var best = contested.MaxBy(candidate => candidate.Achievement.Rank)!;

        if (!incumbents.TryGetValue(key, out var held))
        {
            return PersonalRecord.FirstOf(NextId(), userId, key.Subject, best.Achievement, achievedAt);
        }

        return best.Achievement.Beats(held.Achievement)
            ? PersonalRecord.Succeeding(NextId(), best.Achievement, achievedAt, held)
            : null;
    }

    /// <summary>
    /// Version 7 so successive records sort together and inserts land in one region of the index.
    /// </summary>
    private Guid NextId() => Guid.CreateVersion7(_clock.GetUtcNow());

    /// <summary>One session and what it claims, so the batch carries both through the filter.</summary>
    private sealed record SessionClaims(
        CompletedTrainingSession Session, IReadOnlyList<RecordCandidate> Candidates)
    {
        public DateTime AchievedAt => Session.Period.StartTimeUtc;
    }
}
