using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.DTOs
{
    public class ProgramShellDTO
    {
        [Required]
        public required string Title { get; init; }

        [Required]
        [DataType(DataType.Date)]
        public required DateOnly StartDate { get; init; }

        [Required]
        [DataType(DataType.Date)]
        public required DateOnly EndDate { get; init; }

        [Required]
        public required string Tag { get; init; }
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
