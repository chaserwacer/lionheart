using System.ComponentModel.DataAnnotations;
using lionheart.Model.TrainingProgram;

public class CreateMovementRequest
{
    [Required]
    public required Guid MovementBaseID { get; init; }

    [Required]
    public required MovementModifier MovementModifier { get; init; }

    [Required]
    public required string Notes { get; init; }

    [Required]
    public required Guid TrainingSessionID { get; init; }


}

public class UpdateMovementRequest
{
    [Required]
    public required Guid MovementID { get; init; }
    [Required]
    public required Guid MovementBaseID { get; init; }

    [Required]
    public required MovementModifier MovementModifier { get; init; }

    [Required]
    public required string Notes { get; init; }

    [Required]
    public required Guid TrainingSessionID { get; init; }
    [Required]
    public required bool IsCompleted { get; init; }


}



public class CreateMovementBaseRequest
{
    [Required]
    public required string Name { get; init; }
}

public class UpdateMovementsCompletionRequest
{
    [Required]
    public required Guid TrainingSessionID { get; init; }

    [Required]
    public required bool Complete { get; init; }
}

public class MovementDTO
{
    public Guid MovementID { get; init; }
    public Guid TrainingSessionID { get; init; }
    public Guid MovementBaseID { get; set; }
    public MovementBase MovementBase { get; set; } = new();
    public MovementModifier MovementModifier { get; set; } = new();

    public List<SetEntryDTO> Sets { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    
}
