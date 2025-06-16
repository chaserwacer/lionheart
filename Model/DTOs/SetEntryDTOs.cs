
using System.ComponentModel.DataAnnotations;
using lionheart.Model.TrainingProgram;

public class CreateSetEntryRequest
{
    [Required]
    public required int RecommendedReps { get; init; }

    [Required]
    public required double RecommendedWeight { get; init; }

    [Required]
    [Range(1, 10)]
    public required int RecommendedRPE { get; init; }

    [Required]
    public required WeightUnit WeightUnit { get; init; }

    [Required]
    public required int ActualReps { get; init; }

    [Required]
    public required double ActualWeight { get; init; }

    [Required]
    [Range(1, 10)]
    public required int ActualRPE { get; init; }
    [Required]
    public required Guid MovementID { get; init; }

}

public class UpdateSetEntryRequest
{
    [Required]
    public required Guid SetEntryID { get; init; }
    [Required]
    public required int RecommendedReps { get; init; }

    [Required]
    public required double RecommendedWeight { get; init; }

    [Required]
    [Range(1, 10)]
    public required int RecommendedRPE { get; init; }

    [Required]
    public required WeightUnit WeightUnit { get; init; }

    [Required]
    public required int ActualReps { get; init; }

    [Required]
    public required double ActualWeight { get; init; }

    [Required]
    [Range(1, 10)]
    public required int ActualRPE { get; init; }
    [Required]
    public required Guid MovementID { get; init; }

}

public class SetEntryDTO
{
    public Guid SetEntryID { get; init; }
    public Guid MovementID { get; init; }
    public int RecommendedReps { get; set; }
    public double RecommendedWeight { get; set; }
    public int RecommendedRPE { get; set; }
    public WeightUnit WeightUnit { get; set; }
    public int ActualReps { get; set; }
    public double ActualWeight { get; set; }
    public int ActualRPE { get; set; }

}
