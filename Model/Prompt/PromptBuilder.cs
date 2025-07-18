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
    }
}
