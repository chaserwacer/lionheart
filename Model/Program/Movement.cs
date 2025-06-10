using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lionheart.Model.Program;

/// <summary>
/// Rerepresents a thing that a user does in a training session.
/// A <see cref="MovementBase"/> is chosen, and a <see cref="MovementModifier"/> can be applied.
/// A <see cref="Movement"/> can have multiple <see cref="SetEntry"/>s, which represent the sets performed during the movement.
/// </summary>
public class Movement
{
    [Key]
    public Guid MovementID { get; init; }

    public Guid TrainingSessionID { get; init; }
    public Guid MovementBaseID { get; init; }
    public Guid? MovementModifierID { get; set; }

    public List<SetEntry> Sets { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    
}

/// <summary>
/// Represents a base movement. A base movement is a general type of exercise.
/// </summary>
public class MovementBase
{
    [Key]
    public Guid MovementBaseID { get; init; }
    public string Name { get; set; } = string.Empty;
    // Other base specific properties can be added here [Ex: muscle groups trained]
}
/// <summary>
/// Represents the modifcation of a <see cref="MovementBase"/>.
/// This can include equipment used, modifications[pause, tempo], the duration of the modification,
/// and any other relevant details that affect the movement.
/// </summary>
public class MovementModifier
{
    public Guid MovementModifierID { get; init; }
    public string Name { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public int Duration { get; set; }
}
/// <summary>
/// Represents a set entry within a <see cref="Movement"/>.
/// A set entry contains the recommended and actual reps, weight, and RPE (Rate of Perceived Exertion).
/// </summary>
public class SetEntry
{
    [Key]
    public Guid SetEntryID { get; init; }
    public Guid MovementID { get; init; }
    public int RecommendedReps { get; set; }
    public double RecommendedWeight { get; set; }
    public int RecommendedRPE { get; set; }
    public WeightUnit WeightUnit { get; set; }
    public int ActualReps { get; set; }
    public double ActualWeight { get; set; }
    public int ActualRPE { get; set; }
}

public enum WeightUnit
    {
        Kilograms,
        Pounds
    }