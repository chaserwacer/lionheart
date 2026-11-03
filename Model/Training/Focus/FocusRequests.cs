using lionheart.Functional;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Focus;

// The target crosses the wire as an optional object rather than as its two cases, for the reason
// program membership crosses as an optional id: a flat shape generates a far better TypeScript
// client. Absent is FocusTarget.None, restored here so nothing past the request sees a null.

/// <summary>A target set as a client states it.</summary>
public sealed record LiftTargetRequest(double Weight, WeightUnit Unit, int Reps);

/// <summary>Pins a movement. A pin is always born pinned, and the server stamps when.</summary>
public sealed record PinFocusRequest(
    Guid MovementDataId,
    DateOnly Starts,
    DateOnly Ends,
    int SessionsPerWeek,
    LiftTargetRequest? Target)
{
    public Result<FocusTerms, Problem> ToTerms() => StatedFocusTerms.Of(Starts, Ends, SessionsPerWeek, Target);
}

/// <summary>
/// The whole of a pin's terms, replaced. The movement is not among them — a different movement is
/// a different focus.
/// </summary>
public sealed record ReviseFocusRequest(
    DateOnly Starts,
    DateOnly Ends,
    int SessionsPerWeek,
    LiftTargetRequest? Target)
{
    public Result<FocusTerms, Problem> ToTerms() => StatedFocusTerms.Of(Starts, Ends, SessionsPerWeek, Target);
}

/// <summary>Pinned → unpinned, on the day the athlete names.</summary>
public sealed record UnpinFocusRequest(DateOnly On);

internal static class StatedFocusTerms
{
    public static Result<FocusTerms, Problem> Of(
        DateOnly starts, DateOnly ends, int sessionsPerWeek, LiftTargetRequest? target) =>
        FocusHorizon.Of(starts, ends)
            .Bind(horizon => Target(target)
                .Bind(stated => FocusTerms.Of(horizon, sessionsPerWeek, stated)));

    private static Result<FocusTarget, Problem> Target(LiftTargetRequest? stated) =>
        stated is null
            ? Result<FocusTarget, Problem>.Success(new FocusTarget.None())
            : FocusTarget.Lift.Of(stated.Weight, stated.Unit, stated.Reps);
}
