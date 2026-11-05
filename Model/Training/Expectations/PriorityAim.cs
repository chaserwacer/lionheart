using System.Text.Json.Serialization;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Expectations;

/// <summary>A family's priority lift, its next top set, and how often the week asks for it.</summary>
/// <param name="Reasons">Why the aim is a step, a hold or a first top set, and how far a pinned target is, in words.</param>
public sealed record PriorityAim(
    PatternFamily Family,
    Guid MovementDataId,
    string Name,
    LiftAim Aim,
    PriorityFrequency Frequency,
    IReadOnlyList<string> Reasons);

/// <summary>The next top set of a priority lift.</summary>
/// <remarks>
/// A hold is its own case rather than a step of nothing: it aims at last time's set, has no new
/// best to pass, and says the signals asked for it.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Beat), "beat")]
[JsonDerivedType(typeof(Hold), "hold")]
[JsonDerivedType(typeof(Establish), "establish")]
public abstract record LiftAim
{
    private protected LiftAim() { }

    /// <summary>
    /// The last session's best set with one step more — five pounds or 2.5 kilograms on the bar,
    /// or one more rep where the load is bodyweight — and the twelve weeks' best estimate a new
    /// best has to pass.
    /// </summary>
    /// <param name="Load">The load to move, in the lift's unit: the plate, or the system load where <paramref name="OnSystemLoad"/>.</param>
    /// <param name="EstimatedOneRepMax">The Epley estimate of that set.</param>
    /// <param name="BestToBeat">The best estimate of the last twelve weeks and the window.</param>
    public sealed record Beat(
        double Load, int Reps, WeightUnit Unit, double EstimatedOneRepMax, double BestToBeat, bool OnSystemLoad) : LiftAim;

    /// <summary>
    /// The last session's best set again: tiredness, a hard last session or a low going-in rating
    /// asked for the weight to be held rather than added to.
    /// </summary>
    public sealed record Hold(double Load, int Reps, WeightUnit Unit, double EstimatedOneRepMax, bool OnSystemLoad) : LiftAim;

    /// <summary>No estimate in the last twelve weeks to build on: work up to a top set and set one.</summary>
    public sealed record Establish : LiftAim;
}

/// <summary>How often the week asks for a priority lift: what its pin says, or nothing beyond training it.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Pinned), "pinned")]
[JsonDerivedType(typeof(NotPinned), "notPinned")]
public abstract record PriorityFrequency
{
    private protected PriorityFrequency() { }

    /// <summary>The pin's sessions a week, against the sessions in the window that trained the lift.</summary>
    public sealed record Pinned(Expectation Sessions) : PriorityFrequency;

    /// <summary>No pin asks a frequency; the sessions in the window that trained the lift.</summary>
    public sealed record NotPinned(int Sessions) : PriorityFrequency;
}
