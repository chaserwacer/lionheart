using lionheart.Model.Training.Sessions;

namespace lionheart.Model.Training.Routines;

/// <summary>A routine on the wire, with the last time it was trained.</summary>
/// <remarks>
/// <see cref="LastSession"/> is declared as the session envelope rather than as
/// <see cref="CompletedSessionView"/> so it carries its <c>$type</c>: the serializer writes the
/// discriminator only through the polymorphic base, and a client decodes one session shape, not
/// two. It is always the completed case, or absent when the routine has never been trained.
/// </remarks>
public sealed record RoutineView(
    Guid RoutineId,
    string Name,
    string Notes,
    IReadOnlyList<RoutineMovement> Movements,
    DateTime CreatedAt,
    TrainingSessionView? LastSession)
{
    public static RoutineView Of(Routine routine, CompletedTrainingSession? lastSession) => new(
        routine.RoutineId,
        routine.Name,
        routine.Notes,
        routine.Movements,
        routine.CreatedAt,
        lastSession is null ? null : TrainingSessionView.Of(lastSession));
}
