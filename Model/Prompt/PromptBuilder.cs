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
                .AddSection("Phase 2: Build First Week of Sessions",
                    $"Use trainingProgramID: {programId}.",
                    "Generate training sessions for week 1 based on the user's preferences and powerlifting programming principles.",
                    "Each training day should include 3–5 movements:",
                    "- Start with a main lift (Squat, Bench, or Deadlift)",
                    "- Follow with 2–4 secondary/accessory movements to build that main lift. Find all movements using GetMovementBasesAsync tool.",
                    "- Each movement must include:",
                    "  • movementID: new UUID",
                    "  • movementBaseID: valid ID from GetMovementBasesAsync",
                    "  • sets: include 2–5 recommended sets, each with reps, weight, and RPE",
                    "- Balance movement patterns across the week (e.g. push/pull, hinge/squat, horizontal/vertical pressing)",
                    "- Avoid overloading same muscle groups on consecutive days.",
                    "- It's acceptable to include multiple compound lifts on the same day if needed.",
                    "",
                    "Each session must include the following fields:",
                    "- trainingSessionID: new UUID",
                    "- trainingProgramID: same as above",
                    "- date: string, format YYYY-MM-DD — ensure dates fall between the program's start and end dates",
                    "- sessionNumber: sequential (start at 1)",
                    "- status: 0",
                    "- movements: list of 3–5 properly structured movements",
                    "",
                    "Call `CreateTrainingSessionAsync(request)` with the full object.",
                    "All fields are required. Do not omit any.");

        public static PromptBuilder RemainingWeeks() =>
            new PromptBuilder()
                .AddSection("Phase 3: Generate Weeks 2–3",
                    "Use week 1 as a template.",
                    "- Copy structure, maintain movement pattern, skip weekends.",
                    "- Increment RPE each week: 7.0 → 7.5 → 8.0.",
                    "- Assign new UUIDs, update dates, assign session numbers.",
                    "- Call createTrainingSessionFromJson(sessionDto) for each.");

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
