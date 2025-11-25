
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.DtoGenerator;
using lionheart.Model.TrainingProgram;


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
    [GenerateDto(typeof(LiftSetEntry),
        Exclude = new[] { "Movement" })]
    public partial record LiftSetEntryDTO;
    [GenerateDto(typeof(LiftSetEntry),
        Exclude = new[] { "SetEntryID", "Movement"})]
    public partial record CreateLiftSetEntryRequest;
    [GenerateDto(typeof(LiftSetEntry),
        Exclude = new[] { "Movement"})]
    public partial record UpdateLiftSetEntryRequest;
}

public enum WeightUnit
{
    Kilograms,
    Pounds
};