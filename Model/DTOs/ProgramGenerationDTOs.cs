using System.ComponentModel.DataAnnotations;
using lionheart.Model.TrainingProgram;


namespace lionheart.Model.DTOs
{
    public class ProgramPreferencesDTO
    {
        [Required]
        [Range(1, 7)]
        public required int DaysPerWeek { get; init; }

        [Required]
        public required string PreferredDays { get; init; } = string.Empty;

        [Range(0, 7)]
        public int SquatDays { get; init; }

        [Range(0, 7)]
        public int BenchDays { get; init; }

        [Range(0, 7)]
        public int DeadliftDays { get; init; }

        public string FavoriteMovements { get; init; } = string.Empty;
    }

    public class FirstWeekGenerationDTO
    {
        [Required]
        public string TrainingProgramID { get; init; } = string.Empty;
    }

    public class RemainingWeeksGenerationDTO
    {
        // No inputs now; included for structural parity and potential expansion
    }
    public class CreateSetEntryInlineDTO
    {
        [Required]
        public required int RecommendedReps { get; init; }

        [Required]
        public required double RecommendedWeight { get; init; }

        [Required]
        [Range(0, 10.0)]
        public required double RecommendedRPE { get; init; }
    }
    public class CreateMovementInlineDTO
    {
        [Required]
        public required Guid MovementBaseID { get; init; }

        public MovementModifier? MovementModifier { get; init; } = null;

        [Required(AllowEmptyStrings = true)]
        public required string Notes { get; init; }

        [Required]
        public required WeightUnit WeightUnit { get; init; }

        [Required]
        public required List<CreateSetEntryInlineDTO> Sets { get; init; }
    }



    public class CreateTrainingSessionWithMovementsDTO
    {
        [Required]
        public required DateOnly Date { get; init; }

        [Required]
        public required int SessionNumber { get; init; }

        [Required]
        public required List<CreateMovementInlineDTO> Movements { get; init; }
    }

    public class CreateTrainingSessionWeekRequest
    {
        [Required]
        public required Guid TrainingProgramID { get; init; }

        [Required]
        public required List<CreateTrainingSessionWithMovementsDTO> Sessions { get; init; }
    }
}

