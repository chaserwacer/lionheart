using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using lionheart.Model.TrainingProgram;
using System.Text.Json.Serialization;

public class CreateSetEntryRequest : IValidatableObject
{
    [Required]
    public required int RecommendedReps { get; init; }

    [Required]
    public required double RecommendedWeight { get; init; }

    [Required]
    [Range(0, 10.0)]
    public required double RecommendedRPE { get; init; }


    [Required]
    public required int ActualReps { get; init; }

    [Required]
    public required double ActualWeight { get; init; }

    [Required]
    [Range(0, 10.0)]
    public required double ActualRPE { get; init; }
    
    [Required]
    public required Guid MovementID { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate RecommendedRPE has valid increments of 0.5
        if (Math.Abs(RecommendedRPE * 2 % 1) > 0.001)
        {
            yield return new ValidationResult(
                "RecommendedRPE must be in increments of 0.5",
                new[] { nameof(RecommendedRPE) });
        }

        // Validate ActualRPE has valid increments of 0.5
        if (Math.Abs(ActualRPE * 2 % 1) > 0.001)
        {
            yield return new ValidationResult(
                "ActualRPE must be in increments of 0.5",
                new[] { nameof(ActualRPE) });
        }
    }
}

public class UpdateSetEntryRequest : IValidatableObject
{
    [Required]
    [JsonPropertyName("setEntryID")]
    public required Guid SetEntryID { get; init; }
    
    [Required]
    [JsonPropertyName("recommendedReps")]
    public required int RecommendedReps { get; init; }

    [Required]
    [JsonPropertyName("recommendedWeight")]
    public required double RecommendedWeight { get; init; }

    [Required]
    [JsonPropertyName("recommendedRPE")]
    [Range(0, 10.0)]
    public required double RecommendedRPE { get; init; }


    [Required]
    [JsonPropertyName("actualReps")]
    public required int ActualReps { get; init; }

    [Required]
    [JsonPropertyName("actualWeight")]
    public required double ActualWeight { get; init; }

    [Required]
    [JsonPropertyName("actualRPE")]
    [Range(0, 10.0)]
    public required double ActualRPE { get; init; }
    
    [Required]
    [JsonPropertyName("movementID")]
    public required Guid MovementID { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate RecommendedRPE has valid increments of 0.5
        if (Math.Abs(RecommendedRPE * 2 % 1) > 0.001)
        {
            yield return new ValidationResult(
                "RecommendedRPE must be in increments of 0.5",
                new[] { nameof(RecommendedRPE) });
        }

        // Validate ActualRPE has valid increments of 0.5
        if (Math.Abs(ActualRPE * 2 % 1) > 0.001)
        {
            yield return new ValidationResult(
                "ActualRPE must be in increments of 0.5",
                new[] { nameof(ActualRPE) });
        }
    }
}

public class SetEntryDTO
{
    public required Guid SetEntryID { get; init; }
    public required Guid MovementID { get; init; }
    public required int RecommendedReps { get; set; }
    public required double RecommendedWeight { get; set; }
    public required double RecommendedRPE { get; set; }
    public required int ActualReps { get; set; }
    public required double ActualWeight { get; set; }
    public required double ActualRPE { get; set; }
}
