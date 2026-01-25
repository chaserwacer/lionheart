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
    public required List<TrainedMuscle> TrainedMuscles { get; set; } = new();

}
/// <summary>
/// Representation of a muscle group trained by a movement.
/// </summary>
public class TrainedMuscle
{
    public required Guid MuscleGroupID { get; set; }
    [Range(0, 1)]

    /// <summary>
    /// Percentage contribution of this muscle group during the movement.
    /// Generally expressed as either 1 (for primary muscle) or 0.5 (for secondary muscle).
    /// </summary>
    public double ContributionPercentage { get; set; }

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
    [Required]List<TrainedMuscle> TrainedMuscles
);
public record MovementBaseDTO(
    Guid MovementBaseID,
    string Name,
    string Description,
    List<TrainedMuscle> TrainedMuscles
);
public record UpdateMovementBaseRequest(
    [Required]Guid MovementBaseID,
    [Required]string Name,
    [Required(AllowEmptyStrings = true)]string Description,
    [Required]List<TrainedMuscle> TrainedMuscles
);