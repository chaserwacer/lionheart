using lionheart.Data;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.PersonalRecords;

public interface IPersonalRecordDetector
{
    /// <summary>
    /// Works out which records a completed session established and stages them, together with the
    /// demotion of whatever they beat, into the caller's unit of work.
    /// </summary>
    /// <remarks>
    /// Staged rather than saved: the caller's save is the one that makes the session final, and a
    /// record is a fact about a session that happened. Two saves would leave a window where the
    /// records exist and the session does not, or the other way round.
    /// </remarks>
    Task<IReadOnlyList<PersonalRecord>> StageAsync(CompletedTrainingSession session, CancellationToken ct);
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
/// final, so there is no answer the athlete could act on — a movement the athlete does not own
/// simply raises no record, which is the same thing the session write already decided when it
/// took the set without checking.
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

    public async Task<IReadOnlyList<PersonalRecord>> StageAsync(
        CompletedTrainingSession session, CancellationToken ct)
    {
        var candidates = await EligibleAsync(session, ct);
        if (candidates.Count == 0) return [];

        var awarded = Award(
            session.UserId,
            candidates,
            await StandingAsync(session.UserId, ct),
            session.Period.StartTimeUtc);
        _context.PersonalRecords.AddRange(awarded);

        return awarded;
    }

    /// <summary>
    /// The session's candidates, less any on a movement that is not this athlete's.
    /// </summary>
    /// <remarks>
    /// Sets carry a movement id the session write never checked, so without this a mistyped id
    /// would open a record on a movement that does not exist, or on another athlete's.
    /// </remarks>
    private async Task<IReadOnlyList<RecordCandidate>> EligibleAsync(
        CompletedTrainingSession session, CancellationToken ct)
    {
        var candidates = RecordCandidates.From(session);
        if (candidates.Count == 0) return [];

        var claimed = candidates
            .Select(candidate => candidate.Subject)
            .OfType<RecordSubject.Movement>()
            .Select(subject => subject.MovementDataId)
            .Distinct()
            .ToList();

        if (claimed.Count == 0) return candidates;

        var owned = await _context.MovementDatas
            .Where(movement => movement.UserID == session.UserId && claimed.Contains(movement.MovementDataID))
            .Select(movement => movement.MovementDataID)
            .ToListAsync(ct);

        return
        [
            .. candidates.Where(candidate =>
                candidate.Subject is not RecordSubject.Movement movement
                || owned.Contains(movement.MovementDataId))
        ];
    }

    /// <summary>
    /// Every record currently standing for this athlete, keyed by what identifies it.
    /// </summary>
    /// <remarks>
    /// The whole standing set rather than the subjects this session touched: it is one indexed
    /// query either way, a personal log holds tens of these rather than thousands, and the
    /// alternative is translating a subject's fill-state conversion inside a <c>Contains</c>.
    /// </remarks>
    private async Task<IReadOnlyDictionary<(RecordSubject, RecordKind), PersonalRecord>> StandingAsync(
        Guid userId, CancellationToken ct)
    {
        var standing = await _context.PersonalRecords
            .Where(record => record.UserId == userId
                             && EF.Property<bool>(record, RemodelConfiguration.RecordIsStandingColumn))
            .ToListAsync(ct);

        // Grouped rather than keyed directly, because two standing records of one kind is a state
        // no relational constraint here can refuse: SQLite checks a unique index one statement at
        // a time, so the index that would carry the invariant rejects every hand-over instead. It
        // takes two completions racing to be the first record of a kind to reach it, and when it
        // does, the better of them is the one to beat — throwing would stop the athlete logging
        // training over an anomaly they can see and retract.
        return standing
            .GroupBy(record => (record.Subject, record.Kind))
            .ToDictionary(
                contested => contested.Key,
                contested => contested.MaxBy(record => record.Achievement.Rank)!);
    }

    private IReadOnlyList<PersonalRecord> Award(
        Guid userId,
        IReadOnlyList<RecordCandidate> candidates,
        IReadOnlyDictionary<(RecordSubject, RecordKind), PersonalRecord> standing,
        DateTime achievedAt) =>
    [
        .. candidates
            .GroupBy(candidate => (candidate.Subject, candidate.Kind))
            .SelectMany(contested => Awarded(userId, [.. contested], standing, achievedAt))
    ];

    /// <summary>
    /// The one record a session sets for a key, if it set one at all.
    /// </summary>
    /// <remarks>
    /// One record per key per completion, even when several of the session's sets each beat the
    /// standing one. The sets in between never stood as records — nobody held them for any length
    /// of time — so a row each would read as several records set in the same instant, with no way
    /// for a reader of the history to tell which actually held. The best of them becomes the
    /// record and the rest are simply sets in the session that produced it.
    /// </remarks>
    private IReadOnlyList<PersonalRecord> Awarded(
        Guid userId,
        IReadOnlyList<RecordCandidate> contested,
        IReadOnlyDictionary<(RecordSubject, RecordKind), PersonalRecord> standing,
        DateTime achievedAt)
    {
        var key = (contested[0].Subject, contested[0].Kind);

        if (!standing.TryGetValue(key, out var held))
        {
            // Nothing stands yet, so there is no unit to agree with. Where a session logged one
            // movement in two units any first record is as defensible as the other, and the
            // larger number is the same tie-break every comparison here uses.
            var first = contested.MaxBy(candidate => candidate.Achievement.Rank)!;
            return [PersonalRecord.FirstOf(NextId(), userId, first.Subject, first.Achievement, achievedAt)];
        }

        var better = contested
            .Where(candidate => candidate.Achievement.Beats(held.Achievement))
            .MaxBy(candidate => candidate.Achievement.Rank);

        return better is null
            ? []
            : [PersonalRecord.Succeeding(NextId(), better.Achievement, achievedAt, held)];
    }

    /// <summary>
    /// Version 7 so successive records sort together and inserts land in one region of the index.
    /// </summary>
    private Guid NextId() => Guid.CreateVersion7(_clock.GetUtcNow());
}
