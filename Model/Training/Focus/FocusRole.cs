using System.Text.Json.Serialization;
using lionheart.Functional;
using lionheart.Model.Training.Patterns;

namespace lionheart.Model.Training.Focus;

/// <summary>
/// What a pin stands for: the one lift a family is built around this block, or a movement
/// emphasised on its own.
/// </summary>
/// <remarks>
/// Fixed for the pin's life, like its movement: making a different lift the push priority is a
/// different pin, and the old one stays as the record of the block it led. A family has at most one
/// pinned priority at a time; pinning a new one lets the old one go (<c>FocusPinService</c>).
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Emphasis), "emphasis")]
[JsonDerivedType(typeof(Priority), "priority")]
public abstract record FocusRole
{
    private protected FocusRole() { }

    /// <summary>A movement pinned for emphasis, standing for no family.</summary>
    public sealed record Emphasis : FocusRole;

    /// <summary>The family's priority lift for the horizon.</summary>
    public sealed record Priority : FocusRole
    {
        [JsonConstructor]
        private Priority(PatternFamily family) => Family = family;

        public PatternFamily Family { get; }

        /// <summary>A priority for push, pull or legs. Core is not a family a lift is chosen for.</summary>
        public static Result<FocusRole, Problem> For(PatternFamily family) =>
            MovementPatterns.Judged.Contains(family)
                ? Result<FocusRole, Problem>.Success(new Priority(family))
                : Result<FocusRole, Problem>.Failure(new Problem.Invalid(
                    nameof(PinFocusRequest.Priority), "A priority is chosen for push, pull or legs."));
    }

    public bool IsPriorityFor(PatternFamily family) => this is Priority priority && priority.Family == family;

    /// <summary>
    /// Whether a movement can stand for this role: a priority's lift has to train its family. An
    /// unclassified movement is taken at the athlete's word, and an emphasis stands for no family.
    /// </summary>
    /// <param name="movementName">How the refusal names the movement.</param>
    public Result<FocusRole, Problem> Admits(string movementName, ClassificationStanding standing) =>
        (this, standing) switch
        {
            (Priority priority, ClassificationStanding.Classified { Classification: var trains })
                when trains.Family != priority.Family =>
                Result<FocusRole, Problem>.Failure(new Problem.Invalid(
                    nameof(PinFocusRequest.Priority),
                    $"{movementName} trains {PatternWords.Of(trains.Family)}, not {PatternWords.Of(priority.Family)}.")),
            _ => Result<FocusRole, Problem>.Success(this),
        };
}
