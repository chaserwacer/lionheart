using System.Diagnostics;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>The movements a session names, whatever its lifecycle: in its sets, or in its prescriptions while planned.</summary>
internal static class SessionMovements
{
    public static IReadOnlyList<SetEntry> SetsOf(TrainingSession session) => session switch
    {
        ActiveTrainingSession active => active.Sets,
        CompletedTrainingSession completed => completed.Sets,
        PlannedTrainingSession or SkippedTrainingSession => [],
        _ => throw new UnreachableException(),
    };

    /// <summary>One id per prescription that names a movement, so a movement prescribed for four sets appears four times.</summary>
    public static IEnumerable<Guid> PrescribedIn(TrainingSession session) =>
        session is PlannedTrainingSession planned
            ? planned.Prescriptions
                .Select(prescription => prescription switch
                {
                    LiftPrescription lift => lift.MovementDataId,
                    DistanceTimePrescription distanceTime => distanceTime.MovementDataId,
                    FreeformPrescription => Guid.Empty,
                    _ => throw new UnreachableException(),
                })
                .Where(movement => movement != Guid.Empty)
            : [];

    public static IEnumerable<Guid> NamedBy(TrainingSession session) =>
        SetsOf(session).ByMovement().Select(movement => movement.Key).Concat(PrescribedIn(session));
}
