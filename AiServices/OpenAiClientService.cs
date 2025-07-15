using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using lionheart.Model.DTOs;
using System.Text;
using Model.McpServer;

namespace lionheart.Services.AI
{
    public class OpenAiClientService : IOpenAiClientService
    {
        private readonly IChatClient _chatClient;
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly IMovementService _movementService;
        private readonly ISetEntryService _setEntryService;

        public OpenAiClientService(
            IConfiguration config,
            ITrainingProgramService trainingProgramService,
            ITrainingSessionService trainingSessionService,
            IMovementService movementService,
            ISetEntryService setEntryService)
        {
            _chatClient = new OpenAIClient(config["OpenAI:ApiKey"])
                .GetChatClient("gpt-4o")
                .AsIChatClient();

            _trainingProgramService = trainingProgramService;
            _trainingSessionService = trainingSessionService;
            _movementService = movementService;
            _setEntryService = setEntryService;
        }

        public async Task<string> ChatSimpleAsync(string prompt)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "You are Lionheart, an intelligent training assistant."),
                new(ChatRole.User, prompt)
            };

            await foreach (var response in _chatClient.GetStreamingResponseAsync(messages))
            {
                if (response.FinishReason == ChatFinishReason.Stop)
                    return response.Message?.Content ?? "";
            }

            return "";
        }

        public async Task<string> ChatWithToolsAsync(string prompt, IdentityUser user)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "You are Lionheart, an intelligent training assistant."),
                new(ChatRole.User, prompt)
            };

            await foreach (var response in _chatClient.GetStreamingResponseAsync(
                messages,
                OpenAiFunctionTools.All))
            {
                if (response.FinishReason == ChatFinishReason.ToolCalls)
                {
                    var toolCalls = response.ToolCalls;
                    if (toolCalls == null || toolCalls.Count == 0)
                        break;

                    // only supporting first tool call for now
                    var toolCall = toolCalls[0];
                    var functionName = toolCall.Name;
                    var argsJson = toolCall.Arguments.ToString();

                    var toolResult = await ExecuteToolCallAsync(functionName, argsJson!, user);

                    var toolMessage = new List<ChatMessage>
                    {
                        new(ChatRole.System, "You are Lionheart, an intelligent training assistant."),
                        new(ChatRole.User, prompt),
                        new ChatMessage(ChatRole.Assistant, toolCalls: new List<ToolCall> { toolCall }),
                        new(ChatRole.Tool, toolCall.Id, JsonSerializer.Serialize(toolResult))
                    };

                    await foreach (var followUp in _chatClient.GetStreamingResponseAsync(toolMessage))
                    {
                        if (followUp.FinishReason == ChatFinishReason.Stop)
                            return followUp.Message?.Content ?? "";
                    }

                    break;
                }

                if (response.FinishReason == ChatFinishReason.Stop)
                    return response.Message?.Content ?? "";
            }

            return "";
        }

        private async Task<object?> ExecuteToolCallAsync(string functionName, string argsJson, IdentityUser user)
        {
            try
            {
                switch (functionName)
                {
                    case "createTrainingProgram":
                        var progReq = JsonSerializer.Deserialize<CreateTrainingProgramRequest>(argsJson);
                        return await _trainingProgramService.CreateTrainingProgramAsync(user, progReq!);

                    case "createTrainingSession":
                        var sessReq = JsonSerializer.Deserialize<CreateTrainingSessionRequest>(argsJson);
                        return await _trainingSessionService.CreateTrainingSessionAsync(user, sessReq!);

                    case "createMovement":
                        var moveReq = JsonSerializer.Deserialize<CreateMovementRequest>(argsJson);
                        return await _movementService.CreateMovementAsync(user, moveReq!);

                    case "createSetEntry":
                        var setReq = JsonSerializer.Deserialize<CreateSetEntryRequest>(argsJson);
                        return await _setEntryService.CreateSetEntryAsync(user, setReq!);

                    case "getIdentityUser":
                        return user;

                    case "getMovementBases":
                        return await _movementService.GetMovementBasesAsync();

                    case "getTrainingPrograms":
                        return await _trainingProgramService.GetTrainingProgramsAsync(user);

                    case "getTrainingSessions":
                        var tsReq = JsonSerializer.Deserialize<GetTrainingSessionsRequest>(argsJson);
                        return await _trainingSessionService.GetTrainingSessionsAsync(user, tsReq!.TrainingProgramID);

                    default:
                        throw new InvalidOperationException($"Unknown tool function: {functionName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tool call error: {ex.Message}");
                return null;
            }
        }
    }
}
