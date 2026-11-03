using lionheart.Services.Import;

namespace lionheart.Services.Strong;

/// <summary>How many sets of each kind a batch of workouts holds, and how many rest rows it set aside.</summary>
/// <remarks>Every set is stored as a lift, so the four kinds together are the lifts.</remarks>
public sealed record StrongSetTally(int Working, int WarmUps, int Drops, int Failures, int RestTimers)
{
    public int Lifts => Working + WarmUps + Drops + Failures;

    public static StrongSetTally Of(IEnumerable<StrongWorkout> workouts)
    {
        var all = workouts.ToList();
        var sets = all.SelectMany(workout => workout.Sets).ToList();

        return new StrongSetTally(
            Working: sets.Count(set => set.Kind is StrongSetKind.Working),
            WarmUps: sets.Count(set => set.Kind is StrongSetKind.WarmUp),
            Drops: sets.Count(set => set.Kind is StrongSetKind.Drop),
            Failures: sets.Count(set => set.Kind is StrongSetKind.Failure),
            RestTimers: all.Sum(workout => workout.RestTimers));
    }
}

/// <summary>What one run of the Strong import did.</summary>
/// <remarks>
/// Everything is counted over the workouts this run imported, not the whole export, so a second
/// run over the same export reports the export's size, all of it already imported, and zeroes.
/// <see cref="UnsureMappings"/> are the judgement calls in the movement map that this run applied,
/// listed so that none of them reaches the database unannounced.
/// </remarks>
public sealed record StrongImportReport(
    int WorkoutsInExport,
    int AlreadyImported,
    int SessionsCreated,
    StrongSetTally Sets,
    CatalogueGrowth Catalogue,
    int RecordsEstablished,
    IReadOnlyList<MovementMapping> UnsureMappings);
