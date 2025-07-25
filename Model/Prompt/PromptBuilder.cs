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

            if (!string.IsNullOrWhiteSpace(dto.UserGoals))
            {
                builder.AddSection("User Goals",
                    $"The user provided the following high-level notes about their needs:",
                    $"{dto.UserGoals}");
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
                    "Start by calling these **information tools in parallel**:",
                    $"- `GetTrainingProgramAsync` with the provided program ID {programId}",
                    "- `GetMovementBasesAsync` to see valid movementBaseID options",
                    "- `GetEquipmentsAsync` to retrieve valid equipment options",
                    "✅ You MUST wait for the tool call results before using movementBaseIDs or equipmentIDs in any creation requests.",
                    "✅ Use only the UUIDs returned by GetMovementBasesAsync when assigning movementBaseID values.",
                    "",
                    "Once you have the necessary information, create all sessions by calling `CreateTrainingSessionWeekAsync(request)`.",
                    "You will submit a single request object with a `trainingProgramID` and a `sessions` array.",
                    "Each session in the array must include 3–5 fully populated `movements`. Do not skip or delay movement creation.",
                    "",
                    "Your request object must follow this structure:",
                    "",
                    "- trainingProgramID: UUID (provided above)",
                    "- sessions: array of session objects, each with:",
                    "  • date: string (YYYY-MM-DD) within the program’s start and end date",
                    "  • sessionNumber: integer (sequential, starting from 1)",
                    "  • movements: array of 3–5 full movement objects",
                    "",
                    "Each movement object must include:",
                    "- movementBaseID: UUID from GetMovementBasesAsync",
                    "- movementModifier: object with:",
                    "    • name: optional string (e.g. 'Paused', 'Tempo')",
                    "    • equipmentID: UUID from GetEquipmentsAsync",
                    "    • equipment: full object from GetEquipmentsAsync (must match equipmentID)",
                    "    • duration: optional integer (e.g. 2 for 2s pause)",
                    "- notes: optional string cue or rationale",
                    "- weightUnit: must be either 'Kilograms' or 'Pounds'",
                    "- sets: array of 2–5 set objects, each with:",
                    "  • recommendedReps: integer",
                    "  • recommendedWeight: number (in kg or lb)",
                    "  • recommendedRPE: number (e.g. 8.0)"
                )
                .AddSection("Programming Guidelines",
                    "- Start each session with a main lift (Squat, Bench Press, or Deadlift)",
                    "- Each main lift must begin with a 'top set' (higher RPE by 0.5, usually 1–3 reps)",
                    "- Follow the top set with 2–4 'backdown sets' (same movement, lower RPE)",
                    "- Some sessions may include multiple main lifts",
                    "- Add 2–4 accessories that support the main lift(s)",
                    "- Vary your selection using GetMovementBasesAsync results",
                    "- Use proper equipmentIDs via GetEquipmentsAsync for each movement",
                    "- Follow user preferences from the previous step (lift focus, split, etc.)",
                    "- Avoid overloading the same muscles on consecutive days"
                )
                .AddSection("Tool Call Rules",
                    "✅ First, call `GetTrainingProgramAsync`, `GetMovementBasesAsync`, and `GetEquipmentsAsync` — these can be called in parallel.",
                    "✅ Then call `CreateTrainingSessionWeekAsync(request)` with all sessions and movements included.",
                    "✅ Ensure all UUIDs (trainingProgramID, movementBaseIDs, equipmentIDs) are valid.",
                    "❌ Do NOT submit tool calls with empty `movements` arrays.",
                    "❌ Do NOT split session creation from movement population.",
                    "❌ Do NOT call `CreateTrainingSessionWeekAsync` before retrieving equipment and movement data."
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
                        "4. Provide a brief overall summary of the modifications at the end, end-user facing. Store them within the training session’s `notes` field via an update session tool call.",
                        "",
                        "### Tools:",
                        "You have access to functions for:",
                        "- Retrieving additional athlete data if needed.",
                        "- Modifying movements and sets within a session.",
                        "Always call these tools when making any change. Do not generate a new session JSON directly.",
                        "If you plan to add a movement to a session, it must include a valid movement base and valid equipment item, retrivable via `GetMovementBasesAsync`, as well as `GetEquipmentsAsync`.",
                        "You will create the tool call and return to me a completion with ChatFinishReason.ToolCalls. I will parse the call and return the data formatted.",
                        "The tool declarations have complex objects, they dont all indicate this, but all of the fields for each the different objects are required.",
                        "",
                        "### Constraints:",
                        "- Only modify the provided session.",
                        "- When modifying a session, maintain its original structure and intent, applying only targeted adjustments that optimize for the athlete’s current state rather than redesigning the workout.",
                        // "- Follow all modificationParameters strictly, including injury constraints, preferred strategies (e.g., reduce volume vs. load), and coaching style.",
                        "- Be efficient: minimize the number of tool calls by reasoning carefully before acting.",
                        "- Set the training sessions status to `AIModified` [enum TrainingSessionStatus.AIModified = 4].",
                        "- Do not fill out the actual values of set entrys. You should me modifying the recommended values.",
                        "",
                        "### Output:",
                        "Once all necessary tools have been called and modifications applied, return:",
                        "- `sessionSummary`: A brief description of key changes and reasoning (1-2 sentences).");
    }
}
