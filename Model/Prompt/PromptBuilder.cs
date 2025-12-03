// using System.Text;
// using FluentValidation.Validators;
// using lionheart.Model.DTOs;

// namespace lionheart.Model.Prompt
// {
//     public class PromptBuilder
//     {
//         private readonly List<PromptSection> _sections = new();

//         public PromptBuilder AddSection(string title, params string[] lines)
//         {
//             _sections.Add(new PromptSection(title, lines));
//             return this;
//         }

//         public string Build(bool includeContext = false, string? userId = null)
//         {
//             var sb = new StringBuilder();
//             foreach (var section in _sections)
//             {
//                 sb.AppendLine($"{section.Title}:");
//                 foreach (var line in section.Lines)
//                     sb.AppendLine(line);
//                 sb.AppendLine();
//             }

//             if (includeContext && userId is not null)
//             {
//                 sb.AppendLine("Active User Context:");
//                 sb.AppendLine($"• ID: {userId}");
//                 sb.AppendLine("[Use this identifier when invoking tools or services]");
//             }

//             return sb.ToString().Trim();
//         }

//         private record PromptSection(string Title, string[] Lines);


//         public static PromptBuilder PowerliftingPreferencesOutline(
//             PowerliftingPreferencesDTO prefs,
//             IEnumerable<MovementBaseSlimDTO> movementBases,
//             IEnumerable<EquipmentSlimDTO> equipments,
//             string? userFeedback = null)
//         {
//             // NOTE: this step is OUTLINE ONLY. No sets/reps, no tool calls.
//             var mbJson = System.Text.Json.JsonSerializer.Serialize(movementBases);
//             var eqJson = System.Text.Json.JsonSerializer.Serialize(equipments);

//             var b = new PromptBuilder()
//                 .AddSection("Role",
//                     "You are Lionheart, a powerlifting coach-engine.",
//                     "TASK: Propose a weekly microcycle outline consistent with the user's preferences.",
//                     "Review the Training Programming Reference document now.",
//                     "HARD RULES:",
//                     "- Do NOT call tools. You have all the data you need inline.",
//                     "- Do NOT produce sets/reps or RPE here.",
//                     "- Output must be PURE JSON matching the schema below, and nothing else.",
//                     "- Minimum number of movements in a week is 18.")
//                 .AddSection("User Preferences",
//                     $"- Days per week: {prefs.DaysPerWeek}",
//                     $"- Preferred days: {prefs.PreferredDays}",
//                     $"- Weekly lift goals ⇒ Squat: {prefs.SquatDays}, Bench: {prefs.BenchDays}, Deadlift: {prefs.DeadliftDays}",
//                     string.IsNullOrWhiteSpace(prefs.FavoriteMovements) ? "" : $"- Favorites: {prefs.FavoriteMovements}",
//                     string.IsNullOrWhiteSpace(prefs.UserGoals) ? "" : $"- Goals: {prefs.UserGoals}")
//                 .AddSection("Available Movement Bases (slim JSON)", mbJson)
//                 .AddSection("Available Equipments (slim JSON)", eqJson)
//                 .AddSection("If the user asked for changes (optional)", userFeedback ?? "(none)")
//                 .AddSection("Output Schema (copy exactly)",
//         @"{
//         ""Summary"": ""string"",
//         ""Microcycle"": [
//             { ""Day"": ""Monday"", ""Focus"": ""string"", ""MainLifts"": [""string""], ""Accessories"": [""string""] }
//         ],
//         ""AccessoryHighlights"": [""string""]
//         }")
//                 .AddSection("Instructions",
//                     "Construct a Mon–Sun plan consistent with DaysPerWeek and PreferredDays.",
//                     "Respect weekly Squat/Bench/Deadlift counts.",
//                     "Name main lifts plainly (e.g., \"Primary Squat\", \"Secondary Bench\") — no sets/reps.",
//                     "Accessories should be movement names present or obvious from movement base names. Keep it short.",
//                     "Return only the JSON. No backticks, no commentary.");
//             return b;
//         }

//         public static PromptBuilder PowerliftingFirstWeek(string programId) =>
//             new PromptBuilder()
//                 .AddSection("Phase 2: Create Week 1",
//                     $"Target Program: {programId}",

//                     // STEP 1 — info tools (parallel)
//                     "Step 1 — Fetch in parallel:",
//                     "- GetTrainingProgramAsync(programId)",
//                     "- GetMovementBasesAsync()",
//                     "- GetEquipmentsAsync()",
//                    // "- WebSearchAsync(query: \"powerlifting programming guidelines\")",
//                  //   "When calling WebSearchAsync, always include a query parameter describing what information you need. Example: { \"query\": \"how to program a powerlifting program for a light weight experienced male\" }",
//                   //  "Wait for results. Use ONLY returned UUIDs.",

//                     // WEEKDAY ANCHORING
//                     "Weekday Anchoring:",
//                     "• Choose exact training weekdays for Week 1 using the user's preferredDays if provided. If not provided, infer a clean pattern (e.g., Mon/Wed/Fri or Mon/Tue/Thu/Sat) within the program date range.",
//                     "• Each session’s DATE must align to those weekdays. These weekday choices become the template for ALL future weeks (same days each week).",
//                     "• Do not schedule weekends unless preferredDays included them.",

//                     // STEP 2 — creation (single call)
//                     "Step 2 — Create ALL Week-1 sessions in ONE call:",
//                     "Call the function EXACTLY in this shape:",
//                     "CreateTrainingSessionWeekAsync({",
//                     "  \"request\": {",
//                     "    \"trainingProgramID\": \"<programId>\",",
//                     "    \"sessions\": [ /* fully populated sessions */ ]",
//                     "  }",
//                     "})",
                    

//                     // STRUCTURE & DATA RULES
//                     "Each session must have (HARD REQUIREMENTS):",
//                     "- 4–5 total movements.",
//                     "- The FIRST movement is the main lift (Squat, Bench, or Deadlift).",
//                     "- Minimum 4 sets total for Squat, Bench, and Deadlift.",
//                     "- Main lift sets: exactly 1 top set (1–3 reps) at RPE 6–8 + 3-4 back-off sets (same lift) at RPE 6–7.",
//                     "- Accessories: 3-6 accessory movements (not main lifts), each with 2–4 sets at RPE 8–9 (5–10 reps).",
//                     "- Don't repeat the same accessory movement more then twice in a week",
//                     "- Don't put the same accessory on consecutive days",
//                     "- Ensure all muscle groups are hit (chest, Lats, Traps, rear delts, front delts, side delts, quads, glutes, hamstrings, triceps, biceps)",
//                     "- movementBaseID from GetMovementBasesAsync.",
//                     "- movementModifier { name, equipmentID, equipment (object), duration } from GetEquipmentsAsync.",
//                     "- weightUnit is 'Kilograms' or 'Pounds'.",
//                     "- sets specify recommendedReps, recommendedWeight, recommendedRPE.",
//                     "- No guessed IDs; only tool-returned UUIDs.",
//                     "- No empty movement arrays.",

//                     "Do not ask for confirmation. Do not narrate. Do not call other tools in this step."

//                 )
//                 .AddSection("Reference",
//                     "Refer to the 'TrainingProgrammingReference' document for:",
//                     "• RPE progression rules.",
//                     "• Example weekly layouts.",
//                     "• Variation usage guidelines.",
//                     "• Accessory programming rules.",
//                     "• Implementation reminders (IDs, equipment, weekday consistency)."
//                 )
//                 .AddSection("Pre-submit checklist (the following MUST be true before you call CreateTrainingSessionWeekAsync):",
//                             "• Each session has 4–5 movements.",
//                             "• Main lift per session has 1 top set + 3–4 back-offs (same lift).",
//                             "• Each session includes 3-6 accessories, each 2–4 sets @ RPE 8–9.",
//                             "• Weekday pattern locked; dates valid.",
//                              "• Only tool-returned UUIDs used; full equipment object included.",
//                             "IF ANY ITEM FAILS: revise the plan and DO NOT call the creation tool yet."

                            
//                 )
//                  .AddSection("Call CreateTrainingSessionWeekAsync NOW",
//                              "• Use the validated session data to call CreateTrainingSessionWeekAsync.",
//                              "• Include all necessary parameters and ensure data integrity.",
//                              "• Handle any errors or issues that arise during the call."
//                 );


//         public static PromptBuilder PowerliftingRemainingWeeks(string programId) =>
//             new PromptBuilder()
//                 .AddSection("Phase 3: Create Weeks 2 and 3",
//                     $"Call GetTrainingProgramAsync('{programId}') to read Week 1 (sessions, dates, frequencies, weekday template).",
//                     "Create Week 2 and Week 3 with exactly the same number of sessions as Week 1.",
//                     "Make ONE CreateTrainingSessionWeekAsync(request) call per week (two total calls)."
//                 )
//                 .AddSection("Weekday Lock + Dates",
//                     "• Preserve the Week-1 weekday template exactly.",
//                     "• Week 2 = Week 1 date + 7 days.",
//                     "• Week 3 = Week 1 date + 14 days.",
//                     "• No shifting to other weekdays."
//                 )
//                 .AddSection("Structure & Data Rules",
//                     "• Continue sessionNumber sequentially; keep chronological order.",
//                     "• Regenerate all UUIDs for sessions/movements/sets.",
//                     "• Keep weightUnit consistent.",
//                     "• Use only tool-returned UUIDs for IDs and include full equipment object."
//                 )
//                 .AddSection("Reference",
//                     "Refer to the 'TrainingProgrammingReference' document for:",
//                     "• RPE wave adjustments for Weeks 2 and 3.",
//                     "• Variation guidelines.",
//                     "• Accessory programming.",
//                     "• Example layouts and recovery spacing."
//                 )
//                 .AddSection("Pre-submit checklist",
//                     "• Weekday pattern matches Week 1.",
//                     "• Two CreateTrainingSessionWeekAsync calls made (one per week).",
//                     "• All sessions fully populated and follow reference rules."
//                 );
                
//                 public static PromptBuilder BodybuildingPreferencesOutline(
//                     BodybuildingPreferencesDTO prefs,
//                     IEnumerable<MovementBaseSlimDTO> movementBases,
//                     IEnumerable<EquipmentSlimDTO> equipments,
//                     string? userFeedback = null)
//                 {
//                     var mbJson = System.Text.Json.JsonSerializer.Serialize(movementBases);
//                     var eqJson = System.Text.Json.JsonSerializer.Serialize(equipments);

//                     return new PromptBuilder()
//                         .AddSection("Role",
//                             "You are Lionheart, a hypertrophy-focused bodybuilding coach-engine.",
//                             "TASK: Propose a weekly microcycle outline consistent with the user's preferences for BODYBUILDING.",
//                             "HARD RULES:",
//                             "- Do NOT call tools. You have all the data you need inline.",
//                             "- Minimum of 18 movements per week.",
//                             "- Do NOT produce sets/reps or RPE here.",
//                             "- Output must be PURE JSON matching the schema below, and nothing else.",
//                             "- Treat any section labeled 'User Adjustments:' as HARD constraints; apply them literally.")
//                         .AddSection("User Preferences",
//                             $"- Days per week: {prefs.DaysPerWeek}",
//                             $"- Preferred days: {prefs.PreferredDays}",
//                             prefs.WeakPoints?.Count > 0 ? $"- Weak points: {string.Join(", ", prefs.WeakPoints)}" : "",
//                             string.IsNullOrWhiteSpace(prefs.Bodyweight) ? "" : $"- Bodyweight: {prefs.Bodyweight}",
//                             prefs.YearsOfExperience is null ? "" : $"- Years of experience: {prefs.YearsOfExperience}",
//                             string.IsNullOrWhiteSpace(prefs.FavoriteMovements) ? "" : $"- Favorites: {prefs.FavoriteMovements}",
//                             string.IsNullOrWhiteSpace(prefs.UserGoals) ? "" : $"- Goals: {prefs.UserGoals}")
//                         .AddSection("Available Movement Bases (slim JSON)", mbJson)
//                         .AddSection("Available Equipments (slim JSON)", eqJson)
//                         .AddSection("If the user asked for changes (optional)", userFeedback ?? "(none)")
//                         .AddSection("Output Schema (copy exactly)",
//                 @"{
//                 ""Summary"": ""string"",
//                 ""Microcycle"": [
//                     { ""Day"": ""Monday"", ""Focus"": ""string"", ""MainLifts"": [""string""], ""Accessories"": [""string""] }
//                 ],
//                 ""AccessoryHighlights"": [""string""]
//                 }")
//                         .AddSection("Instructions",
//                             "Design for hypertrophy: choose a split that fits days/week (e.g., Upper/Lower x2, Push/Pull/Legs, or Full Body x3).",
//                             "MainLifts = key compounds (e.g., High-Bar Squat, Incline Bench, RDL, Weighted Pull-ups).",
//                             "Accessories = isolation/machine work (e.g., Leg Extension, Cable Fly, Lateral Raise).",
//                             "Name movements plainly; no sets/reps.",
//                             "Return only the JSON. No backticks, no commentary.");
//                 }

//                 public static PromptBuilder BodybuildingFirstWeek(string programId) =>
//                     new PromptBuilder()
//                         .AddSection("Phase 2: Create Week 1 (Bodybuilding)",
//                             $"Target Program: {programId}",

//                             // STEP 1 — info tools (parallel)
//                             "Step 1 — Fetch in parallel:",
//                             "- GetTrainingProgramAsync(programId)",
//                             "- GetMovementBasesAsync()",
//                             "- GetEquipmentsAsync()",
//                             "Use ONLY the movementBaseID values and Equipment OBJECTS returned by these tools. Do NOT fabricate or reuse cached/slim lists. Do NOT add fields.",

//                             // WEEKDAY ANCHORING
//                             "Weekday Anchoring:",
//                             "• Choose exact training weekdays for Week 1 using the user's preferredDays if provided. If not provided, infer a clean pattern (e.g., 4-day Upper/Lower split or Push/Pull/Legs + Upper) within the program date range.",
//                             "• Each session’s DATE must align to those weekdays. These weekday choices become the template for ALL future weeks (same days each week).",
//                             "• Do not schedule weekends unless preferredDays included them.",

//                             // STEP 2 — creation (single call)
//                             "Step 2 — Create ALL Week-1 sessions in ONE call:",
//                             "Call the function EXACTLY in this shape:",
//                             "CreateTrainingSessionWeekAsync({",
//                             "  \"request\": {",
//                             "    \"trainingProgramID\": \"<programId>\",",
//                             "    \"sessions\": [ /* fully populated sessions */ ]",
//                             "  }",
//                             "})",

//                             // STRUCTURE & DATA RULES (Hypertrophy)
//                             "Each session must have (HARD REQUIREMENTS):",
//                             "- 4–6 total movements.",
//                             "- Compounds first (as MainLifts), then accessories.",
//                             "- For each movement: 3–4 sets; 6–15 reps typical; RPE 8–9.",
//                             "- Balance weekly sets across major muscle groups (chest, back (lats+upper back), delts (front/side/rear), quads, hamstrings, glutes, arms).",
//                             "- Don't repeat the same accessory more than twice in a week.",
//                             "- Avoid placing the same accessory on consecutive days.",
//                             "- movementBaseID from GetMovementBasesAsync.",
//                             "- movementModifier { name, equipmentID, equipment (object), duration } from GetEquipmentsAsync.",
//                             "- weightUnit is 'Kilograms' or 'Pounds'.",
//                             "- sets specify recommendedReps, recommendedWeight, recommendedRPE.",
//                             "- No guessed IDs; only tool-returned UUIDs.",
//                             "- No empty movement arrays.",

//                             "Do not ask for confirmation. Do not narrate. Only call the creation tool when the checklist passes.")
//                         .AddSection("Pre-submit checklist (MUST be true before calling CreateTrainingSessionWeekAsync):",
//                             "• Each session has 4–6 movements.",
//                             "• Compounds lead each session; accessories follow.",
//                             "• Each movement includes 3–4 sets in the 6–15 rep range at RPE 8–9.",
//                             "• Weekly volume distribution across muscle groups is reasonable.",
//                             "• Weekday pattern locked; dates valid.",
//                             "• For every movement: movementModifier.equipmentID is from GetEquipmentsAsync.",
//                             "• For every movement: movementModifier.equipment (object) is exactly the tool-returned object for that equipmentID (unchanged).",
//                             "IF ANY ITEM FAILS: revise the plan and DO NOT call the creation tool yet.")
//                         .AddSection("Call CreateTrainingSessionWeekAsync NOW",
//                             "• Use the validated session data to call CreateTrainingSessionWeekAsync.",
//                             "• Include all necessary parameters and ensure data integrity.",
//                             "• Handle any errors or issues that arise during the call.");

//                 public static PromptBuilder BodybuildingRemainingWeeks(string programId) =>
//                     new PromptBuilder()
//                         .AddSection("Phase 3: Create Weeks 2 and 3 (Bodybuilding)",
//                             $"Call GetTrainingProgramAsync('{programId}') to read Week 1 (sessions, dates, weekday template).",
//                             "Create Week 2 and Week 3 with exactly the same number of sessions as Week 1.",
//                             "Make ONE CreateTrainingSessionWeekAsync(request) call per week (two total calls).")
//                         .AddSection("Weekday Lock + Dates",
//                             "• Preserve the Week-1 weekday template exactly.",
//                             "• Week 2 = Week 1 date + 7 days.",
//                             "• Week 3 = Week 1 date + 14 days.",
//                             "• No shifting to other weekdays.")
//                         .AddSection("Structure & Progression (Hypertrophy)",
//                             "• Continue sessionNumber sequentially; keep chronological order.",
//                             "• Regenerate all UUIDs for sessions/movements/sets.",
//                             "• Keep weightUnit consistent.",
//                             "• Progress by modestly adjusting reps/sets/RPE (e.g., +1 set on a lagging muscle group; or +1–2 reps on accessories; or small RPE bump where recovery allows).",
//                             "• You may rotate variations (grip/angle/stance/machine) to distribute joint stress while keeping intent (e.g., Incline DB Press ↔ Incline Machine Press).",
//                             "• Use only tool-returned UUIDs for IDs and include full equipment object.")
//                         .AddSection("Pre-submit checklist",
//                             "• Weekday pattern matches Week 1.",
//                             "• Two CreateTrainingSessionWeekAsync calls made (one per week).",
//                             "• Volume stays in hypertrophy range; distribution remains balanced.",
//                             "• All sessions fully populated and follow reference rules.");






//         public static PromptBuilder AnalyzeUserWellness() =>
//             new PromptBuilder()
//                 .AddSection("Analyze User Wellness",
//                     "### Your Role:",
//                     "You are an expert wellness analyst and training assistant. Your job is to interpret physiological and behavioral signals from the user and identify key insights that affect performance, health, and recovery. You will synthesize data from multiple inputs to create a comprehensive snapshot of user state and wellness trends.",

//                     "### You Will Receive:",
//                     "- `userContext`: A structured JSON object containing the users training data from the past 7 days, including:",
//                     "  - Oura Wearable data: sleep, heart rate, HRV, temperature, readiness, activity, recovery, etc.",
//                     "  - Wellness Scores: Subjective inputs: fatigue, mood, soreness, stress, perceived effort, illness markers.",
//                     "  - Training Sessions : Recent training sessions with performance metrics (RPE, volume, load, etc.).",
//                     "  - Activities: Other activities performed (outside of training programs).",

//                     "### Data Notes:",
//                     "- The `userContext` is a serialized JSON object containing all the above data.",
//                     "- The users context may not include all fields for each day. (oura ring may not have data for every day, etc.)",
//                     "- The oura ring data has some unique structuring. Many of the values are stored as integers, representing a percentage from [0,100].",
//                     "- Example: `sleepScore` is an integer from 0 to 100, where higher is better. Deep sleep is also [0,100], where the number represents the percentage of the night spent in deep sleep.",

//                     "### Your Responsibilities:",
//                     "1. Ingest and interpret the full `userContext` holistically.",
//                     "2. Identify the current state, highlighting any abnormal signals (e.g., elevated temperature, declining HRV, sleep disruption, or increased fatigue).",
//                     "3. Detect trends across the past 7 days, especially compounding effects (e.g., accumulating fatigue, improving recovery, illness onset, disrupted patterns).",
//                     "4. Assess the user's adaptive capacity and readiness — is performance improving, stagnating, or declining?",
//                     "5. Integrate both quantitative metrics and subjective feedback to create a synthesized, contextual view.",
//                     "6. Look for warning signs (e.g., overtraining, illness, burnout risk) or areas of resilience (e.g., consistent recovery, positive mood).",

//                     "### Constraints:",
//                     "- Avoid generic advice. All insights must be based on actual trends or signals in the input.",
//                     "- Be concise but precise. Highlight only the most meaningful contributors to the user's state.",
//                     "- Do not make medical diagnoses, but identify suspicious trends or risk factors clearly.",
//                     "- Ensure summary aligns with the quantitative *and* qualitative context.",

//                     "### Output:",
//                     "- `stateAnalysis`: A brief structured summary (~100–300 words) of the user's wellness based on recent data, describing trends, contributing factors, and any significant observations (e.g., signs of illness, strong recovery, overreaching, etc.)."
//                 );


//         public static PromptBuilder ModifyTrainingSession() =>
//                 new PromptBuilder()
//                     .AddSection("Modify Training Session",
//                         "You are Lionheart’s Adaptive Training Coach, an expert AI system specializing in modifying strength training sessions based on user performance, recovery, and preferences.",
//                         "",
//                         "### Your Role:",
//                         "Your job is to adjust training sessions for users to optimize recovery, progression, and safety. You are provided with tools that allow you to manage and update movements and sets within a session. You must use these tools intelligently to apply all necessary modifications. Do not attempt to edit the session directly in memory; always rely on the tools provided.",
//                         "",
//                         "### You will receive:",
//                         "- `trainingSession`: The training session to modify(serialized JSON).",
//                         "- `userContextSummary`: A structured, holistic summary of the users wellness and performance over the past 7 days, with descriptions of trends, contributing factors, and any significant observations.",
//                         "- available movement bases: A list of valid movement bases (serialized JSON).",
//                         "- available equipments: A list of valid equipment items (serialized JSON).",
//                         "- UserPrompt: A user-provided prompt with specific instructions for modifying the session or additional user context notes.",
//                         "",
//                         "### Your Responsibilities:",
//                         "1. Analyze the userContextSummary to determine how the session should be adapted, based on the users current wellness state.",
//                         "2. Consider the UserPrompt, and attempt to implement the requested modifications, so long as they align with the user’s current state.",
//                         "3. Use the provided tools to perform all modifications to the session:",
//                         "   - Add, remove, or adjust movements.",
//                         "   - Modify sets, reps, loads, RPE, and other parameters.",
//                         "4. For each change, populate the movement’s `notes` field with a concise rationale (e.g., \"Reduced load by 10% due to poor sleep and high fatigue markers\").",
//                         "5. Provide a brief overall summary of the modifications at the end, end-user facing. Store them within the training session’s `notes` field via an update session tool call.",
//                         "",
//                         "### Tools:",
//                         "You have access to functions for:",
//                         "- Creating, deleting, and modifying movements and sets within a session.",
//                         "Always call these tools when making any change. Do not generate a new session JSON directly.",
//                         "If you plan to add a movement to a session, it must include a valid movement base and valid equipment item, provided in the available movement bases and equipments.",
//                         "You will create the tool call and return to me a completion with ChatFinishReason.ToolCalls. I will parse the call and return the data formatted.",
//                         "",
//                         "### Constraints:",
//                         "- Only modify the provided session.",
//                         "- When modifying a session, maintain its original structure and intent, applying only targeted adjustments that optimize for the athlete’s current state rather than redesigning the workout.",
//                         "- Be efficient: minimize the number of tool calls by reasoning carefully before acting.",
//                         "- Do not fill out the actual values of set entrys. You should me modifying the recommended values.",
//                         "- Generally, your modification rationale should remain consistent across movements.",
//                         "",
//                         "### Output:",
//                         "Once all necessary tools have been called and modifications applied, return:",
//                         "- `sessionSummary`: A brief description of key changes and reasoning (1-2 sentences).");
//     }
// }
