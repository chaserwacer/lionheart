using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Eventing.Reader;
using lionheart.Model.TrainingProgram;

namespace lionheart.Model.DTOs;

public class CreateTrainingProgramRequest
{
    [Required]
    public required string Title { get; init; }

    [Required]
    public required DateOnly StartDate { get; init; }

    [Required]
    public required DateOnly EndDate { get; init; }

    [Required]
    public required List<string> Tags { get; init; }
}

public class UpdateTrainingProgramRequest
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


}

public class TrainingSessionDTO
{
    public Guid TrainingSessionID { get; init; }
    public Guid TrainingProgramID { get; init; }
    public int SessionNumber { get; set; }
    public DateOnly Date { get; set; }
    public TrainingSessionStatus Status { get; set; } 
    public List<Movement> Movements { get; set; } = [];
}