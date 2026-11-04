using lionheart.Services.Training.PersonalRecords;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>Whether a tidy is looked at or kept.</summary>
public enum TidyRun
{
    /// <summary>Everything is done inside a transaction and rolled back: the report is what a write would do.</summary>
    DryRun,

    /// <summary>Done and committed, if there was anything to do.</summary>
    Write,
}

/// <summary>How many of each thing an athlete's library holds.</summary>
public sealed record LibraryCounts(int Movements, int Bases, int Equipment, int Modifiers);

/// <summary>What became of the athlete's records.</summary>
public abstract record RecordReplay
{
    private protected RecordReplay() { }

    /// <summary>No movement merged, so no record changed movement and none was replayed.</summary>
    public sealed record NotNeeded : RecordReplay;

    /// <summary>Movements merged, so the whole history was replayed into records again.</summary>
    public sealed record Replayed(RecordRebuildReport Report) : RecordReplay;
}

/// <summary>What one tidy did, or in a dry run would do.</summary>
/// <param name="Unclassified">Bases the library ends with that neither the athlete nor the movement-pattern map classifies.</param>
public sealed record TidyReport(
    LibraryCounts Before,
    LibraryCounts After,
    TidyPlan Plan,
    RecordReplay Records,
    IReadOnlyList<string> Unclassified,
    TidyRun Run)
{
    /// <summary>Whether anything was committed: a write with something to do.</summary>
    public bool Written => Run is TidyRun.Write && !Plan.IsEmpty;
}
