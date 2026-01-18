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
        [Required]
        public required Guid UserID { get; init; }
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
        /// <summary>
        /// Optional perceived effort ratings for the session.
        /// </summary>
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
    /// <summary>
    /// Request to create a new training session.
    /// </summary>
    /// <param name="TrainingProgramID">Optional training program ID to associate the session with.</param>
    /// <param name="PerceivedEffortRatings">Optional perceived effort ratings for the session.</param>
    public record CreateTrainingSessionRequest(
        [Required]DateOnly Date,
        Guid? TrainingProgramID,
        string Notes,
        PerceivedEffortRatings? PerceivedEffortRatings
    );
    /// <summary>
    /// Request to update an existing training session.
    /// </summary>
    /// <param name="TrainingProgramID">Optional training program ID to associate the session with.</param>
    /// <param name="PerceivedEffortRatings">Optional perceived effort ratings for the session.</param>
    public record UpdateTrainingSessionRequest(
        [Required]Guid TrainingSessionID,
        Guid? TrainingProgramID,
        [Required]DateOnly Date,
        [Required]TrainingSessionStatus Status,
        string Notes,
        PerceivedEffortRatings? PerceivedEffortRatings
    );
    /// <summary>
    /// Enum representing the status of a training session.
    /// </summary>
    public enum TrainingSessionStatus
    {
        Planned,
        Active,
        Completed,
        Skipped,
        AIModified
    }
}


