using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lionheart.Model.TrainingProgram;

/// <summary>
/// Rerepresents a thing that a user does in a training session.
/// A <see cref="MovementBase"/> is chosen, and a <see cref="MovementModifier"/> can be applied.
/// A <see cref="Movement"/> can have multiple <see cref="SetEntry"/>s, which represent the sets performed during the movement.
/// </summary>
/// <remarks>
/// A prexisiting <see cref="MovementBase"/> is referenced, a new entry in the db will not be created.
/// The <see cref="MovementModifier"/> will be created and will not attempt to reference an existing one.
/// </remarks>
public class Movement
{
    [Key]
    public Guid MovementID { get; init; }
    [ForeignKey("TrainingSession")]
    public Guid TrainingSessionID { get; init; }
    public TrainingSession TrainingSession { get; set; } = null!;
    [ForeignKey("MovementBase")]
    public Guid MovementBaseID { get; set; }
    public MovementBase MovementBase { get; set; } = new();
    public required MovementModifier MovementModifier { get; set; }
    public required IMovementData MovementData { get; set; } 
    public string Notes { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public required int Ordering { get; set; }
    public WeightUnit WeightUnit { get; set; }

    public MovementDTO ToDTO()
    {
        return new MovementDTO
        {
            MovementID = MovementID,
            TrainingSessionID = TrainingSessionID,
            MovementBaseID = MovementBaseID,
            MovementBase = MovementBase,
            MovementModifier = MovementModifier,
            Sets = Sets.Select(set => set.ToDTO()).ToList(),
            Notes = Notes,
            Ordering = Ordering,
            IsCompleted = IsCompleted,
            WeightUnit = WeightUnit
        };
    }

}

public interface IMovementData
{
    [Required]
    public Guid MovementDataID { get; init; }
    [Required]
    public Guid MovementID { get; init; }
    [Required]
    public Movement Movement { get; set; }

}

public enum WeightUnit
{
    Kilograms,
    Pounds
};

public class LiftMovementData : IMovementData
{
    [Key]
    public required Guid MovementDataID { get; init; }
    [ForeignKey("Movement")]
    public required Guid MovementID { get; init; }
    public required Movement Movement { get; set; }
    public required List<SetEntry> Sets { get; set; } = new();
    public required WeightUnit WeightUnit { get; set; }
}

public class DistanceTimeMovementData : IMovementData
{
    [Key]
    public required Guid MovementDataID { get; init; }
    [ForeignKey("Movement")]
    public required Guid MovementID { get; init; }
    public required Movement Movement { get; set; }
    public required List<DTSetEntry> IntervalEntrys { get; set; } = new();

}

public class DTSetEntry
{
    
    public required double RecommendedDistance { get; set; }
    public required double ActualDistance { get; set; }
    public required DistanceUnit DistanceUnit { get; set; }


    public required TimeSpan IntervalDuration { get; set; }  
    
    public required TimeSpan RecommendedDuration { get; set; }
    public required TimeSpan ActualDuration { get; set; }

    public required TimeSpan RecommendedRest { get; set; }
    public required TimeSpan ActualRest { get; set; }
}

public enum DistanceUnit
{
    Meters,
    Yards,
    Miles,
    Kilometers
}


/// <summary>
/// Represents a base movement. A base movement is a general type of exercise.
/// </summary>
public class MovementBase
{
    [Key]
    public Guid MovementBaseID { get; init; }
    public string Name { get; set; } = string.Empty;
    public Guid UserID { get; init; }
}
/// <summary>
/// Represents the modifcation of a <see cref="MovementBase"/>.
/// This tells you how to perform the movement.
/// </summary>
public class MovementModifier
{
    [Required]
    public required string Name { get; set; } = string.Empty;
    [Required]
    public required Guid EquipmentID { get; set; }
    [ForeignKey("EquipmentID")]
    [Required]
    public required Equipment Equipment { get; set; } 
}

public class Equipment
{
    [Key][Required]
    public required Guid EquipmentID { get; init; }
    [Required]
    public required string Name { get; set; } = string.Empty;
    [Required]
    public required Guid UserID { get; init; }
}
/// <summary>
/// Represents a set entry within a <see cref="Movement"/>.
/// A set entry contains the recommended and actual reps, weight, and RPE (Rate of Perceived Exertion).
/// </summary>
public class SetEntry
{
    [Key]
    public Guid SetEntryID { get; init; }
    [ForeignKey("Movement")]
    public Guid MovementID { get; init; }
    public Movement Movement { get; set; } = null!;
    public int RecommendedReps { get; set; }
    public double RecommendedWeight { get; set; }
    public double RecommendedRPE { get; set; }
    public int ActualReps { get; set; }
    public double ActualWeight { get; set; }
    public double ActualRPE { get; set; }

    public SetEntryDTO ToDTO()
    {
        return new SetEntryDTO
        {
            SetEntryID = SetEntryID,
            MovementID = MovementID,
            RecommendedReps = RecommendedReps,
            RecommendedWeight = RecommendedWeight,
            RecommendedRPE = RecommendedRPE,
            ActualReps = ActualReps,
            ActualWeight = ActualWeight,
            ActualRPE = ActualRPE
        };
    }
}

