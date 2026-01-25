using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Base definition of a physical movement.
/// Contains associated <see cref="Description"/> and collection of <see cref="TrainedMuscles"/>.
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

public class MuscleGroup
{
    [Key]
    public required Guid MuscleGroupID { get; init; }
    public required string Name { get; set; } = string.Empty;
}


public record CreateMovementBaseRequest(
    [Required]string Name,
    [Required(AllowEmptyStrings = true)]string Description,
    [Required]List<MuscleGroup> MuscleGroups    
);
public record MovementBaseDTO(
    Guid MovementBaseID,
    string Name,
    string Description,
    List<MuscleGroup> MuscleGroups
);
public record UpdateMovementBaseRequest(
    [Required]Guid MovementBaseID,
    [Required]string Name,
    [Required(AllowEmptyStrings = true)]string Description,
    [Required]List<MuscleGroup> MuscleGroups
);