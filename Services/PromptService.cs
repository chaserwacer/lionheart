using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Ardalis.Result;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using lionheart.Model.DTOs;
using Model.McpServer;


namespace lionheart.Services
{
    public class PromptService : IPromptService
    {
        private readonly ILogger<MCPClientService> _logger;
        private readonly IOuraService _ouraService;
        private readonly IWellnessService _wellnessService;
        private readonly ITrainingProgramService _trainingProgramService;
        public PromptService(
            ILogger<MCPClientService> logger,
            IChatClient chatClient,
            IOuraService ouraService,
            IWellnessService wellnessService,
            ITrainingProgramService trainingProgramService
                    )
        {
            _logger = logger;
            _trainingProgramService = trainingProgramService;
            _ouraService = ouraService;
            _wellnessService = wellnessService;
        }





        public async Task<Result<string>> GeneratePromptAsync(IdentityUser user, GeneratePromptRequest request)
        {
            if (request.PromptType is not null && request.PromptType == "cb.01")
            {
                var dateRange = new DateRangeRequest
                {
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
                    EndDate = DateOnly.FromDateTime(DateTime.UtcNow)
                };

                var lionMCPPrompt = new LionMcpPrompt { User = user };

                var taskSection = new InstructionPromptSection { Name = "Primary Task Description" };
                taskSection.AddInstruction("You are Lionheart, an intelligent training assistant that helps users manage their athletic performance, training plans, wellness, and recovery.");
                taskSection.AddInstruction("Please assist in generating insights on a users recent data that will be provided later.");
                lionMCPPrompt.Sections.Add(taskSection);

                var instructions = new InstructionPromptSection { Name = "Instructions" };
                instructions.AddInstruction("1. Analyze the users recent oura data and wellness data, generating insights.");
                instructions.AddInstruction("2. Prioritize insights on strong differences or similarities between the oura data (measured) vs the wellness data (perceived).");
                instructions.AddInstruction("3. Do not follow up with the user, simply provide the insights.");
                lionMCPPrompt.Sections.Add(instructions);

                await lionMCPPrompt.AddOuraDataSectionAsync(_ouraService, dateRange);
                await lionMCPPrompt.AddWellnessDataSectionAsync(_wellnessService, dateRange);

                var chatHistory = lionMCPPrompt.ToChatMessage();
                var chatPrompt = lionMCPPrompt.ToStringPrompty();
                return Result.Success(chatPrompt);
            }
            else if (request.PromptType is not null && request.PromptType == "cb.02")
            {


                var lionMCPPrompt = new LionMcpPrompt { User = user };

                var taskSection = new InstructionPromptSection { Name = "Primary Task Description" };
                taskSection.AddInstruction("You are Lionheart, an intelligent training assistant that helps users manage their athletic performance, training plans, wellness, and recovery.");
                taskSection.AddInstruction("Please assist in creating a training program, populated with training sessions, movements, and set entries. You should design a training program based on the following parameters:");
                lionMCPPrompt.Sections.Add(taskSection);

                var parameters = new InstructionPromptSection { Name = "{Parameters}" };
                parameters.AddInstruction("1. The program should be designed to be 4 weeks long, with 3-5 training sessions per week.");
                parameters.AddInstruction("2. You should create this program to best help the user acheive their goals, noted next:");
                lionMCPPrompt.Sections.Add(parameters);

                var userGoals = new InstructionPromptSection { Name = "{User Goals}" };
                userGoals.AddInstruction("1. I am a 21 year old powerlfiter , looking to improve my squat, bench press, and deadlift.");
                lionMCPPrompt.Sections.Add(userGoals);


                var instructions = new InstructionPromptSection { Name = "{Instructions}" };
                instructions.AddInstruction("You can get the Identity User by calling a Get Identity User method, which intakes a string id you will be given.");

                instructions.AddInstruction("Create a training program using the tool. Store the training program id for later use in building the training sessions.");
                instructions.AddInstruction("You will need to fetch the movement bases to see the available exercises for use, a movement base is required within a movement.");
                instructions.AddInstruction("You should analyze the users goals and reason through creating a training program to help them reach their goals, built inside of the GeneratePopulatedTrainingProgramRequest object, JSON shown later.");

                instructions.AddInstruction("You will call an mcp server tool method: GeneratePopulatedTrainingProgramAsync, which will need to take in the IdentityUser and a GeneratePopulatedTrainingProgramRequest.");
                lionMCPPrompt.Sections.Add(instructions);

                var jsonSpecs = new InstructionPromptSection { Name = "{JSON Specifications}" };
                var gptprschema = NJsonSchema.JsonSchema.FromType<GeneratePopulatedTrainingProgramRequest>();
                jsonSpecs.AddInstruction(gptprschema.ToJson());
                var idenityUser = NJsonSchema.JsonSchema.FromType<IdentityUser>();
                jsonSpecs.AddInstruction(idenityUser.ToJson());
                lionMCPPrompt.Sections.Add(jsonSpecs);

                // var exampleJSON = new InstructionPromptSection { Name = "{Example GeneratePopulatedTrainingProgramRequest}" };
                // exampleJSON.AddInstruction("");
                // lionMCPPrompt.Sections.Add(exampleJSON);


                var chatPrompt = lionMCPPrompt.ToStringPrompty();
                return Result.Success(chatPrompt);
            }
            else if (request.PromptType is not null && request.PromptType == "genprog.03")
            {

                var lionMCPPrompt = new LionMcpPrompt { User = user };

                var taskSection = new InstructionPromptSection { Name = "Task Overview" };
                taskSection.AddInstruction("You are Lionheart, an intelligent training assistant that helps users manage athletic performance through structured, data-informed training programs.");
                taskSection.AddInstruction("Your task is to generate a *new* training program in JSON format, modeled on previous programs and tailored to the user’s current goals.");
                taskSection.AddInstruction("You will be provided with historical data, user goals, and structural requirements.");
                lionMCPPrompt.Sections.Add(taskSection);

                var parameters = new InstructionPromptSection { Name = "Program Design Parameters" };
                parameters.AddInstruction("1. The new program should be exactly **2 weeks long**.");
                parameters.AddInstruction("2. Each week should contain **3 structured training sessions**, evenly spaced if possible.");
                parameters.AddInstruction("3. This is a **powerlifting-focused program**, emphasizing the squat, bench press, and deadlift.");
                parameters.AddInstruction("4. Movements should be structured with associated sets and recommended RPE/reps/weights.");
                parameters.AddInstruction("5. All generated identifiers (TrainingProgramID, MovementID, etc.) must be valid **UUID/GUIDs**.");
                parameters.AddInstruction("6. Actual performance fields (e.g., `ActualReps`, `ActualWeight`, `ActualRPE`) should always be set to `0`.");
                lionMCPPrompt.Sections.Add(parameters);

                var userGoals = new InstructionPromptSection { Name = "User Goals & Profile" };
                userGoals.AddInstruction("1. The user is a **21-year-old male powerlifter**.");
                userGoals.AddInstruction("2. Their primary goal is to **increase performance in the Squat, Bench Press, and Deadlift**.");
                userGoals.AddInstruction("3. They are experienced and are looking for a well-structured, progressive program.");
                lionMCPPrompt.Sections.Add(userGoals);

                var instructions = new InstructionPromptSection { Name = "Instructions & Behavior" };
                instructions.AddInstruction("0. Fetch available movememnt bases from the MCP server to use in the program. You must use these (specifically the `MovementBaseID`) when creating movements. Do not use non-existent movement bases.");
                instructions.AddInstruction("1. Begin by analyzing the structure and content of past training programs to identify patterns.");
                instructions.AddInstruction("2. Use your reasoning to create a similar but newly generated program aligned with the user’s goals.");
                instructions.AddInstruction("3. Do not ask for clarification or user input — generate the final output directly.");
                instructions.AddInstruction("4. Return a **JSON object only**, following the schema from the example. Do not include explanations or surrounding commentary.");
                instructions.AddInstruction("5. Use the naming and structure patterns found in previous training programs as reference.");
                lionMCPPrompt.Sections.Add(instructions);

                var historicalProgramsSection = new InstructionPromptSection { Name = "📂 Historical Training Programs (Examples)" };
                var previousTrainingPrograms = await _trainingProgramService.GetTrainingProgramsAsync(user);
                historicalProgramsSection.AddInstruction(JsonSerializer.Serialize(previousTrainingPrograms.Value, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
                lionMCPPrompt.Sections.Add(historicalProgramsSection);

                var chatPrompt = lionMCPPrompt.ToStringPrompty();
                return Result.Success(chatPrompt);
            }
             else if (request.PromptType is not null && request.PromptType == "gensess.01")
            {

                var lionMCPPrompt = new LionMcpPrompt { User = user };

                var taskSection = new InstructionPromptSection { Name = "Task Overview" };
                taskSection.AddInstruction("You are Lionheart, an intelligent training assistant that helps users manage athletic performance through structured, data-informed training programs.");
                taskSection.AddInstruction("Your task is to generate a *new* training session in JSON format, modeled on previous sessions and tailored to the user’s current goals.");
                taskSection.AddInstruction("You will be provided with historical data, user goals, and structural requirements.");
                lionMCPPrompt.Sections.Add(taskSection);

                var parameters = new InstructionPromptSection { Name = "Program Design Parameters" };
                parameters.AddInstruction("1. The new session should be similar to existing sessions");
                parameters.AddInstruction("2. This is a **powerlifting-focused program**, emphasizing the squat, bench press, and deadlift.");
                parameters.AddInstruction("3. Movements should be structured with associated sets and recommended RPE/reps/weights.");
                parameters.AddInstruction("4. All generated identifiers (TrainingProgramID, MovementID, etc.) must be valid **UUID/GUIDs**.");
                parameters.AddInstruction("5. Actual performance fields (e.g., `ActualReps`, `ActualWeight`, `ActualRPE`) should always be set to `0`.");
                lionMCPPrompt.Sections.Add(parameters);

                var userGoals = new InstructionPromptSection { Name = "User Goals & Profile" };
                userGoals.AddInstruction("1. The user is a **21-year-old male powerlifter**.");
                userGoals.AddInstruction("2. Their primary goal is to **increase performance in the Squat, Bench Press, and Deadlift**.");
                userGoals.AddInstruction("3. They are experienced and are looking for a well-structured, progressive program.");
                lionMCPPrompt.Sections.Add(userGoals);

                var instructions = new InstructionPromptSection { Name = "Instructions & Behavior" };
                instructions.AddInstruction("0. Fetch available movememnt bases from the MCP server to use in the program. You must use these (specifically the `MovementBaseID`) when creating movements. Do not use non-existent movement bases.");
                instructions.AddInstruction("1. Begin by analyzing the structure and content of past training sessions to identify patterns.");
                instructions.AddInstruction("2. Use your reasoning to create a similar but newly generated session aligned with the user’s goals, to add to the existing program.");
                instructions.AddInstruction("3. Do not ask for clarification or user input — generate the final output directly.");
                instructions.AddInstruction("4. Return a **JSON object only**, following the schema from the example. Do not include explanations or surrounding commentary.");
                lionMCPPrompt.Sections.Add(instructions);

                var historicalProgramsSection = new InstructionPromptSection { Name = "📂 Historical Training Programs (Examples)" };
                var previousTrainingPrograms = await _trainingProgramService.GetTrainingProgramsAsync(user);
                historicalProgramsSection.AddInstruction(JsonSerializer.Serialize(previousTrainingPrograms.Value, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
                lionMCPPrompt.Sections.Add(historicalProgramsSection);

                var chatPrompt = lionMCPPrompt.ToStringPrompty();
                return Result.Success(chatPrompt);
            }
            else
            {
                return Result<string>.Error("Invalid prompt type specified.");
            }

        }
    }
}
