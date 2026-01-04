using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace lionheart.Model.Training
{
    /// <summary>
    /// Representation of a training session, which is a collection of <see cref="Movement"/>s performed by a user on a specific date.
    /// </summary>
    /// <remarks>
    /// A training session can be part of a <see cref="TrainingProgram"/> or standalone.
    /// </remarks>
    public class TrainingSession
    {
        [Key]
        [Required]
        public required Guid TrainingSessionID { get; init; }
        [ForeignKey(nameof(TrainingProgram))]
        public Guid? TrainingProgramID { get; set; }
        /// <summary>
        /// Optional reference to the training program this session is part of.
        /// </summary>
        public TrainingProgram? TrainingProgram { get; set; }
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
        Active,
        Completed,
        Skipped,
        AIModified
    }
}


