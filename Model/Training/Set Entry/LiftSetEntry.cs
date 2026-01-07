namespace lionheart.Model.Training.SetEntry
{
    //// <summary>
    /// Represents an occurence of a <see cref="Movement"/> being performed in a lifting/repetition/weight based manner.
    /// </summary>
    /// <remarks>
    /// Lifting sets can contain optional recommended values for reps, weight, and RPE. This allows for preprogramming of workouts.
    /// </remarks>
    public class LiftSetEntry
    {
        public Guid SetEntryID { get; init; }
        public Guid MovementID { get; init; }
        public Movement Movement { get; set; } = null!;
        public int? RecommendedReps { get; set; }
        public double? RecommendedWeight { get; set; }
        public double? RecommendedRPE { get; set; }
        public int ActualReps { get; set; }
        public double ActualWeight { get; set; }
        public double ActualRPE { get; set; }
        public required WeightUnit WeightUnit { get; set; }
    }
    public record LiftSetEntryDTO(
        Guid SetEntryID,
        Guid MovementID,
        int? RecommendedReps,
        double? RecommendedWeight,
        double? RecommendedRPE,
        int ActualReps,
        double ActualWeight,
        double ActualRPE,
        WeightUnit WeightUnit
    );

    public record CreateLiftSetEntryRequest(
        Guid MovementID,
        int? RecommendedReps,
        double? RecommendedWeight,
        double? RecommendedRPE,
        int ActualReps,
        double ActualWeight,
        double ActualRPE,
        WeightUnit WeightUnit
    );

    public record UpdateLiftSetEntryRequest(
        Guid SetEntryID,
        Guid MovementID,
        int? RecommendedReps,
        double? RecommendedWeight,
        double? RecommendedRPE,
        int ActualReps,
        double ActualWeight,
        double ActualRPE,
        WeightUnit WeightUnit
    );

    public enum WeightUnit
    {
        Kilograms,
        Pounds
    };
}