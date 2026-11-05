using lionheart.Model.Training.PersonalRecords;

namespace lionheart.Services.Training.PersonalRecords;

/// <summary>
/// How an athlete's records after a rebuild compare with the ones before it.
/// </summary>
/// <remarks>
/// Compared by what each record says rather than by id: a rebuild writes every record afresh, so
/// ids always differ, while the subject, the performance, its date and its place in the chain are
/// what a reader of the records sees.
/// <list type="bullet">
/// <item><see cref="Unchanged"/>: the same performance on the same date, standing the same way,
/// behind the same predecessor.</item>
/// <item><see cref="Restated"/>: the same performance on the same date, now beaten on another date,
/// standing where it was beaten, or behind a different predecessor.</item>
/// <item><see cref="Dropped"/>: no longer set by anything in the history — an older set already
/// beats it, or the session or movement behind it is no longer the athlete's.</item>
/// <item><see cref="Established"/>: a record that did not exist before.</item>
/// </list>
/// </remarks>
public sealed record RecordRebuildReport(int Before, int After, int Unchanged, int Restated, int Dropped, int Established)
{
    public int Changed => Restated + Dropped + Established;

    public static RecordRebuildReport Between(IReadOnlyList<PersonalRecord> before, IReadOnlyList<PersonalRecord> after)
    {
        var was = Placed(before);
        var now = Placed(after);

        var kept = now.Keys.Where(was.ContainsKey).ToList();

        return new RecordRebuildReport(
            Before: before.Count,
            After: after.Count,
            Unchanged: kept.Count(link => was[link] == now[link]),
            Restated: kept.Count(link => was[link] != now[link]),
            Dropped: was.Keys.Count(link => !now.ContainsKey(link)),
            Established: now.Keys.Count(link => !was.ContainsKey(link)));
    }

    /// <summary>Each record by what it holds, with where it sits in its chain.</summary>
    /// <remarks>
    /// A chain is one identity, and the detector writes at most one record of an identity per
    /// session, so a performance and its date identify a link. A predecessor is named the same way,
    /// since its id is exactly what a rebuild changes.
    /// </remarks>
    private static Dictionary<Link, Place> Placed(IReadOnlyList<PersonalRecord> records)
    {
        var byId = records.ToDictionary(record => record.PersonalRecordId);

        return records.ToDictionary(
            Link.Of,
            record => new Place(
                record.Standing,
                record.Lineage switch
                {
                    RecordLineage.Replaced replaced when byId.TryGetValue(replaced.PersonalRecordId, out var behind) =>
                        new Predecessor.Behind(Link.Of(behind)),
                    RecordLineage.Replaced replaced => new Predecessor.Missing(replaced.PersonalRecordId),
                    _ => new Predecessor.None(),
                }));
    }

    private sealed record Link(RecordKey Key, Achievement Achievement, DateTime AchievedAt)
    {
        public static Link Of(PersonalRecord record) => new(record.Key, record.Achievement, record.AchievedAt);
    }

    private sealed record Place(RecordStanding Standing, Predecessor Predecessor);

    /// <summary>What a record replaced, named by content so two rebuilds can be compared.</summary>
    private abstract record Predecessor
    {
        private protected Predecessor() { }

        public sealed record None : Predecessor;

        public sealed record Behind(Link Link) : Predecessor;

        /// <summary>A pointer at a row that is not among the athlete's records: corruption, kept visible.</summary>
        public sealed record Missing(Guid PersonalRecordId) : Predecessor;
    }
}

/// <summary>What happened to the athlete's records on an import run.</summary>
/// <remarks>
/// Two cases because a run that adds no session leaves the records alone rather than rebuilding
/// them to the same answer, and a zeroed comparison would claim the athlete has no records.
/// </remarks>
public abstract record RecordsOutcome
{
    private protected RecordsOutcome() { }

    public sealed record Untouched : RecordsOutcome;

    public sealed record Rebuilt(RecordRebuildReport Report) : RecordsOutcome;
}
