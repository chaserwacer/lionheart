using System.ComponentModel.DataAnnotations;
using lionheart.Model.TrainingProgram;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace lionheart.Model.DTOs
{
    public class ProgramPreferencesDTO
    {
        [Required, Range(1, 7)]
        public required int DaysPerWeek { get; init; }

        [Required]
        public required string PreferredDays { get; init; } = string.Empty;

        public string FavoriteMovements { get; init; } = string.Empty;
        public string? UserGoals { get; set; }

        // Keep the endpoint stable; carry sport-specific keys here.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; init; }
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
    


     public class PowerliftingPreferencesDTO : ProgramPreferencesDTO
    {
        public int SquatDays { get; init; }
        public int BenchDays { get; init; }
        public int DeadliftDays { get; init; }

        public static PowerliftingPreferencesDTO FromBase(ProgramPreferencesDTO dto)
        {
            return new PowerliftingPreferencesDTO
            {
                DaysPerWeek       = dto.DaysPerWeek,
                PreferredDays     = dto.PreferredDays,
                FavoriteMovements = dto.FavoriteMovements,
                UserGoals         = dto.UserGoals,
                Extra             = dto.Extra,

                // tolerant parsing from Extra (number or string)
                SquatDays    = GetInt(dto.Extra, "squatDays")    ?? 0,
                BenchDays    = GetInt(dto.Extra, "benchDays")    ?? 0,
                DeadliftDays = GetInt(dto.Extra, "deadliftDays") ?? 0,
            };
        }

        private static int? GetInt(Dictionary<string, JsonElement>? extra, string key)
        {
            if (extra is null) return null;
            if (!extra.TryGetValue(key, out var el)) return null;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var m)) return m;
            return null;
        }
    }

    public class BodybuildingPreferencesDTO : ProgramPreferencesDTO
    {
        public List<string> WeakPoints      { get; init; } = new();
        public string?      Bodyweight      { get; init; } // "172 lbs" or "78 kg"
        public int?         YearsOfExperience { get; init; }

        public static BodybuildingPreferencesDTO FromBase(ProgramPreferencesDTO dto)
        {
            return new BodybuildingPreferencesDTO
            {
                DaysPerWeek       = dto.DaysPerWeek,
                PreferredDays     = dto.PreferredDays,
                FavoriteMovements = dto.FavoriteMovements,
                UserGoals         = dto.UserGoals,
                Extra             = dto.Extra,

                WeakPoints        = GetWeakPoints(dto.Extra),
                Bodyweight        = GetString(dto.Extra, "bodyweight"),
                YearsOfExperience = GetInt(dto.Extra, "yearsOfExperience"),
            };
        }

        private static List<string> GetWeakPoints(Dictionary<string, JsonElement>? extra)
        {
            var list = new List<string>();
            if (extra is null) return list;
            if (!extra.TryGetValue("weakPoints", out var el)) return list;

            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
                    }
                }
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    list = s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                            .Where(x => x.Length > 0)
                            .ToList();
                }
            }
            return list;
        }

        private static string? GetString(Dictionary<string, JsonElement>? extra, string key)
        {
            if (extra is null) return null;
            if (!extra.TryGetValue(key, out var el)) return null;
            return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
        }

        private static int? GetInt(Dictionary<string, JsonElement>? extra, string key)
        {
            if (extra is null) return null;
            if (!extra.TryGetValue(key, out var el)) return null;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var m)) return m;
            return null;
        }
    }
}




