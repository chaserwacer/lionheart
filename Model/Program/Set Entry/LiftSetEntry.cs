
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.DtoGenerator;
using lionheart.Model.TrainingProgram;
namespace lionheart.Model.TrainingProgram.SetEntry;

/// <summary>
/// Represents a set entry within a <see cref="Movement"/>.
/// A set entry contains the recommended and actual reps, weight, and RPE (Rate of Perceived Exertion).
/// </summary>
public class LiftSetEntry : ISetEntry
{
    public Guid SetEntryID { get; init; }
    public Guid MovementID { get; init; }
    public Movement Movement { get; set; } = null!;
    public int RecommendedReps { get; set; }
    public double RecommendedWeight { get; set; }
    public double RecommendedRPE { get; set; }
    public int ActualReps { get; set; }
    public double ActualWeight { get; set; }
    public double ActualRPE { get; set; }
    public required WeightUnit WeightUnit { get; set; }
}
public record LiftSetEntryDTO(
    Guid SetEntryID,
    Guid MovementID,
    int RecommendedReps,
    double RecommendedWeight,
    double RecommendedRPE,
    int ActualReps,
    double ActualWeight,
    double ActualRPE,
    WeightUnit WeightUnit
) : ISetEntryDTO;

public record CreateLiftSetEntryRequest(
    Guid MovementID,
    int RecommendedReps,
    double RecommendedWeight,
    double RecommendedRPE,
    int ActualReps,
    double ActualWeight,
    double ActualRPE,
    WeightUnit WeightUnit
) : ICreateSetEntryRequest;

public record UpdateLiftSetEntryRequest(
    Guid SetEntryID,
    Guid MovementID,
    int RecommendedReps,
    double RecommendedWeight,
    double RecommendedRPE,
    int ActualReps,
    double ActualWeight,
    double ActualRPE,
    WeightUnit WeightUnit
) : IUpdateSetEntryRequest;

public enum WeightUnit
{
    Kilograms,
    Pounds
};