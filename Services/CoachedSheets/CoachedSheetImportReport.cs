using lionheart.Services.Import;
using lionheart.Services.Training.PersonalRecords;

namespace lionheart.Services.CoachedSheets;

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

/// <summary>Rows set aside as never lifted, for one reason.</summary>
public sealed record NotPerformedTally(NotPerformedKind Kind, int Rows, int Sets);

/// <summary>What one run of the coached-programme import did.</summary>
/// <remarks>
/// The log and its decisions are described whole, so a second run reports the same history, all
/// of it already imported, and zeroes for what it created.
/// </remarks>
public sealed record CoachedSheetImportReport(
    int SheetSetsInLog,
    int StrongRowsSetAside,
    IReadOnlyList<NotPerformedTally> NotPerformed,
    int ClustersSplit,
    int SessionsWithNothingPerformed,
    int SessionsInLog,
    int AlreadyImported,
    int SessionsCreated,
    int SetsCreated,
    IReadOnlyList<string> ProgramsCreated,
    CatalogueGrowth Catalogue,
    IReadOnlyList<MovementMapping> UnsureMappings,
    RecordsOutcome Records);
