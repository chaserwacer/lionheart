using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Eventing.Reader;
using lionheart.Model.TrainingProgram;

namespace lionheart.Model.DTOs;

public class CreateTrainingProgramRequest : IValidatableObject
{
    [Required]
    public required string Title { get; init; }

    [Required]
    public required DateOnly StartDate { get; init; }

    [Required]
    public required DateOnly EndDate { get; init; }

    [Required]
    public required List<string> Tags { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "EndDate cannot be before StartDate.",
                new[] { nameof(EndDate), nameof(StartDate) }
            );
        }
    }
}
public class TrainingProgramDTO
{
    [Required]
    public required Guid TrainingProgramID { get; init; }
    [Required]
    public required string Title { get; set; } = string.Empty;
    [Required]
    public required DateOnly StartDate { get; set; }

    [Required]
    public required DateOnly NextTrainingSessionDate { get; set; }
    [Required]
    public required DateOnly EndDate { get; set; }
    [Required]
    public required List<TrainingSessionDTO> TrainingSessions { get; set; } = [];
    [Required]
    public required List<string> Tags { get; set; } = [];
    [Required]
    public required bool IsCompleted { get; set; }
}

public class UpdateTrainingProgramRequest : IValidatableObject
{
    [Required]
    public required Guid TrainingProgramID { get; init; }
    [Required]
    public required string Title { get; init; }

    [Required]
    public required DateOnly StartDate { get; init; }

    [Required]
    public required DateOnly EndDate { get; init; }

    [Required]
    public required List<string> Tags { get; init; }
    [Required]
    public required bool IsCompleted { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "EndDate cannot be before StartDate.",
                new[] { nameof(EndDate), nameof(StartDate) }
            );
        }
    }
}

public class GetTrainingSessionsRequest
{
    [Required]
    public required Guid TrainingProgramID { get; init; }
}



public class CreateTrainingSessionRequest
{
    [Required]
    public required DateOnly Date { get; init; }
    [Required]
    public Guid TrainingProgramID { get; init; }
}


public class UpdateTrainingSessionRequest
{
    [Required]
    public Guid TrainingProgramID { get; init; }

    [Required]
    public required DateOnly Date { get; init; }


    [Required]
    public required TrainingSessionStatus Status { get; init; }
    [Key]
    public Guid TrainingSessionID { get; init; }
     [Required(AllowEmptyStrings = true)]
    public required string Notes { get; set; } = string.Empty;

}

public class TrainingSessionDTO
{
    public required Guid TrainingSessionID { get; init; }
    public required Guid TrainingProgramID { get; init; }
    public required double SessionNumber { get; set; } // Changed from int to double
    public required DateOnly Date { get; set; }
    public required TrainingSessionStatus Status { get; set; }
    [Required]
    public required List<MovementDTO> Movements { get; set; } = [];
    [Required]
    public required string Notes { get; set; } = string.Empty;
}

public class GenerateTrainingSessionsRequest
{
    [Required]
    public required Guid TrainingProgramID { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public required int Count { get; init; }
}

public class GetTrainingSessionRequest
{
    [Required]
    public required Guid TrainingSessionID { get; init; }
    [Required]
    public required Guid TrainingProgramID { get; init; }
}
    public class GetTrainingProgramRequest
    {
        [Required]
        public required Guid TrainingProgramID { get; init; }
    }