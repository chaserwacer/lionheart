using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.DtoGenerator;

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
    public Guid MovementID { get; init; }
    [ForeignKey("TrainingSession")]
    public Guid TrainingSessionID { get; init; }
    public TrainingSession TrainingSession { get; set; } = null!;
    [ForeignKey("MovementBase")]
    public Guid MovementBaseID { get; set; }
    public MovementBase MovementBase { get; set; } = new();
    public required MovementModifier MovementModifier { get; set; }
    public required List<ISetEntry> Sets { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public required int Ordering { get; set; }
    [GenerateDto(typeof(Movement),
             Include = new[] { "TrainingSessionID", "MovementBaseID", "MovementModifier", "Notes" })]
    public partial record CreateMovementRequest;
    [GenerateDto(typeof(Movement),
                 Include = new[] { "MovementID", "MovementBaseID", "MovementModifier", "Notes", "IsCompleted" })]
    public partial record UpdateMovementRequest;
    [GenerateDto(typeof(Movement),
                Exclude = new[] { "TrainingSession" })]
    public partial record MovementDTO;




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
    public string Description { get; set; } = string.Empty;
    public List<MuscleGroup> MuscleGroups { get; set; } = new();
    [GenerateDto(typeof(MovementBase),
                 Include = new[] { "Name", "Description", "MuscleGroups" })]
    public partial record CreateMovementBaseRequest;
    [GenerateDto(typeof(MovementBase),
                 Exclude = new[] { "UserID" })]
    public partial record MovementBaseDTO;
    [GenerateDto(typeof(MovementBase),
                 Include = new[] { "MovementBaseID", "Name", "Description", "MuscleGroups" })]
    public partial record UpdateMovementBaseRequest;
}

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
    [GenerateDto(typeof(Equipment),
                Include = new[] { "Name" })
       ]
    public partial record CreateEquipmentRequest;
}
