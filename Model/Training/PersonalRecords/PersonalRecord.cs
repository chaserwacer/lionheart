namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// The best an athlete has done at one thing: a subject, a question asked about it, and the
/// performance that currently answers it, with the chain back through everything it replaced.
/// </summary>
/// <remarks>
/// A sealed class rather than a record, unlike almost everything else in this domain, because a
/// record has identity and a lifecycle: its <see cref="Standing"/> changes when a later session
/// beats it or the athlete withdraws it. Give a mutating entity value equality and two freshly
/// built ones compare equal while the hash of a tracked one moves as it changes, which breaks
/// change tracking. Everything it holds is an immutable record; only the standing moves, and only
/// through the three transitions below.
/// <para>
/// Records are established when a session's sets become final, and exactly one record of a kind is
/// written per completion — the best of that session's candidates. The sets a session beat its own
/// record with never stood as records, so writing one row each would make the history read as
/// several records set in the same instant with no way to tell which of them actually held.
/// </para>
/// </remarks>
public sealed class PersonalRecord
{
    private PersonalRecord(
        Guid personalRecordId,
        Guid userId,
        RecordSubject subject,
        Achievement achievement,
        DateTime achievedAt,
        RecordStanding standing,
        RecordLineage lineage)
    {
        PersonalRecordId = personalRecordId;
        UserId = userId;
        Subject = subject;
        Achievement = achievement;
        AchievedAt = achievedAt;
        Standing = standing;
        Lineage = lineage;
    }

    public Guid PersonalRecordId { get; private init; }

    public Guid UserId { get; private init; }

    public RecordSubject Subject { get; private init; }

    /// <summary>What this record is the best of, and the performance holding it.</summary>
    public Achievement Achievement { get; private init; }

    /// <summary>
    /// When the session behind it was performed — not when the row was written.
    /// </summary>
    /// <remarks>
    /// A session logged a fortnight after the fact set its records on the day it happened. Stamping
    /// the write instant instead would put every backfilled record in the wrong week and make a
    /// progression read as a flurry.
    /// </remarks>
    public DateTime AchievedAt { get; private init; }

    public RecordStanding Standing { get; private set; }

    public RecordLineage Lineage { get; private init; }

    /// <summary>Which record this is, for matching a session's candidates against what stands.</summary>
    public RecordKey Key => new(Subject, Achievement.Kind, Achievement.HeldIn);

    public RecordKind Kind => Achievement.Kind;

    public bool IsStanding => Standing is RecordStanding.Current;

    /// <summary>
    /// The first record of its kind on a subject.
    /// </summary>
    public static PersonalRecord FirstOf(
        Guid personalRecordId, Guid userId, RecordSubject subject, Achievement achievement, DateTime achievedAt) =>
        new(
            personalRecordId,
            userId,
            subject,
            achievement,
            achievedAt,
            new RecordStanding.Current(),
            new RecordLineage.FirstOfItsKind());

    /// <summary>
    /// A record that takes over from <paramref name="beaten"/>, which this leaves beaten.
    /// </summary>
    /// <remarks>
    /// Both halves of the hand-over happen here so a new record and the demotion of the one it
    /// replaced cannot be staged apart — two standing records of one identity is the state this
    /// aggregate exists to make impossible, and with no relational constraint able to express it
    /// (see <see cref="lionheart.Data.RemodelConfiguration"/>) this factory is where it lives.
    /// That is why it refuses rather than trusts: every caller is inside this assembly, so a
    /// hand-over that does not hold is a defect here and not an answer for an athlete.
    /// </remarks>
    public static PersonalRecord Succeeding(
        Guid personalRecordId, Achievement achievement, DateTime achievedAt, PersonalRecord beaten)
    {
        if (beaten.Standing is not RecordStanding.Current)
            throw new InvalidOperationException(
                "Only the standing record can be succeeded; this one is already out of the chain.");
        if (achievement.Kind != beaten.Achievement.Kind || achievement.HeldIn != beaten.Achievement.HeldIn)
            throw new InvalidOperationException(
                $"A {achievement.Kind.Name} record cannot succeed a {beaten.Achievement.Kind.Name} one.");
        if (!achievement.Beats(beaten.Achievement))
            throw new InvalidOperationException(
                "A record only succeeds one it beats; equalling the standing record does not take it.");

        beaten.Standing = new RecordStanding.Beaten(achievedAt);

        return new PersonalRecord(
            personalRecordId,
            beaten.UserId,
            beaten.Subject,
            achievement,
            achievedAt,
            new RecordStanding.Current(),
            new RecordLineage.Replaced(beaten.PersonalRecordId, beaten.AchievedAt));
    }

    /// <summary>
    /// Withdraws this record, because the set behind it was not real.
    /// </summary>
    /// <remarks>
    /// Only from standing, which is what keeps retraction a walk back along the chain one link at
    /// a time: a beaten record is history, so it is never withdrawn, so the record behind a
    /// withdrawn one is always one the athlete beat and restoring it is total.
    /// </remarks>
    public void Retract(DateTime at)
    {
        if (Standing is not RecordStanding.Current)
            throw new InvalidOperationException("Only the standing record can be retracted.");

        Standing = new RecordStanding.Retracted(at);
    }

    /// <summary>
    /// Puts this record back as the current one, after the record that beat it was withdrawn.
    /// </summary>
    public void Restore()
    {
        if (Standing is not RecordStanding.Beaten)
            throw new InvalidOperationException(
                $"Only a record the athlete beat comes back, and this one is {Standing.GetType().Name.ToLowerInvariant()}.");

        Standing = new RecordStanding.Current();
    }
}
