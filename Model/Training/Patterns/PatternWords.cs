using System.Diagnostics;

namespace lionheart.Model.Training.Patterns;

/// <summary>
/// The words a sentence about patterns uses — refusals, the score's own explanations, the text a
/// chat tool hands the model — lower case, as they sit mid-sentence.
/// </summary>
public static class PatternWords
{
    public static string Of(PatternFamily family) => family switch
    {
        PatternFamily.Push => "push",
        PatternFamily.Pull => "pull",
        PatternFamily.Legs => "legs",
        PatternFamily.Core => "core",
        _ => throw new UnreachableException(),
    };

    public static string Of(MovementPattern pattern) => pattern switch
    {
        MovementPattern.HorizontalPush => "horizontal push",
        MovementPattern.VerticalPush => "vertical push",
        MovementPattern.HorizontalPull => "horizontal pull",
        MovementPattern.VerticalPull => "vertical pull",
        MovementPattern.Squat => "squat",
        MovementPattern.Hinge => "hinge",
        MovementPattern.SingleLeg => "single-leg",
        MovementPattern.Isolation => "isolation",
        MovementPattern.Other => "other",
        _ => throw new UnreachableException(),
    };
}
