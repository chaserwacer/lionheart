using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


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
        public TrainingSessionPerceivedEffortRatings? PerceivedEffortRatings { get; set; }
    }
    public class TrainingSessionPerceivedEffortRatings
    {
        [Key]
        public required Guid TrainingSessionID { get; init; }
        public required PerceivedEffortRatings PerceivedEffortRatings { get; set; }
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
        TrainingSessionPerceivedEffortRatings? PerceivedEffortRatings
    );
    public record CreateTrainingSessionRequest(
        DateOnly Date,
        Guid? TrainingProgramID,
        string Notes,
        TrainingSessionPerceivedEffortRatings? PerceivedEffortRatings
    );
    public record UpdateTrainingSessionRequest(
        Guid TrainingSessionID,
        Guid? TrainingProgramID,
        DateOnly Date,
        TrainingSessionStatus Status,
        string Notes,
        TrainingSessionPerceivedEffortRatings? PerceivedEffortRatings
    );
    public enum TrainingSessionStatus
    {
        Planned,
        InProgress,
        Completed,
        Skipped,
        AIModified
    }
}


   