using System.Diagnostics;
using lionheart.Functional;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>
/// A movement merge as it reaches each thing that names a movement: the merged-away id swapped for
/// the one kept, everything else as it was.
/// </summary>
/// <remarks>
/// Pins follow through <c>FocusPin.FollowMerge</c>, records by being replayed, and previous
/// performance is read from the sets, so it follows them.
/// </remarks>
public static class MovementMerge
{
    /// <summary>Whether a session names any of the movements that go.</summary>
    public static bool Names(TrainingSession session, IReadOnlyDictionary<Guid, Guid> merges) =>
        SessionMovements.NamedBy(session).Any(merges.ContainsKey);

    /// <summary>The session with every set and prescription on a merged movement moved to the one kept.</summary>
    public static TrainingSession Repointed(TrainingSession session, IReadOnlyDictionary<Guid, Guid> merges) => session switch
    {
        PlannedTrainingSession planned => planned with
        {
            Prescriptions = [.. planned.Prescriptions.Select(prescription => Repointed(prescription, merges))],
        },
        ActiveTrainingSession active => active with { Sets = [.. active.Sets.Select(set => Repointed(set, merges))] },
        CompletedTrainingSession completed => completed with { Sets = [.. completed.Sets.Select(set => Repointed(set, merges))] },
        SkippedTrainingSession skipped => skipped,
        _ => throw new UnreachableException(),
    };

    /// <summary>
    /// The routine's content with its movements moved, or a refusal when a routine holds two
    /// movements that become one, since which of their set counts it means is the athlete's call.
    /// </summary>
    public static Result<RoutineContent, Problem> Repointed(Routine routine, IReadOnlyDictionary<Guid, Guid> merges) =>
        RoutineContent.Of(
                routine.Name,
                routine.Notes,
                [.. routine.Movements.Select(movement => movement with { MovementDataId = Kept(movement.MovementDataId, merges) })])
            .MapError(Problem (_) => new Problem.Conflict(
                $"routine '{routine.Name}' holds two movements that merge into one; take one out in the app first."));

    /// <summary>The injury note's movements moved, each named once.</summary>
    public static List<Guid> Repointed(InjuryEvent note, IReadOnlyDictionary<Guid, Guid> merges) =>
        [.. note.MovementIDs.Select(movement => Kept(movement, merges)).Distinct()];

    private static Guid Kept(Guid movement, IReadOnlyDictionary<Guid, Guid> merges) => merges.GetValueOrDefault(movement, movement);

    private static SetEntry Repointed(SetEntry set, IReadOnlyDictionary<Guid, Guid> merges) => set switch
    {
        LiftSet lift => lift with { MovementDataId = Kept(lift.MovementDataId, merges) },
        DistanceTimeSet distanceTime => distanceTime with { MovementDataId = Kept(distanceTime.MovementDataId, merges) },
        TrailRunSet or EnduranceSet or FreeformSet => set,
        _ => throw new UnreachableException(),
    };

    private static SetPrescription Repointed(SetPrescription prescription, IReadOnlyDictionary<Guid, Guid> merges) => prescription switch
    {
        LiftPrescription lift => lift with { MovementDataId = Kept(lift.MovementDataId, merges) },
        DistanceTimePrescription distanceTime => distanceTime with { MovementDataId = Kept(distanceTime.MovementDataId, merges) },
        FreeformPrescription => prescription,
        _ => throw new UnreachableException(),
    };
}
