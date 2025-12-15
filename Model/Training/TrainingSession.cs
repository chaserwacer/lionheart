using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.DTOs;

namespace lionheart.Model.Training
{
    public class TrainingSession
    {
        [Key]
        [Required]
        public required Guid TrainingSessionID { get; init; }   
        [ForeignKey(nameof(TrainingProgram))]
        public Guid? TrainingProgramID { get; set; }
        public TrainingProgram? TrainingProgram { get; set; } // A training session may be standalone, or part of a program
        public required DateOnly Date { get; set; }
        public required TrainingSessionStatus Status { get; set; } = TrainingSessionStatus.Planned;
        public required List<Movement> Movements { get; set; } = [];
        public required DateTime CreationTime { get; init; }
        public required string Notes { get; set; } = string.Empty;
        public PerceivedEffortRatings? PerceivedEffortRatings { get; set; }
    }
    public record TrainingSessionDTO(
        [Required]
        Guid TrainingSessionID,
        Guid? TrainingProgramID,
        [Required]
        DateOnly Date,
        [Required]
        TrainingSessionStatus Status,
        [Required]
        List<MovementDTO> Movements,
        [Required]
        DateTime CreationTime,
        [Required]
        string Notes,
        PerceivedEffortRatings? PerceivedEffortRatings
    );
    public record CreateTrainingSessionRequest(
        DateOnly Date,
        Guid? TrainingProgramID,
        string Notes,
        PerceivedEffortRatings? PerceivedEffortRatings
    );
    public record UpdateTrainingSessionRequest(
        Guid TrainingSessionID,
        Guid? TrainingProgramID,
        DateOnly Date,
        TrainingSessionStatus Status,
        string Notes,
        PerceivedEffortRatings? PerceivedEffortRatings
    );
    public enum TrainingSessionStatus
    {
        Planned,
        InProgress,
        Completed,
        Skipped,
        AIModified
    }
    public class PerceivedEffortRatings
    {
        public int? AccumulatedFatigue { get; set; }
        public int? DifficultyRating { get; set; }
        public int? EngagementRating { get; set; }
        public int? ExternalVariablesRating { get; set; }
    }
}