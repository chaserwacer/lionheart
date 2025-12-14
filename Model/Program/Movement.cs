using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.TrainingProgram.SetEntry;

namespace lionheart.Model.TrainingProgram;

/// <summary>
/// Rerepresents a thing that a user does in a training session.
/// A <see cref="MovementBase"/> is chosen, and a <see cref="MovementModifier"/> can be applied.
/// A <see cref="Movement"/> can have multiple <see cref="LiftSetEntry"/>s, which represent the sets performed during the movement.
/// </summary>
/// <remarks>
/// A prexisiting <see cref="MovementBase"/> is referenced, a new entry in the db will not be created.
/// The <see cref="MovementModifier"/> will be created and will not attempt to reference an existing one.
/// </remarks>
public class Movement
{
    [Key]
    public required Guid MovementID { get; init; }
    [ForeignKey("TrainingSession")]
    public required Guid TrainingSessionID { get; init; }
    public TrainingSession TrainingSession { get; set; } = null!;
    [ForeignKey("MovementBase")]
    public required Guid MovementBaseID { get; set; }
    public required MovementBase MovementBase { get; set; }
    public required MovementModifier MovementModifier { get; set; }
    public required List<LiftSetEntry> LiftSets { get; set; }
    public required List<DTSetEntry> DistanceTimeSets { get; set; }
    public required string Notes { get; set; } = string.Empty;
    public required bool IsCompleted { get; set; } = false;
    public required int Ordering { get; set; }

}
public record CreateMovementRequest(
    Guid TrainingSessionID,
    Guid MovementBaseID,
    MovementModifier MovementModifier,
    string Notes
);
public record UpdateMovementRequest(
    Guid MovementID,
    Guid MovementBaseID,
    MovementModifier MovementModifier,
    string Notes,
    bool IsCompleted
);
public record MovementDTO(
    Guid MovementID,
    Guid TrainingSessionID,
    Guid MovementBaseID,
    MovementBase MovementBase,
    MovementModifier MovementModifier,
    List<LiftSetEntryDTO> LiftSets,
    List<DTSetEntryDTO> DistanceTimeSets,
    string Notes,
    bool IsCompleted,
    int Ordering
);


/// <summary>
/// Represents a base movement. A base movement is a general type of exercise.
/// </summary>
public class MovementBase
{
    [Key]
    public required Guid MovementBaseID { get; init; }
    public required string Name { get; set; } = string.Empty;
    public required Guid UserID { get; init; }
    public required string Description { get; set; } = string.Empty;
    public required List<MuscleGroup> MuscleGroups { get; set; } = new();


}

public record CreateMovementBaseRequest(
    string Name,
    string Description,
    List<MuscleGroup> MuscleGroups
);
public record MovementBaseDTO(
    Guid MovementBaseID,
    string Name,
    string Description,
    List<MuscleGroup> MuscleGroups
);
public record UpdateMovementBaseRequest(
    Guid MovementBaseID,
    string Name,
    string Description,
    List<MuscleGroup> MuscleGroups
);

public enum MuscleGroup
{
    Chest,
    Back,
    Hamstrings,
    Quadriceps,
    SideDeltoids,
    FrontDeltoids,
    RearDeltoids,
    Biceps,
    Triceps,
    Calves,
    Abs,
    Glutes,
    Forearms,
    Traps,
    Lats,
    LowerBack,
    Neck
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
    [Key]
    [Required]
    public required Guid EquipmentID { get; init; }
    [Required]
    public required string Name { get; set; } = string.Empty;
    [Required]
    public required Guid UserID { get; init; }
    [Required]
    public bool Enabled { get; set; } = true;

}
public record CreateEquipmentRequest(
    string Name
);
public record EquipmentDTO(
    Guid EquipmentID,
    string Name,
    bool Enabled
);
public record UpdateEquipmentRequest(
    Guid EquipmentID,
    string Name,
    bool Enabled
);