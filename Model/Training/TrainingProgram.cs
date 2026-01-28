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
        public TrainingProgramDTO ToDTO()
        {
            return new TrainingProgramDTO
            {
                TrainingProgramID = TrainingProgramID,
                Title = Title,
                StartDate = StartDate,
                EndDate = EndDate,
                IsCompleted = IsCompleted,
                TrainingSessions = TrainingSessions.Select(ts => ts.ToDTO()).ToList(),
                Tags = Tags
            };
        }
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
        [Required] Guid TrainingProgramID,
        [Required] string Title,
        [Required] DateTime StartDate,
        [Required] DateTime EndDate,
        List<string> Tags
    );
    public record UpdateTrainingProgramRequest(
        [Required] Guid TrainingProgramID,
        [Required] string Title,
        [Required] DateTime StartDate,
        [Required] DateTime EndDate,
        [Required] bool IsCompleted,
        List<string> Tags
    );
}