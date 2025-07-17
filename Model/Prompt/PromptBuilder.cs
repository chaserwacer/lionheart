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

        public static PromptBuilder Initialization() =>
            new PromptBuilder()
                .AddSection("Task Overview",
                    "You are an agent that generates valid TrainingProgramDTO JSON for a .NET codebase. You will guide the user through the program creation workflow.")
                .AddSection("Behavior",
                    "Do not create any JSON or issue any tool calls yet. Simply acknowledge readiness and await further user input.");

        public static PromptBuilder ProgramShell(string title, DateOnly startDate, DateOnly endDate, string tag) =>
        new PromptBuilder()
            .AddSection("Phase 1: Create Program Shell",
            "The user has provided these values:",
            $"- title: \"{title}\"",
            $"- startDate: \"{startDate:yyyy-MM-dd}\"",
            $"- endDate: \"{endDate:yyyy-MM-dd}\"",
            $"- tags: [\"{tag}\"]",
            "",
            "Now CALL the function CreateTrainingProgramAsync(request) with those values; do NOT emit any other text."
            );


        public static PromptBuilder Preferences(ProgramPreferencesDTO dto)
        {
            var builder = new PromptBuilder()
                .AddSection("Phase 2: User Preferences",
                    "Use the following preferences provided by the user:",
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
                "Use these values when planning sessions, but do not generate any sessions yet.");

            return builder;
        }

        public static PromptBuilder FirstWeek(string programId) =>
            new PromptBuilder()
                .AddSection("Phase 3: Build First Week of Sessions",
                    $"Use trainingProgramID: {programId}.",
                    "For each selected training day in week 1:",
                    "1. Construct a TrainingSessionDTO:",
                    "   - trainingSessionID: new UUID",
                    "   - sessionNumber: sequential, using getTrainingSessions(programId)",
                    "   - status: 0",
                    "   - movements: based on user's preferred lifts",
                    "2. Call `createTrainingSessionFromJson(sessionDto)` after each session.");

        public static PromptBuilder RemainingWeeks() =>
            new PromptBuilder()
                .AddSection("Phase 4: Generate Weeks 2–3",
                    "Use week 1 as a template.",
                    "- Copy structure, maintain movement pattern, skip weekends.",
                    "- Increment RPE each week: 7.0 → 7.5 → 8.0.",
                    "- Assign new UUIDs, update dates, assign session numbers.",
                    "- Call createTrainingSessionFromJson(sessionDto) for each.");
    }
}
