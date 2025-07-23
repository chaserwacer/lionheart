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
                    "Call `GetTrainingProgramAsync` with the provided ID to retrieve the program details, including the start date and end date.",
                    "You MUST create all sessions by calling `CreateTrainingSessionWeekAsync(request)`.",
                    "You will provide a single request object with a `trainingProgramID` and a `sessions` array.",
                    "Each session in the array must include 3–5 complete `movements`. Do not skip or split movement creation.",
                    "",
                    "Your request object must follow this structure:",
                    "",
                    "- trainingProgramID: UUID (provided above)",
                    "- sessions: array of session objects, each with:",
                    "  • date: string (format: YYYY-MM-DD) within the start date and end date of the program",
                    "  • sessionNumber: integer (sequential, starting at 1)",
                    "  • movements: array of 3–5 full movement objects",
                    "",
                    "Each movement object must include:",
                    "- movementBaseID: UUID from GetMovementBasesAsync",
                    "- movementModifier: object with:",
                    "    • name: optional string (e.g., 'Paused', 'Tempo', etc.)",
                    "    • equipmentID: UUID from GetEquipmentsAsync",
                    "    • equipment: include full object from GetEquipmentsAsync with equipmentID and name",
                    "    • duration: integer (e.g., 2 for 2s pause)",
                    "- notes: optional rationale or cue (string)",
                    "- weightUnit: must be exactly 'Kilograms' or 'Pounds' (matching the enum)",
                    "- sets: array of 2–5 set objects, each with:",
                    "  • recommendedReps: integer",
                    "  • recommendedWeight: number (kg)",
                    "  • recommendedRPE: number (e.g. 8.0)"
                )
                .AddSection("Programming Guidelines",
                    "- Start each session with a main lift (Squat, Bench Press, or Deadlift)",
                    "- The main lifts should have a 'top set' with a .5 higher RPE than the 'backdowns' and should be 1-3 reps",
                    "- The top set is followed by 2-4 backdown sets with a lower RPE",
                    "- There will likely be more then one main lift some days",
                    "- Add 2–4 accessory lifts to support the main lift",
                    "- Use a variety of movementBaseIDs via GetMovementBasesAsync",
                    "- Use appropriate equipmentIDs for each movement via GetEquipmentsAsync",
                    "- Use the preferences from the previous step to guide your choices",
                    "- Avoid overloading the same muscle groups on consecutive days"
                )
                .AddSection("Tool Call Rules",
                    "✅ Call `CreateTrainingSessionWeekAsync(request)` with the full structure — do not narrate or explain.",
                    "✅ Provide valid UUIDs for trainingProgramID, movementBaseIDs, and equipmentIDs.",
                    "✅ Use GetMovementBasesAsync and GetEquipmentsAsync to ensure all IDs and names are valid.",
                    "✅ Generate exactly how many sessions the user requested in the previous step.",
                    "❌ Do NOT submit tool calls with empty `movements` arrays — these will be rejected.",
                    "❌ Do NOT split session creation from movement population. All must be included in the same request."
                );



        public static PromptBuilder RemainingWeeks(string programId) =>
    new PromptBuilder()
        .AddSection("Phase 3: Build Weeks 2 and 3 of the Training Program",
            "call getTrainingProgramAsync with '" + programId + "' to retrieve the training program details",
            "Copy the structure of Week 1 and create Weeks 2 and 3.",
            "You MUST call `CreateTrainingSessionWeekAsync(request)` once for Week 2 and once for Week 3.",
            "Each call should generate a `sessions` array with the same number of sessions as Week 1.",
            "Do NOT generate duplicate session content — RPE should increase from week to week.",
            "",
            "Follow these rules for each week:",
            "- Keep the same session structure and movement order (e.g., main lifts on same days).",
            "- Progressively increase RPE for each week:",
            "  • Week 2: +0.5 RPE on all sets",
            "  • Week 3: +1.0 RPE compared to Week 1",
            "- Regenerate all UUIDs: trainingSessionID, movementID, and setEntryID.",
            "- Update sessionNumber (continue from last week) and set new dates (no weekends).",
            "- Maintain proper movement balance and recovery (avoid repeating high-intensity movements on back-to-back days)."
        )
        .AddSection("Technical Requirements",
            "- Each session must contain 3–5 `movements`, each with 2–5 `sets`.",
            "- Date: string (format: YYYY-MM-DD) within the program date range",
            "- Every `movement` must include:",
            "  • movementBaseID: from GetMovementBasesAsync",
            "  • movementModifier: required, including:",
            "     - name (optional),",
            "     - equipmentID (from GetEquipmentsAsync),",
            "     - equipment object,",
            "     - duration (optional integer)",
            "  • notes: optional rationale or cue",
            "  • weightUnit: must be 'Kilograms' or 'Pounds'",
            "  • sets: each with reps, weight, and RPE (adjusted per week)"
        )
        .AddSection("Tool Call Rules",
            "✅ Call `CreateTrainingSessionWeekAsync(request)` once for each new week.",
            "✅ Use fresh UUIDs for all sessions, movements, and sets.",
            "✅ Use GetMovementBasesAsync and GetEquipmentsAsync for valid movementBaseIDs and equipmentIDs.",
            "✅ Ensure correct sessionNumber sequencing and proper date progression.",
            "❌ Do NOT repeat exact movement sets from previous weeks.",
            "❌ Do NOT use weekends for training unless they were used in Week 1.",
            "❌ Do NOT split movement population from session creation.");



        public static PromptBuilder ModifyTrainingSessionSys() =>
                new PromptBuilder()
                    .AddSection("Modify Training Session",
                        "You are Lionheart’s Adaptive Training Coach, an expert AI system specializing in modifying strength training sessions based on user performance, recovery, and preferences.",
                        "",
                        "### Your Role:",
                        "Your job is to adjust training sessions for users to optimize recovery, progression, and safety. You are provided with tools that allow you to manage and update movements and sets within a session. You must use these tools intelligently to apply all necessary modifications. Do not attempt to edit the session directly in memory; always rely on the tools provided.",
                        "",
                        "### You will receive:",
                        "- `trainingSession`: The training session to modify(serialized JSON).",
                        "- `userContext`: The user’s recent performance data, wearable metrics, and subjective feedback (serialized JSON).",
                        // "- `modificationParameters`: Dynamic coaching preferences and constraints (serialized JSON).",
                        "",
                        "### Your Responsibilities:",
                        "1. Analyze the userContext and modificationParameters to determine how the session should be adapted.",
                        "2. Use the provided tools to perform all modifications to the session:",
                        "   - Add, remove, or adjust movements.",
                        "   - Modify sets, reps, loads, RPE, and other parameters.",
                        "3. For each change, populate the movement’s `notes` field with a concise rationale (e.g., \"Reduced load by 10% due to poor sleep and high fatigue markers\").",
                        "4. Provide a brief overall summary of the modifications at the end.",
                        "",
                        "### Tools:",
                        "You have access to functions for:",
                        "- Retrieving additional athlete data if needed.",
                        "- Modifying movements and sets within a session.",
                        "Always call these tools when making any change. Do not generate a new session JSON directly.",
                        "If you plan to add a movement to a session, it must include a valid movement base and valid equipment item, retrivable via `GetMovementBasesAsync`, as well as `GetEquipmentsAsync`.",
                        "You will create the tool call and return to me a completion with ChatFinishReason.ToolCalls. I will parse the call and return the data formatted.",
                        "",
                        "### Constraints:",
                        "- Only modify the provided session.",
                        "- When modifying a session, maintain its original structure and intent, applying only targeted adjustments that optimize for the athlete’s current state rather than redesigning the workout.",
                        // "- Follow all modificationParameters strictly, including injury constraints, preferred strategies (e.g., reduce volume vs. load), and coaching style.",
                        "- Be efficient: minimize the number of tool calls by reasoning carefully before acting.",
                        "- Set the training sessions status to `AIModified` [enum TrainingSessionStatus.AIModified = 4].",
                        "",
                        "### Output:",
                        "Once all necessary tools have been called and modifications applied, return:",
                        "- `sessionSummary`: A brief description of key changes and reasoning (1-2 sentences).");
    }
}
