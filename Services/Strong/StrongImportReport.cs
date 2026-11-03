namespace lionheart.Services.Strong;

/// <summary>How many of each kind of set an import stored, and how many rest rows it set aside.</summary>
/// <remarks>
/// <see cref="Lifts"/> counts every set stored as a lift, the drop and failure sets included;
/// those two are also counted on their own because their kind lives only in the session notes.
/// </remarks>
public sealed record StrongSetTally(int Lifts, int Drops, int Failures, int WarmUps, int RestTimers)
{
    public static StrongSetTally Of(IEnumerable<StrongWorkout> workouts)
    {
        var all = workouts.ToList();
        var sets = all.SelectMany(workout => workout.Sets).ToList();

        return new StrongSetTally(
            Lifts: sets.Count(set => set.Kind is not StrongSetKind.WarmUp),
            Drops: sets.Count(set => set.Kind is StrongSetKind.Drop),
            Failures: sets.Count(set => set.Kind is StrongSetKind.Failure),
            WarmUps: sets.Count(set => set.Kind is StrongSetKind.WarmUp),
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
