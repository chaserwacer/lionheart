using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.Training
{
    /// <summary>
    /// Class representing a fitness training program that contains a collection of <see cref="TrainingSession"/>s.
    /// </summary>
    public class TrainingProgram
    {
        [Key]
        public required Guid TrainingProgramID { get; init; }
        public required Guid UserID { get; init; }
        public required string Title { get; set; } = string.Empty;
        public required DateOnly StartDate { get; set; }
        public required DateOnly EndDate { get; set; }
        public required bool IsCompleted { get; set; } = false;
        public required List<TrainingSession> TrainingSessions { get; set; } = [];
        public required List<string> Tags { get; set; } = [];
    }
    public record TrainingProgramDTO
    {
        [Required]
        public Guid TrainingProgramID { get; init; }
    
        [Required]
        public string Title { get; init; } = string.Empty;
    
        [Required]
        public DateOnly StartDate { get; init; }
    
        [Required]
        public DateOnly EndDate { get; init; }
    
        [Required]
        public bool IsCompleted { get; init; }
    
        [Required]
        public List<TrainingSessionDTO> TrainingSessions { get; init; } = [];
    
        [Required]
        public List<string> Tags { get; init; } = [];
    }

    public record CreateTrainingProgramRequest(
        Guid TrainingProgramID,
        string Title,
        DateOnly StartDate,
        DateOnly EndDate,
        List<string> Tags
    );
    public record UpdateTrainingProgramRequest(
        Guid TrainingProgramID,
        string Title,
        DateOnly StartDate,
        DateOnly EndDate,
        bool IsCompleted,
        List<string> Tags
    );
}