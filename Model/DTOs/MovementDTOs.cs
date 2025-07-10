using System.ComponentModel.DataAnnotations;
using lionheart.Model.TrainingProgram;

public class CreateMovementRequest
{
    [Required]
    public required Guid MovementBaseID { get; init; }

    [Required]
    public required MovementModifier MovementModifier { get; init; }

    [Required(AllowEmptyStrings = true)]
    public required string Notes { get; init; }

    [Required]
    public required Guid TrainingSessionID { get; init; }
    [Required]
    public required WeightUnit WeightUnit { get; set; }

}

public class UpdateMovementRequest
{
    [Required]
    public required Guid MovementID { get; init; }
    [Required]
    public required Guid MovementBaseID { get; init; }

    [Required]
    public required MovementModifier MovementModifier { get; init; }

    [Required(AllowEmptyStrings = true)]
    public required string Notes { get; init; }

    [Required]
    public required Guid TrainingSessionID { get; init; }
    [Required]
    public required bool IsCompleted { get; init; }
    [Required]
    public required WeightUnit WeightUnit { get; set; }


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
    public required Guid MovementID { get; init; }
    public required Guid TrainingSessionID { get; init; }
    public required Guid MovementBaseID { get; set; }
    public required MovementBase MovementBase { get; set; } = new();
    public required MovementModifier MovementModifier { get; set; } = new();
    public required WeightUnit WeightUnit { get; set; }
    [Required]
    public required List<SetEntryDTO> Sets { get; set; } = [];
    public required string Notes { get; set; } = string.Empty;
    public required bool IsCompleted { get; set; } = false;
    public required int Ordering { get; set; } 
}

public class MovementOrderUpdate
{
    public Guid MovementID { get; set; }
    public int Ordering { get; set; }
}

public class UpdateMovementOrderRequest
{
    public Guid TrainingSessionID { get; set; }
    public List<MovementOrderUpdate> Movements { get; set; } = new();
}
