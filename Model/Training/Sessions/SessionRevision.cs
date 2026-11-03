using lionheart.Model.Analysis;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Sessions;

/// <summary>
/// A completed session corrected after the fact: its sets, when it happened and its notes restated
/// whole, and whether the correction reaches the athlete's records.
/// </summary>
/// <remarks>
/// A record is a fact about a set and the instant its session began, so a correction that changes
/// either can change which records stand — on this session and on every later one judged against
/// it. <see cref="AffectsRecords"/> says so, and the caller re-detects the whole history when it
/// does. A notes-only correction never does.
/// <para>
/// A set the correction leaves as it was keeps the source and facets it was stored with, so the
/// words a texted set was logged in survive an edit to the set beside it. A set that changes is
/// restated as the athlete now states it, which is <see cref="Formed"/>. A correction that reaches
/// the records also drops the session's decoration: a summary of sets that are no longer the sets,
/// or of a day it no longer happened on, would describe a different session.
/// </para>
/// </remarks>
public sealed record SessionRevision(CompletedTrainingSession Revised, bool AffectsRecords)
{
    private static readonly IReadOnlyList<Facet> NoFacets = [];

    public static SessionRevision Of(
        CompletedTrainingSession current, IReadOnlyList<SetEntry> sets, Period period, string notes)
    {
        var kept = KeepingUnchanged(current.Sets, sets);
        var affectsRecords = !SameMeasurements(current.Sets, kept)
            || current.Period.StartTimeUtc != period.StartTimeUtc;

        return new SessionRevision(
            current with
            {
                Sets = kept,
                Period = period,
                Notes = notes,
                Decoration = affectsRecords ? new NotDecorated() : current.Decoration,
            },
            affectsRecords);
    }

    /// <summary>
    /// The revised sets, each one that matches a stored set's measurement replaced by that stored
    /// set — matched in order, each stored set used once, so two identical sets keep two sources.
    /// </summary>
    private static IReadOnlyList<SetEntry> KeepingUnchanged(IReadOnlyList<SetEntry> stored, IReadOnlyList<SetEntry> revised)
    {
        var unused = stored.ToList();
        return
        [
            .. revised.Select(set =>
            {
                var match = unused.FirstOrDefault(candidate => SameMeasurement(candidate, set));
                if (match is null) return set;
                unused.Remove(match);
                return match;
            }),
        ];
    }

    private static bool SameMeasurements(IReadOnlyList<SetEntry> before, IReadOnlyList<SetEntry> after) =>
        before.Count == after.Count && before.Zip(after).All(pair => SameMeasurement(pair.First, pair.Second));

    /// <summary>
    /// Whether two sets record the same thing done. A freeform set's measurement is its words;
    /// every other shape's is its members, compared with how it was logged set aside.
    /// </summary>
    private static bool SameMeasurement(SetEntry stored, SetEntry revised) => (stored, revised) switch
    {
        (FreeformSet a, FreeformSet b) => Words(a) == Words(b),
        (FreeformSet, _) or (_, FreeformSet) => false,
        _ => Measured(stored) == Measured(revised),
    };

    private static SetEntry Measured(SetEntry set) => set with { Source = new Formed(), Facets = NoFacets };

    private static string Words(SetEntry set) => set.Source is Texted texted ? texted.RawText : string.Empty;
}
