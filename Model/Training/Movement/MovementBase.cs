using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

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
    public required List<TrainedMuscle> TrainedMuscles { get; set; } = new();

}
[Owned]
public class TrainedMuscle
{
    public required Guid MuscleGroupID { get; set; }
    [Range(0, 1)]
    public double ContributionPercentage { get; set; }

}

public class MuscleGroup
{
    [Key]
    public required Guid MuscleGroupID { get; init; }
    public required string Name { get; set; } = string.Empty;
}



public record CreateMovementBaseRequest(
    string Name,
    string Description,
    List<TrainedMuscle> TrainedMuscles
);
public record MovementBaseDTO(
    Guid MovementBaseID,
    string Name,
    string Description,
    List<TrainedMuscle> TrainedMuscles
);
public record UpdateMovementBaseRequest(
    Guid MovementBaseID,
    string Name,
    string Description,
    List<TrainedMuscle> TrainedMuscles
);