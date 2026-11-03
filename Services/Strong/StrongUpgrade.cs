using System.Diagnostics;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Strong;

/// <summary>Where a session the import already holds stands against the workout it came from.</summary>
/// <remarks>
/// Three states, because a held session that is already current and one the athlete has changed
/// both stay as they are, for opposite reasons, and the report has to say which.
/// </remarks>
internal abstract record HeldSession
{
    private protected HeldSession() { }

    /// <summary>It already reads exactly as the import writes the workout now.</summary>
    public sealed record Current : HeldSession;

    /// <summary>It is exactly what the earlier import wrote for the workout, and becomes <paramref name="Upgraded"/>.</summary>
    public sealed record WrittenTheEarlierWay(
        CompletedTrainingSession Stored, CompletedTrainingSession Upgraded, StrongWorkout Workout) : HeldSession;

    /// <summary>
    /// It matches neither: edited since it was imported, or written under a movement mapping that
    /// has changed. Left as it is, since only the athlete knows which of the two is right.
    /// </summary>
    public sealed record ChangedSinceImport(StrongWorkoutKey Workout) : HeldSession;
}

/// <summary>
/// Bringing sessions the earlier import wrote up to the shape the import writes now.
/// </summary>
/// <remarks>
/// The earlier import had no set kind to write. A warm-up became a freeform set holding its own
/// words ("Warm-up — Bench Press (Barbell): 155 lb × 13"); drop and failure sets were plain lifts,
/// with their kind named in two lines of the session notes. Re-running the import over the same
/// export recognises each session it wrote that way — set by set, and the notes whole — and
/// replaces its sets and notes. Nothing else about the session changes, so a routine it was saved
/// as since keeps it.
/// <para>
/// Records do not change and are not re-detected: a freeform warm-up raised none and a warm-up
/// lift raises none, and drop and failure sets were lifts before and after.
/// </para>
/// </remarks>
internal static class StrongUpgrade
{
    /// <param name="movementIds">The athlete's movement for each exercise, for those they already hold.</param>
    public static HeldSession Classify(
        TrainingSession stored,
        StrongWorkout workout,
        IReadOnlyDictionary<string, Guid> movementIds,
        DateTime now) =>
        stored is CompletedTrainingSession completed && workout.Sets.All(set => movementIds.ContainsKey(set.Exercise))
            ? Compared(completed, workout, StrongSessions.ToSession(workout, completed.UserId, movementIds, now), movementIds)
            : new HeldSession.ChangedSinceImport(workout.Key);

    private static HeldSession Compared(
        CompletedTrainingSession stored,
        StrongWorkout workout,
        CompletedTrainingSession current,
        IReadOnlyDictionary<string, Guid> movementIds) =>
        Reads(stored, [.. current.Sets.Select(Shape)], current.Notes)
            ? new HeldSession.Current()
            : Reads(stored, EarlierShapes(workout, movementIds), EarlierNotes(workout))
                ? new HeldSession.WrittenTheEarlierWay(
                    stored, stored with { Sets = current.Sets, Notes = current.Notes }, workout)
                : new HeldSession.ChangedSinceImport(workout.Key);

    private static bool Reads(CompletedTrainingSession stored, IReadOnlyList<SetShape> sets, string notes) =>
        stored.Notes == notes && stored.Sets.Select(Shape).SequenceEqual(sets);

    // ---- What the earlier import wrote ----

    private static IReadOnlyList<SetShape> EarlierShapes(
        StrongWorkout workout, IReadOnlyDictionary<string, Guid> movementIds) =>
    [
        .. workout.Sets.Select(SetShape (set) => set.Kind is StrongSetKind.WarmUp
            ? new SetShape.Words($"Warm-up — {set.Exercise}: {StrongSessions.Performed(set)}")
            : new SetShape.Lift(
                movementIds[set.Exercise], set.Reps, set.Weight, StrongSessions.ExportUnit, set.Rpe, SetKind.Working))
    ];

    private static string EarlierNotes(StrongWorkout workout) =>
        string.Join("\n", (IEnumerable<string>)
        [
            .. StrongSessions.NoteLines(workout),
            .. KindLine(workout, StrongSetKind.Drop, "Drop sets"),
            .. KindLine(workout, StrongSetKind.Failure, "Taken to failure"),
        ]);

    /// <summary>One line naming every set of a kind, grouped by exercise in the order they were done.</summary>
    private static IEnumerable<string> KindLine(StrongWorkout workout, StrongSetKind kind, string label)
    {
        var groups = workout.Sets
            .Where(set => set.Kind == kind)
            .GroupBy(set => set.Exercise)
            .Select(sets => $"{sets.Key} {string.Join(", ", sets.Select(StrongSessions.Performed))}")
            .ToList();

        return groups.Count == 0 ? [] : [$"{label}: {string.Join("; ", groups)}"];
    }

    // ---- Comparing sets ----

    /// <summary>What two sets must agree on to be the same set the import wrote.</summary>
    /// <remarks>
    /// Not the set records themselves: their facet lists compare by reference, and a lift read back
    /// from storage never equals one built in memory.
    /// </remarks>
    private abstract record SetShape
    {
        private protected SetShape() { }

        public sealed record Lift(Guid MovementDataId, int Reps, double Weight, WeightUnit Unit, Rpe Rpe, SetKind Kind)
            : SetShape;

        public sealed record Words(string Text) : SetShape;

        /// <summary>A shape the import never writes, so it matches nothing the import would have.</summary>
        public sealed record Foreign : SetShape;
    }

    private static SetShape Shape(SetEntry set) => set switch
    {
        LiftSet lift => new SetShape.Lift(lift.MovementDataId, lift.Reps, lift.Weight, lift.Unit, lift.Rpe, lift.Kind),
        FreeformSet { Source: Texted texted } => new SetShape.Words(texted.RawText),
        FreeformSet or DistanceTimeSet or TrailRunSet => new SetShape.Foreign(),
        _ => throw new UnreachableException(),
    };
}
