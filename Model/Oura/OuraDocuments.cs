using lionheart.Model.Analysis;
using lionheart.Model.Oura.Readings;

namespace lionheart.Model.Oura;

/// <summary>
/// An Oura document that happened at a point or across a span of clock time.
/// </summary>
/// <remarks>
/// Exists so the persistence layer can stamp the queryable StartTime column for every Oura event
/// without naming each collection: the generic base below cannot be pattern-matched, and a list of
/// concrete types in that switch would silently omit the next collection added.
/// </remarks>
public interface IOuraEvent
{
    Period Period { get; }
}

/// <summary>
/// One document as Oura issued it: who it belongs to, the day Oura filed it under, and what it
/// measured.
/// </summary>
/// <remarks>
/// These are entities rather than the immutable records the training domain is built from, and the
/// difference is the update path. Oura revises a document after it first publishes one — a day's
/// score is provisional for hours, and its sleep-staging algorithm reprocesses nights already
/// synced — so a re-sync has to replace a payload in place. An entity with identity and a
/// replaceable payload is what that needs; an immutable record would mean deleting and
/// re-inserting a row inside one transaction for every revision.
/// <para>
/// <see cref="OuraId"/> is Oura's own document id and this row's primary key, which is what makes
/// both the importer and the live sync idempotent without a second key or a sync-date ledger:
/// re-reading a document Oura has not changed applies a payload the change tracker compares as
/// equal, and writes nothing.
/// </para>
/// </remarks>
public abstract class OuraDocument<TReading> where TReading : class
{
    /// <remarks>For EF materialization, which writes the members below directly.</remarks>
    private protected OuraDocument() { }

    private protected OuraDocument(Guid ouraId, Guid userId, DateOnly day, TReading reading)
    {
        OuraId = ouraId;
        UserId = userId;
        Day = day;
        Reading = reading;
    }

    /// <summary>Oura's own id for this document. The primary key.</summary>
    public Guid OuraId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>
    /// The calendar day Oura files this document under.
    /// </summary>
    /// <remarks>
    /// Oura's own attribution, not a day derived from the clock time: a sleep period beginning
    /// before midnight is filed under the day the athlete woke, which is the day a reader asking
    /// "how did he sleep on the 8th" means. For an event this is therefore a second, independent
    /// fact from its <see cref="OuraEvent{TReading}.Period"/>, and the two disagree on roughly a
    /// quarter of the archived sleep records. Both are kept.
    /// </remarks>
    public DateOnly Day { get; private set; }

    /// <summary>What Oura measured. Replaced wholesale when Oura revises the document.</summary>
    public TReading Reading { get; private set; } = null!;

    /// <summary>
    /// Takes Oura's latest version of this document.
    /// </summary>
    /// <remarks>
    /// The day is replaced along with the payload. Oura files a document under a day it can
    /// revise — a sleep period's attribution moves if it restages the night — and replacing only
    /// the payload would leave the indexed <see cref="Day"/> disagreeing with the period inside it.
    /// </remarks>
    public void Apply(DateOnly day, TReading reading)
    {
        Day = day;
        Reading = reading;
    }
}

/// <summary>
/// An Oura document for something that happened over a span of clock time rather than for a day
/// as a whole: a sleep period, a workout, a session, a tag.
/// </summary>
/// <remarks>
/// When it happened is a <see cref="Analysis.Period"/>, the same temporal shape the training
/// domain uses, so an Oura event and a training session order on one timeline and the stamped
/// StartTime column means the same thing in both tables.
/// <para>
/// It is read off the reading rather than stored beside it. The span is part of what Oura reported
/// and is replaced with the rest of the payload when Oura revises a record, so a second copy on
/// the row would be one more thing a revision could leave stale. The persistence layer ignores
/// this member and stamps the indexed StartTime column from it instead.
/// </para>
/// </remarks>
public abstract class OuraEvent<TReading> : OuraDocument<TReading>, IOuraEvent where TReading : class
{
    private protected OuraEvent() { }

    private protected OuraEvent(Guid ouraId, Guid userId, DateOnly day, TReading reading)
        : base(ouraId, userId, day, reading) { }

    public abstract Period Period { get; }
}

public sealed class OuraDailyActivity : OuraDocument<DailyActivityReading>
{
    private OuraDailyActivity() { }

    public OuraDailyActivity(Guid ouraId, Guid userId, DateOnly day, DailyActivityReading reading)
        : base(ouraId, userId, day, reading) { }
}

public sealed class OuraDailyReadiness : OuraDocument<DailyReadinessReading>
{
    private OuraDailyReadiness() { }

    public OuraDailyReadiness(Guid ouraId, Guid userId, DateOnly day, DailyReadinessReading reading)
        : base(ouraId, userId, day, reading) { }
}

public sealed class OuraDailySleep : OuraDocument<DailySleepReading>
{
    private OuraDailySleep() { }

    public OuraDailySleep(Guid ouraId, Guid userId, DateOnly day, DailySleepReading reading)
        : base(ouraId, userId, day, reading) { }
}

public sealed class OuraDailySpo2 : OuraDocument<DailySpo2Reading>
{
    private OuraDailySpo2() { }

    public OuraDailySpo2(Guid ouraId, Guid userId, DateOnly day, DailySpo2Reading reading)
        : base(ouraId, userId, day, reading) { }
}

public sealed class OuraDailyStress : OuraDocument<DailyStressReading>
{
    private OuraDailyStress() { }

    public OuraDailyStress(Guid ouraId, Guid userId, DateOnly day, DailyStressReading reading)
        : base(ouraId, userId, day, reading) { }
}

public sealed class OuraDailyResilience : OuraDocument<DailyResilienceReading>
{
    private OuraDailyResilience() { }

    public OuraDailyResilience(Guid ouraId, Guid userId, DateOnly day, DailyResilienceReading reading)
        : base(ouraId, userId, day, reading) { }
}

public sealed class OuraSleepPeriod : OuraEvent<SleepPeriodReading>
{
    private OuraSleepPeriod() { }

    public OuraSleepPeriod(Guid ouraId, Guid userId, DateOnly day, SleepPeriodReading reading)
        : base(ouraId, userId, day, reading) { }

    public override Period Period => Reading.Period;
}

public sealed class OuraWorkout : OuraEvent<WorkoutReading>
{
    private OuraWorkout() { }

    public OuraWorkout(Guid ouraId, Guid userId, DateOnly day, WorkoutReading reading)
        : base(ouraId, userId, day, reading) { }

    public override Period Period => Reading.Period;
}

public sealed class OuraSession : OuraEvent<SessionReading>
{
    private OuraSession() { }

    public OuraSession(Guid ouraId, Guid userId, DateOnly day, SessionReading reading)
        : base(ouraId, userId, day, reading) { }

    public override Period Period => Reading.Period;
}

public sealed class OuraTag : OuraEvent<TagReading>
{
    private OuraTag() { }

    public OuraTag(Guid ouraId, Guid userId, DateOnly day, TagReading reading)
        : base(ouraId, userId, day, reading) { }

    public override Period Period => Reading.Period;
}
