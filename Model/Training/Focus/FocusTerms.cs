using System.Text.Json.Serialization;
using lionheart.Functional;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Focus;

/// <summary>
/// What a focus pin asks of the athlete: over which days, how many sessions a week, and to what
/// target. Revised as one, since the three are set together and read together.
/// </summary>
/// <remarks>
/// Every constructor here that guards a range is private, so terms from a client have passed the
/// factories. Storage reads them back through the same constructors without re-checking, as it
/// does every other stored JSON shape: a stored row is one this application wrote.
/// </remarks>
public sealed record FocusTerms
{
    public const int FewestSessionsPerWeek = 1;
    public const int MostSessionsPerWeek = 7;

    [JsonConstructor]
    private FocusTerms(FocusHorizon horizon, int sessionsPerWeek, FocusTarget target)
    {
        Horizon = horizon;
        SessionsPerWeek = sessionsPerWeek;
        Target = target;
    }

    public FocusHorizon Horizon { get; }

    /// <summary>How often a week the pin asks for the movement to be trained.</summary>
    public int SessionsPerWeek { get; }

    public FocusTarget Target { get; }

    public static Result<FocusTerms, Problem> Of(FocusHorizon horizon, int sessionsPerWeek, FocusTarget target) =>
        sessionsPerWeek is >= FewestSessionsPerWeek and <= MostSessionsPerWeek
            ? Result<FocusTerms, Problem>.Success(new FocusTerms(horizon, sessionsPerWeek, target))
            : Result<FocusTerms, Problem>.Failure(new Problem.Invalid(
                nameof(SessionsPerWeek),
                $"A focus asks for {FewestSessionsPerWeek} to {MostSessionsPerWeek} sessions a week."));
}

/// <summary>The days a focus pin covers, first and last both included.</summary>
/// <remarks>
/// Whether the horizon has begun or passed is a reading against today, not a stored state, so it
/// is not modelled here: a pin keeps one status whichever side of its horizon the calendar is on.
/// </remarks>
public sealed record FocusHorizon
{
    [JsonConstructor]
    private FocusHorizon(DateOnly starts, DateOnly ends)
    {
        Starts = starts;
        Ends = ends;
    }

    public DateOnly Starts { get; }

    public DateOnly Ends { get; }

    public static Result<FocusHorizon, Problem> Of(DateOnly starts, DateOnly ends) =>
        ends < starts
            ? Result<FocusHorizon, Problem>.Failure(
                new Problem.Invalid(nameof(Ends), "The focus ends before it starts."))
            : Result<FocusHorizon, Problem>.Success(new FocusHorizon(starts, ends));
}

/// <summary>
/// What a focus pin is aiming at, if anything beyond training the movement often.
/// </summary>
/// <remarks>
/// A case rather than a nullable: a pin with no target is a pin about frequency alone, which is a
/// fact about the pin and not a missing field. Whether the target was met is read against the
/// training log by the client and never stored, so a target carries no outcome.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(None), "none")]
[JsonDerivedType(typeof(Lift), "lift")]
public abstract record FocusTarget
{
    private protected FocusTarget() { }

    public sealed record None : FocusTarget;

    /// <summary>A set the athlete means to perform: a weight, in its unit, for a rep count.</summary>
    /// <remarks>Its unit is kept as stated: weights never convert anywhere in this domain.</remarks>
    public sealed record Lift : FocusTarget
    {
        public const int FewestReps = 1;
        public const int MostReps = 30;

        [JsonConstructor]
        private Lift(double weight, WeightUnit unit, int reps)
        {
            Weight = weight;
            Unit = unit;
            Reps = reps;
        }

        public double Weight { get; }

        public WeightUnit Unit { get; }

        public int Reps { get; }

        /// <remarks>
        /// Zero is a weight a target can name — added load on a bodyweight movement starts there —
        /// so only a negative or non-finite one is refused.
        /// </remarks>
        public static Result<FocusTarget, Problem> Of(double weight, WeightUnit unit, int reps) =>
            Stated.ClosedEnum(unit, "Target.Unit")
                .Ensure(
                    _ => double.IsFinite(weight) && weight >= 0,
                    _ => new Problem.Invalid("Target.Weight", "A target weight is a finite number of zero or more."))
                .Ensure(
                    _ => reps is >= FewestReps and <= MostReps,
                    _ => new Problem.Invalid("Target.Reps", $"A target is {FewestReps} to {MostReps} reps."))
                .Map(FocusTarget (stated) => new Lift(weight, stated, reps));
    }
}
