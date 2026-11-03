namespace lionheart.Model.Training.Routines;

// Purpose-built commands (docs/api-and-composition.md §1). Neither carries a RoutineId, a UserId or
// a creation time: the server owns all three. A routine movement carries nothing the server owns,
// so the request states it in the domain's own shape rather than in a mirror of it.

/// <summary>Defines a routine, or restates one whole: <c>PUT</c> replaces every member this names.</summary>
public sealed record DefineRoutineRequest(
    string Name,
    string Notes,
    IReadOnlyList<RoutineMovement> Movements);

/// <summary>
/// Saves a completed session as a routine — the athlete saying "that was my upper day". The
/// movements and their working-set counts are read off the session.
/// </summary>
public sealed record RoutineFromSessionRequest(string Name);
