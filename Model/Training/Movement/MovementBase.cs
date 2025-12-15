using System.ComponentModel.DataAnnotations;

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