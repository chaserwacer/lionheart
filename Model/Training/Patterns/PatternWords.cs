using System.Diagnostics;

namespace lionheart.Model.Training.Patterns;

/// <summary>
/// The words a sentence about patterns uses — refusals, the score's own explanations, the text a
/// chat tool hands the model — lower case, as they sit mid-sentence.
/// </summary>
public static class PatternWords
{
    /// <summary>"push and legs": families named together mid-sentence.</summary>
    public static string Listed(IEnumerable<PatternFamily> families) => string.Join(" and ", families.Select(Of));

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

    public static string Of(MuscleGroup muscle) => muscle switch
    {
        MuscleGroup.Chest => "chest",
        MuscleGroup.FrontDelts => "front delts",
        MuscleGroup.SideDelts => "side delts",
        MuscleGroup.RearDelts => "rear delts",
        MuscleGroup.Triceps => "triceps",
        MuscleGroup.Lats => "lats",
        MuscleGroup.UpperBack => "upper back",
        MuscleGroup.Biceps => "biceps",
        MuscleGroup.Forearms => "forearms",
        MuscleGroup.Quads => "quads",
        MuscleGroup.Hamstrings => "hamstrings",
        MuscleGroup.Glutes => "glutes",
        MuscleGroup.Adductors => "adductors",
        MuscleGroup.Calves => "calves",
        MuscleGroup.LowerBack => "lower back",
        MuscleGroup.Abs => "abs",
        _ => throw new UnreachableException(),
    };
}
