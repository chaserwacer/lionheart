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
        public string? UserGoals { get; set; }

    }

    public class FirstWeekGenerationDTO
    {
        [Required]
        public string TrainingProgramID { get; init; } = string.Empty;
    }

    public class RemainingWeeksGenerationDTO
    {
        [Required]
        public string TrainingProgramID { get; init; } = string.Empty;
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

        public string Notes { get; set; } = string.Empty;
    }

    public class CreateTrainingSessionWeekRequest
    {
        [Required]
        public required Guid TrainingProgramID { get; init; }

        [Required]
        public required List<CreateTrainingSessionWithMovementsDTO> Sessions { get; init; }
    }
        public class PreferencesOutlineRequest
    {
        // existing user inputs
        public required ProgramPreferencesDTO Preferences { get; init; }
        // optional free-text feedback to redo the outline
        public string? UserFeedback { get; init; }
    }

    // What the AI returns for the outline step
    public class ProgramOutlineDTO
    {
        public required string Summary { get; init; } // short human-facing blurb
        public required List<OutlineDayDTO> Microcycle { get; init; } = new();
        public required List<string> AccessoryHighlights { get; init; } = new();
    }

    public class OutlineDayDTO
    {
        public required string Day { get; init; }
        public required string Focus { get; init; }
        public List<string> MainLifts { get; init; } = new();
        public List<string> Accessories { get; init; } = new();
    }

    public class MovementBaseSlimDTO
    {
        public required Guid MovementBaseID { get; init; }
        public required string Name { get; init; }
    }
    public class EquipmentSlimDTO
    {
        public required Guid EquipmentID { get; init; }
        public required string Name { get; init; }
    }
}

