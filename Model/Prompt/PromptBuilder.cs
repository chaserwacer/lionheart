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
                "Acknowledge these preferences in 3–5 lines and say you’re ready to fetch data/tools when instructed.",
                "Do NOT call tools yet. Do NOT propose sets/reps here.");


            return builder;
        }


public static PromptBuilder FirstWeek(string programId) =>
  new PromptBuilder()
    .AddSection("Phase 2: Create Week 1",
      $"Target Program: {programId}",

      // STEP 1 — info tools (parallel)
      "Step 1 — Fetch in parallel:",
      "- GetTrainingProgramAsync(programId)",
      "- GetMovementBasesAsync()",
      "- GetEquipmentsAsync()",
      "Wait for results. Use ONLY returned UUIDs.",

      // WEEKDAY ANCHORING
      "Weekday Anchoring:",
      "• Choose exact training weekdays for Week 1 using the user's preferredDays if provided. If not provided, infer a clean pattern (e.g., Mon/Wed/Fri or Mon/Tue/Thu/Sat) within the program date range.",
      "• Each session’s DATE must align to those weekdays. These weekday choices become the template for ALL future weeks (same days each week).",
      "• Do not schedule weekends unless preferredDays included them.",

      // STEP 2 — creation (single call)
      "Step 2 — Immediately create ALL Week‑1 sessions in ONE call:",
      "Call the function EXACTLY in this shape:",
      "CreateTrainingSessionWeekAsync({",
      "  \"request\": {",
      "    \"trainingProgramID\": \"<programId>\",",
      "    \"sessions\": [ /* fully populated sessions */ ]",
      "  }",
      "})",
      "Do not ask for confirmation. Do not narrate. Do not call other tools in this step.",

      // content rules (coaching)
      "Sessions:",
      "- Each session has 3–5 movements; the FIRST is the main lift (Squat/Bench/Deadlift).",
      "- Main lift must include a TOP SET (1–3 reps, highest RPE) + 2–4 back‑off sets (lower RPE).",
      "- 2–4 accessories at RPE 8–9 that support the main lift.",
      "- Distribute S/B/D frequencies exactly per user request.",
      "- Use simple DUP across the week (skill/volume/strength exposures).",

      // variations
      "Variations (movementModifier):",
      "- Use 'Competition' when no special variant (duration = 0).",
      "- Use 'Paused' for paused variations; set duration to the pause seconds (e.g., 2).",
      "- Use 'Tempo 3-2-0' style for tempo; set duration to the total tempo seconds (e.g., 3+2+0 = 5).",
      "- Always include equipmentID and equipment (object) copied from GetEquipmentsAsync for the chosen implement.",

      // minimal object reminders (schema handles shape)
      "Movement object reminders:",
      "- movementBaseID from GetMovementBasesAsync,",
      "- movementModifier { name, equipmentID, equipment (object), duration },",
      "- weightUnit is 'Kilograms' or 'Pounds',",
      "- sets: 2–5 with recommendedReps, recommendedWeight, recommendedRPE.",

      // guardrails
      "Rules:",
      "✅ Info tools first; ✅ Then one CreateTrainingSessionWeekAsync call with fully populated sessions;",
      "✅ Respect S/B/D frequencies & weekday anchoring; ✅ Use only tool‑returned UUIDs;",
      "❌ No empty movement arrays; ❌ No guessed IDs; ❌ No extra info calls during creation.")
    .AddSection("Programming Guardrails",
      "- Avoid heavy hip/back stress on consecutive days; separate heavy Squat and heavy Deadlift days when possible.",
      "- Bench gets the highest weekly frequency; include at least one lower‑RPE bench day for skill and one heavier day.",
      "- Top sets guide intensity; back‑offs accrue volume.")
    .AddSection("Pre‑submit checklist",
      "• Week 1 dates align to the chosen weekdays; • each session starts with a main lift;",
      "• 3–5 movements per session; • main lift has top set + back‑offs;",
      "• accessories at RPE 8–9; • only tool‑returned UUIDs used; • one creation call made.");

    public static PromptBuilder RemainingWeeks(string programId) =>
      new PromptBuilder()
        .AddSection("Phase 3: Create Weeks 2 and 3",
          $"Call GetTrainingProgramAsync('{programId}') to read Week 1 (sessions, dates, frequencies, weekday template).",
          "Create Week 2 and Week 3 with exactly the same number of sessions as Week 1.",
          "Make ONE CreateTrainingSessionWeekAsync(request) call per week (two total calls).")

        .AddSection("Weekday Lock + Dates",
          "• Preserve the Week‑1 weekday template: if a session was Monday in Week 1, it MUST be Monday in Weeks 2 and 3.",
          "• Derive dates strictly by offset from Week 1:",
          "  – Week 2 date = Week 1 date + 7 days",
          "  – Week 3 date = Week 1 date + 14 days",
          "• Do not shift to different weekdays. Do not add weekends unless Week 1 used weekends.")

        .AddSection("Progression (S/B/D only)",
          "Apply the RPE wave ONLY to the main lifts (Squat/Bench/Deadlift). Accessories remain RPE 8–9.",
          "• Week 2: +0.5 RPE vs the matching main‑lift sets in Week 1.",
          "• Week 3: +1.0 RPE vs Week 1 (technical—no grinders).",
          "Rep targets may stay the same; adjust load to hit the RPE.")

        .AddSection("Programming Guardrails",
          "• First movement each session is the main lift with a top set (1–3 reps, highest RPE) + 2–4 back‑offs (lower RPE).",
          "• 2–4 accessories at RPE 8–9 that support the main lift.",
          "• Respect exact weekly S/B/D frequencies.",
          "• Recovery spacing: avoid heavy hip/back stress on consecutive days; separate heavy SQ and heavy DL when possible.",
          "• No verbatim duplicates; minor accessory/target tweaks OK (keep the same structure).")

        .AddSection("Variations (movementModifier)",
          "• Use 'Competition' (duration 0) when comp style; 'Paused' with duration in seconds for pause; 'Tempo a-b-c' with duration = a+b+c.",
          "• Always copy equipmentID and full equipment object (including userID) from GetEquipmentsAsync; IDs must match.")

        .AddSection("Dates, Order, IDs",
          "• Dates within program range; follow the +7d/+14d rule.",
          "• Continue sessionNumber sequentially; keep chronological order.",
          "• Regenerate ALL UUIDs for sessions/movements/sets.",
          "• Keep weightUnit consistent across all tools and requests.");




        public static PromptBuilder AnalyzeUserWellness() =>
            new PromptBuilder()
                .AddSection("Analyze User Wellness",
                    "### Your Role:",
                    "You are an expert wellness analyst and training assistant. Your job is to interpret physiological and behavioral signals from the user and identify key insights that affect performance, health, and recovery. You will synthesize data from multiple inputs to create a comprehensive snapshot of user state and wellness trends.",

                    "### You Will Receive:",
                    "- `userContext`: A structured JSON object containing the users training data from the past 7 days, including:",
                    "  - Oura Wearable data: sleep, heart rate, HRV, temperature, readiness, activity, recovery, etc.",
                    "  - Wellness Scores: Subjective inputs: fatigue, mood, soreness, stress, perceived effort, illness markers.",
                    "  - Training Sessions : Recent training sessions with performance metrics (RPE, volume, load, etc.).",
                    "  - Activities: Other activities performed (outside of training programs).",

                    "### Data Notes:",
                    "- The `userContext` is a serialized JSON object containing all the above data.",
                    "- The users context may not include all fields for each day. (oura ring may not have data for every day, etc.)",
                    "- The oura ring data has some unique structuring. Many of the values are stored as integers, representing a percentage from [0,100].",
                    "- Example: `sleepScore` is an integer from 0 to 100, where higher is better. Deep sleep is also [0,100], where the number represents the percentage of the night spent in deep sleep.",
                    
                    "### Your Responsibilities:",
                    "1. Ingest and interpret the full `userContext` holistically.",
                    "2. Identify the current state, highlighting any abnormal signals (e.g., elevated temperature, declining HRV, sleep disruption, or increased fatigue).",
                    "3. Detect trends across the past 7 days, especially compounding effects (e.g., accumulating fatigue, improving recovery, illness onset, disrupted patterns).",
                    "4. Assess the user's adaptive capacity and readiness — is performance improving, stagnating, or declining?",
                    "5. Integrate both quantitative metrics and subjective feedback to create a synthesized, contextual view.",
                    "6. Look for warning signs (e.g., overtraining, illness, burnout risk) or areas of resilience (e.g., consistent recovery, positive mood).",

                    "### Constraints:",
                    "- Avoid generic advice. All insights must be based on actual trends or signals in the input.",
                    "- Be concise but precise. Highlight only the most meaningful contributors to the user's state.",
                    "- Do not make medical diagnoses, but identify suspicious trends or risk factors clearly.",
                    "- Ensure summary aligns with the quantitative *and* qualitative context.",

                    "### Output:",
                    "- `stateAnalysis`: A brief structured summary (~100–300 words) of the user's wellness based on recent data, describing trends, contributing factors, and any significant observations (e.g., signs of illness, strong recovery, overreaching, etc.)."
                );


        public static PromptBuilder ModifyTrainingSession() =>
                new PromptBuilder()
                    .AddSection("Modify Training Session",
                        "You are Lionheart’s Adaptive Training Coach, an expert AI system specializing in modifying strength training sessions based on user performance, recovery, and preferences.",
                        "",
                        "### Your Role:",
                        "Your job is to adjust training sessions for users to optimize recovery, progression, and safety. You are provided with tools that allow you to manage and update movements and sets within a session. You must use these tools intelligently to apply all necessary modifications. Do not attempt to edit the session directly in memory; always rely on the tools provided.",
                        "",
                        "### You will receive:",
                        "- `trainingSession`: The training session to modify(serialized JSON).",
                        "- `userContextSummary`: A structured, holistic summary of the users wellness and performance over the past 7 days, with descriptions of trends, contributing factors, and any significant observations.",
                        "- available movement bases: A list of valid movement bases (serialized JSON).",
                        "- available equipments: A list of valid equipment items (serialized JSON).",
                        "- UserPrompt: A user-provided prompt with specific instructions for modifying the session or additional user context notes.",
                        "",
                        "### Your Responsibilities:",
                        "1. Analyze the userContextSummary to determine how the session should be adapted, based on the users current wellness state.",
                        "2. Consider the UserPrompt, and attempt to implement the requested modifications, so long as they align with the user’s current state.",
                        "3. Use the provided tools to perform all modifications to the session:",
                        "   - Add, remove, or adjust movements.",
                        "   - Modify sets, reps, loads, RPE, and other parameters.",
                        "4. For each change, populate the movement’s `notes` field with a concise rationale (e.g., \"Reduced load by 10% due to poor sleep and high fatigue markers\").",
                        "5. Provide a brief overall summary of the modifications at the end, end-user facing. Store them within the training session’s `notes` field via an update session tool call.",
                        "",
                        "### Tools:",
                        "You have access to functions for:",
                        "- Creating, deleting, and modifying movements and sets within a session.",
                        "Always call these tools when making any change. Do not generate a new session JSON directly.",
                        "If you plan to add a movement to a session, it must include a valid movement base and valid equipment item, provided in the available movement bases and equipments.",
                        "You will create the tool call and return to me a completion with ChatFinishReason.ToolCalls. I will parse the call and return the data formatted.",
                        "",
                        "### Constraints:",
                        "- Only modify the provided session.",
                        "- When modifying a session, maintain its original structure and intent, applying only targeted adjustments that optimize for the athlete’s current state rather than redesigning the workout.",
                        "- Be efficient: minimize the number of tool calls by reasoning carefully before acting.",
                        "- Do not fill out the actual values of set entrys. You should me modifying the recommended values.",
                        "- Generally, your modification rationale should remain consistent across movements.",
                        "",
                        "### Output:",
                        "Once all necessary tools have been called and modifications applied, return:",
                        "- `sessionSummary`: A brief description of key changes and reasoning (1-2 sentences).");
    }
}
