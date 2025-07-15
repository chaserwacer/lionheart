using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.DTOs
{
    public class ProgramShellDTO
    {
        [Required]
        public required string Title { get; init; } = string.Empty;

        [Required]
        [Range(1, 52, ErrorMessage = "Length must be between 1 and 52 weeks.")]
        public required int LengthWeeks { get; init; }
    }

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
}
