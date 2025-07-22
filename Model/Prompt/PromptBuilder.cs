using System.Text;
using lionheart.Model.DTOs;

namespace lionheart.Model.Prompt
{
    public class PromptBuilder
    {
        private readonly List<PromptSection> _sections = new();

        public PromptBuilder AddSection(string title, params string[] lines)
        {
            _sections.Add(new PromptSection(title, lines));
            return this;
        }

        public string Build(bool includeContext = false, string? userId = null)
        {
            var sb = new StringBuilder();
            foreach (var section in _sections)
            {
                sb.AppendLine($"{section.Title}:");
                foreach (var line in section.Lines)
                    sb.AppendLine(line);
                sb.AppendLine();
            }

            if (includeContext && userId is not null)
            {
                sb.AppendLine("Active User Context:");
                sb.AppendLine($"• ID: {userId}");
                sb.AppendLine("[Use this identifier when invoking tools or services]");
            }

            return sb.ToString().Trim();
        }

        private record PromptSection(string Title, string[] Lines);

        // --- Static prompt templates ---

public static PromptBuilder Preferences(ProgramPreferencesDTO dto)
{
    var builder = new PromptBuilder()
        .AddSection("Context",
            "You are assisting with a training program that has already been created.",
            "You are not creating sessions yet.",
            "You are not calling any tools yet.",
            "Simply confirm your understanding of the preferences and wait for the next instruction.");

    builder.AddSection("User Preferences",
        $"- Days per week: {dto.DaysPerWeek}",
        $"- Preferred training days: {dto.PreferredDays}");

    builder.AddSection("Lift Frequency",
        $"- Weekly lift goals → Squat: {dto.SquatDays}, Bench: {dto.BenchDays}, Deadlift: {dto.DeadliftDays}");

    if (!string.IsNullOrWhiteSpace(dto.FavoriteMovements))
    {
        builder.AddSection("Favorites",
            $"- Favorite movements: {dto.FavoriteMovements}");
    }

    builder.AddSection("Instruction",
        "Respond with a short summary (as text) confirming that you’ve noted these preferences and are ready to begin generating sessions when instructed.",
        "Keep in mind: sessions will later be generated to follow a powerlifting split structure.",
        "Lifters typically squat, bench, and deadlift multiple times a week.",
        "Each training session will have 3–5 total movements focused around a main lift and its supporting exercises.");

    return builder;
}

public static PromptBuilder FirstWeek(string programId) =>
    new PromptBuilder()
        .AddSection("Phase 2: Build First Week of Training Sessions",
            $"You are creating the **first full week** of structured training sessions for trainingProgramID: {programId}.",
            "",
            "You MUST create all sessions by calling `CreateTrainingSessionWeekAsync(request)`.",
            "You will provide a single request object with a `trainingProgramID` and a `sessions` array.",
            "Each session in the array must include 3–5 complete `movements`. Do not skip or split movement creation.",
            "",
            "Your request object must follow this structure:",
            "",
            "- trainingProgramID: UUID (provided above)",
            "- sessions: array of session objects, each with:",
            "  • date: string (format: YYYY-MM-DD) within the program date range",
            "  • sessionNumber: integer (sequential, starting at 1)",
            "  • movements: array of 3–5 full movement objects",
            "",
            "Each movement object must include:",
            "- movementBaseID: UUID from GetMovementBasesAsync",
            "- movementModifier: object with:",
            "    • name: optional string (e.g., 'Paused', 'Tempo', etc.)",
            "    • equipmentID: UUID from GetEquipmentsAsync",
            "    • equipment: include full object from GetEquipmentsAsync with equipmentID and name",
            "    • duration: optional integer (e.g., 2 for 2s pause)",
            "- notes: optional rationale or cue (string)",
            "- weightUnit: must be exactly 'Kilograms' or 'Pounds' (matching the enum)",
            "- sets: array of 2–5 set objects, each with:",
            "  • recommendedReps: integer",
            "  • recommendedWeight: number (kg)",
            "  • recommendedRPE: number (e.g. 8.0)"
        )
        .AddSection("Programming Guidelines",
            "- Start each session with a main lift (Squat, Bench Press, or Deadlift)",
            "- Add 2–4 accessory lifts to support the main lift",
            "- Use a variety of movementBaseIDs via GetMovementBasesAsync",
            "- Use appropriate equipmentIDs for each movement via GetEquipmentsAsync",
            "- Balance push/pull and hinge/squat patterns across the week",
            "- Avoid overloading the same muscle groups on consecutive days"
        )
        .AddSection("Tool Call Rules",
            "✅ Call `CreateTrainingSessionWeekAsync(request)` with the full structure — do not narrate or explain.",
            "✅ Provide valid UUIDs for trainingProgramID, movementBaseIDs, and equipmentIDs.",
            "✅ Use GetMovementBasesAsync and GetEquipmentsAsync to ensure all IDs and names are valid.",
            "✅ Generate at least 3 sessions in the week.",
            "❌ Do NOT submit tool calls with empty `movements` arrays — these will be rejected.",
            "❌ Do NOT split session creation from movement population. All must be included in the same request."
        );

    

        public static PromptBuilder RemainingWeeks() =>
            new PromptBuilder()
                .AddSection("Phase 3: Generate Weeks 2–3",
                    "Use week 1 as a template.",
                    "- Copy structure, maintain movement pattern, skip weekends.",
                    "- Increment RPE each week: 7.0 → 7.5 → 8.0.",
                    "- Assign new UUIDs, update dates, assign session numbers.",
                    "- Call createTrainingSessionFromJson(sessionDto) for each.");
    }
}
