// using Ardalis.Result;
// using lionheart.Model.DTOs;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.Extensions.AI;
// using Model.McpServer;
// using System.Text.Json;

// namespace lionheart.Services.AI
// {
//     public class ProgramGenerationService : IProgramGenerationService
//     {
//         private readonly IOpenAiClientService _openAi;
//         private readonly ITrainingProgramService _trainingProgramService;
//         private readonly ITrainingSessionService _trainingSessionService;
//         private readonly IMovementService _movementService;
//         private readonly ISetEntryService _setEntryService;

//         public ProgramGenerationService(
//             IOpenAiClientService openAi,
//             ITrainingProgramService trainingProgramService,
//             ITrainingSessionService trainingSessionService,
//             IMovementService movementService,
//             ISetEntryService setEntryService)
//         {
//             _openAi = openAi;
//             _trainingProgramService = trainingProgramService;
//             _trainingSessionService = trainingSessionService;
//             _movementService = movementService;
//             _setEntryService = setEntryService;
//         }

//         public async Task<Result<string>> GenerateInitializationAsync(IdentityUser user)
//         {
//             var prompt = new LionMcpPrompt { User = user };

//             var task = new InstructionPromptSection { Name = "Task Overview" };
//             task.AddInstruction("You are an agent that generates valid TrainingProgramDTO JSON for a .NET codebase. You will guide the user through the program creation workflow.");
//             prompt.Sections.Add(task);

//             var behavior = new InstructionPromptSection { Name = "Behavior" };
//             behavior.AddInstruction("Do not create any JSON or issue any tool calls yet. Simply acknowledge readiness and await further user input.");
//             prompt.Sections.Add(behavior);

//             var response = await _openAi.ChatSimpleAsync(prompt.ToStringPrompty());
//             return Result.Success(response);
//         }

//         public async Task<Result<string>> GenerateProgramShellAsync(IdentityUser user, Dictionary<string, object>? inputs)
//         {
//             var title = inputs?["title"]?.ToString() ?? "Untitled Program";
//             var length = inputs?["lengthWeeks"]?.ToString() ?? "3";

//             var prompt = new LionMcpPrompt { User = user };
//             var task = new InstructionPromptSection { Name = "Phase 1: Create Program Shell" };

//             task.AddInstruction($"Create a blank TrainingProgramDTO named '{title}' with a duration of {length} weeks.");
//             task.AddInstruction("- trainingProgramID: new UUID v4");
//             task.AddInstruction("- tags: [\"Powerlifting\"]");
//             task.AddInstruction("- createdByUserId: getIdentityUser()");
//             task.AddInstruction("- startDate, endDate, nextTrainingSessionDate: all set to the next Monday on or after 2025-06-30");
//             task.AddInstruction("- trainingSessions: empty array");
//             task.AddInstruction("Call `createTrainingProgramFromJson(programDto)` using the object above.");

//             prompt.Sections.Add(task);

//             var response = await _openAi.ChatWithToolsAsync(prompt.ToStringPrompty(), user);
//             return Result.Success(response);
//         }

//         public async Task<Result<string>> GeneratePreferencesAsync(IdentityUser user, Dictionary<string, object>? inputs)
//         {
//             var daysPerWeek = inputs?["daysPerWeek"]?.ToString() ?? "4";
//             var preferredDays = inputs?["preferredDays"] is IEnumerable<object> days
//                 ? string.Join(", ", days)
//                 : "Mon, Wed, Fri";
//             var squatDays = inputs?["squatDays"]?.ToString() ?? "2";
//             var benchDays = inputs?["benchDays"]?.ToString() ?? "3";
//             var deadliftDays = inputs?["deadliftDays"]?.ToString() ?? "1";
//             var favoriteMovements = inputs?["favoriteMovements"] is IEnumerable<object> moves
//                 ? string.Join(", ", moves)
//                 : "None";

//             var prompt = new LionMcpPrompt { User = user };
//             var task = new InstructionPromptSection { Name = "Phase 2: User Preferences" };

//             task.AddInstruction("Use the following preferences provided by the user:");
//             task.AddInstruction($"- Days per week: {daysPerWeek}");
//             task.AddInstruction($"- Preferred training days: {preferredDays}");
//             task.AddInstruction($"- Weekly lift goals → Squat: {squatDays}, Bench: {benchDays}, Deadlift: {deadliftDays}");
//             task.AddInstruction($"- Favorite movements: {favoriteMovements}");
//             task.AddInstruction("Use these values when planning sessions, but do not generate any sessions yet.");

//             prompt.Sections.Add(task);

//             var response = await _openAi.ChatWithToolsAsync(prompt.ToStringPrompty(), user);
//             return Result.Success(response);
//         }

//         public async Task<Result<string>> GenerateFirstWeekAsync(IdentityUser user, Dictionary<string, object>? inputs)
//         {
//             var programId = inputs?["trainingProgramID"]?.ToString() ?? "MISSING_PROGRAM_ID";
//             var prompt = new LionMcpPrompt { User = user };
//             var task = new InstructionPromptSection { Name = "Phase 3: Build First Week of Sessions" };

//             task.AddInstruction($"Use trainingProgramID: {programId}.");
//             task.AddInstruction("For each selected training day in week 1:");
//             task.AddInstruction("1. Construct a TrainingSessionDTO:");
//             task.AddInstruction("   - trainingSessionID: new UUID");
//             task.AddInstruction("   - sessionNumber: sequential, using getTrainingSessions(programId)");
//             task.AddInstruction("   - status: 0");
//             task.AddInstruction("   - movements: based on user's preferred lifts");
//             task.AddInstruction("2. Call `createTrainingSessionFromJson(sessionDto)` after each session.");

//             prompt.Sections.Add(task);
//             var response = await _openAi.ChatWithToolsAsync(prompt.ToStringPrompty(), user);
//             return Result.Success(response);
//         }

//         public async Task<Result<string>> GenerateRemainingWeeksAsync(IdentityUser user, Dictionary<string, object>? inputs)
//         {
//             var prompt = new LionMcpPrompt { User = user };
//             var task = new InstructionPromptSection { Name = "Phase 4: Generate Weeks 2–3" };

//             task.AddInstruction("Use week 1 as a template.");
//             task.AddInstruction("- Copy structure, maintain movement pattern, skip weekends.");
//             task.AddInstruction("- Increment RPE each week: 7.0 → 7.5 → 8.0.");
//             task.AddInstruction("- Assign new UUIDs, update dates, assign session numbers.");
//             task.AddInstruction("- Call createTrainingSessionFromJson(sessionDto) for each.");

//             prompt.Sections.Add(task);
//             var response = await _openAi.ChatWithToolsAsync(prompt.ToStringPrompty(), user);
//             return Result.Success(response);
//         }
//     }
// }
