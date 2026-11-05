using lionheart.Services.Import;
using lionheart.Services.Training.PersonalRecords;

namespace lionheart.Services.CoachedSheets;

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
