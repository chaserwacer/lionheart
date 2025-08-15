using Microsoft.AspNetCore.Identity;
using OpenAI.Chat;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json;
using lionheart.Services;
using lionheart.Model.DTOs;
using Ardalis.Result;
using lionheart.Converters;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json; // if not already at top
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using OpenAI.Responses;          // OpenAIResponseClient, OpenAIResponse, ResponseItem, ...
using System.Linq;               // FirstOrDefault (when reading message content safely)
using System.Text;





/// <summary>
/// This class implements the IToolCallExecutor interface and handles the execution of tool calls. 
/// The primary entry point is <see cref="ExecuteToolCallsAsync"/>.
/// </summary>
public class ToolCallExecutor : IToolCallExecutor
{
    private readonly ITrainingSessionService _trainingSessionService;
    private readonly IMovementService _movementService;
    private readonly ISetEntryService _setEntryService;
    private readonly ITrainingProgramService _trainingProgramService;
    private readonly IOuraService _ouraService;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IWellnessService _wellnessService;
    private readonly IActivityService _activityService;
    #pragma warning disable OPENAI001
    private readonly OpenAIResponseClient _responses;

    // Static base options shared across clones
    private static readonly JsonSerializerOptions _baseJsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() } // ✅ Ensures enums like "Kilograms" deserialize
    };

    // Clone and add DateOnly converter
    private static JsonSerializerOptions CloneWithDateOnlySupport(JsonSerializerOptions original)
    {
        var clone = new JsonSerializerOptions(original);
        clone.Converters.Add(new DateOnlyJsonConverter("yyyy-MM-dd"));
        return clone;
    }
    private class WebSearchArgs { public string Query { get; set; } = ""; }


    public ToolCallExecutor(
        ITrainingSessionService trainingSessionService,
        IMovementService movementService,
        ISetEntryService setEntryService,
        ITrainingProgramService trainingProgramService,
        IOuraService ouraService,
        IActivityService activityService,
        IWellnessService wellnessService,
        OpenAIResponseClient responses)
    {
        _trainingSessionService = trainingSessionService;
        _movementService = movementService;
        _setEntryService = setEntryService;
        _trainingProgramService = trainingProgramService;
        _ouraService = ouraService;
        _activityService = activityService;
        _wellnessService = wellnessService;
        _responses = responses;

        // ✅ Clone base options with DateOnly support
        _jsonOptions = CloneWithDateOnlySupport(_baseJsonOptions);
    }

    /// <summary>
    /// Intakes a list of tool calls and executes them sequentially.
    /// If any tool call fails, it returns a single error result.
    /// </summary>
    /// <param name="toolCalls"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<List<Result<ToolChatMessage>>> ExecuteToolCallsAsync(IReadOnlyList<ChatToolCall> toolCalls, IdentityUser user)
    {
        var results = new List<Result<ToolChatMessage>>();

        // ✅ List of tool functions safe to call in parallel
        var parallelizable = new[] { "GetMovementBasesAsync", "GetEquipmentsAsync", "GetTrainingProgramAsync", "WebSearchAsync" };

        var parallelCalls = toolCalls.Where(tc => parallelizable.Contains(tc.FunctionName)).ToList();
        var sequentialCalls = toolCalls.Where(tc => !parallelizable.Contains(tc.FunctionName)).ToList();

        // ✅ Run info tools in parallel
        var parallelTasks = parallelCalls.Select(async tc =>
        {
            var result = await ExecuteToolAsync(tc, user);
            return (tc, result);
        });

        var parallelResults = await Task.WhenAll(parallelTasks);

        foreach (var (tc, result) in parallelResults)
        {
            if (!result.IsSuccess)
                return new List<Result<ToolChatMessage>> { result };

            results.Add(result);
        }

        // ✅ Run creation tools in order
        foreach (var tc in sequentialCalls)
        {
            var result = await ExecuteToolAsync(tc, user);
            if (!result.IsSuccess)
                return new List<Result<ToolChatMessage>> { result };

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Intakes a list of tool calls and executes them sequentially.
    /// If any tool call fails, it returns a single error result.
    /// </summary>
    /// <param name="toolCalls"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task<List<ToolCallResponse>> ExecuteModifyTrainingSessionToolCallsAsync(IReadOnlyList<ChatToolCall> toolCalls, IdentityUser user)
    {
        var results = new List<ToolCallResponse>();
        foreach (var toolCall in toolCalls)
        {
            var result = await ExecuteModifyTrainingSessionTools(toolCall, user);
            results.Add(result);
            
        }
        return results;
    }

    /// <summary>
    /// Executes a web search tool call.
    /// </summary>
    /// <param name="tc"></param>
    /// <returns></returns>
    private async Task<Result<ToolChatMessage>> ExecuteWebSearchAsync(ChatToolCall tc)
    {
        var fallback = "Search the web for powerlifting programming guidelines.";

        var query = GetSafeQueryOrDefault(tc, fallback);

        OpenAIResponse response = await _responses.CreateResponseAsync(
            userInputText: query,
            new ResponseCreationOptions
            {
                Tools = { ResponseTool.CreateWebSearchTool() }
            }
        );
        var sb = new StringBuilder();
        foreach (ResponseItem item in response.OutputItems)
        {

            if (item is WebSearchCallResponseItem ws)
            {
                // Optional: diagnostic line
                sb.AppendLine($"[web_search:{ws.Status}] {ws.Id}");
            }
            else if (item is MessageResponseItem msg)
            {
                var text = msg.Content?.FirstOrDefault()?.Text;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text);
            }
        }

        return Result<ToolChatMessage>.Success(
            new ToolChatMessage(toolCallId: tc.Id, content: sb.ToString().Trim())
        );
    }

        private static string GetSafeQueryOrDefault(ChatToolCall tc, string defaultQuery)
        {
            try
            {
                var funcArgsStr = tc.FunctionArguments.ToString();
                if (string.IsNullOrWhiteSpace(funcArgsStr) || funcArgsStr.Trim() == "{}")
                    return defaultQuery;

                using var doc = JsonDocument.Parse(tc.FunctionArguments);
                if (doc.RootElement.TryGetProperty("query", out var qProp))
                {
                    var q = qProp.GetString();
                    return string.IsNullOrWhiteSpace(q) ? defaultQuery : q!;
                }
                return defaultQuery;
            }
            catch
            {
                return defaultQuery;
            }
        }



    /// <summary>
    /// Private helper method.
    /// Executes a single tool call based on its function name and arguments.
    /// It handles various tool functions related to training sessions, movements, and set entries.
    /// </summary>
    /// <param name="toolCall"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    private async Task<Result<ToolChatMessage>> ExecuteToolAsync(ChatToolCall toolCall, IdentityUser user)
    {
        var fn = toolCall.FunctionName;
        var args = JsonNode.Parse(toolCall.FunctionArguments);

        try
        {
            switch (fn)
            {
                case "WebSearchAsync":
                    return await ExecuteWebSearchAsync(toolCall);

                case "CreateTrainingSessionWeekAsync":
                    {
                        var request = args?["request"]?.Deserialize<CreateTrainingSessionWeekRequest>(_jsonOptions);
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for CreateTrainingSessionWeekAsync.");

                        var result = await _trainingSessionService.CreateTrainingSessionWeekAsync(user, request);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }

                case "GetTrainingSessionAsync":
                    {
                        var request = args?["request"]?.Deserialize<GetTrainingSessionRequest>();
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for GetTrainingSession.");
                        var result = await _trainingSessionService.GetTrainingSessionAsync(user, request);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "GetTrainingProgramAsync":
                    {
                        var request = args?["request"]?.Deserialize<GetTrainingProgramRequest>();
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for GetTrainingProgram.");
                        var result = await _trainingProgramService.GetTrainingProgramAsync(user, request.TrainingProgramID);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "CreateTrainingSessionAsync":
                    {
                        var request = args?["request"]?.Deserialize<CreateTrainingSessionRequest>(_jsonOptions);
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for CreateTrainingSession.");
                        var result = await _trainingSessionService.CreateTrainingSessionAsync(user, request);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "CreateTrainingProgramAsync":
                    {
                        // Deserialize the *root* JSON object…
                        var request = args?.Deserialize<CreateTrainingProgramRequest>(_jsonOptions);
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Invalid arguments for CreateTrainingProgramAsync.");

                        var result = await _trainingProgramService.CreateTrainingProgramAsync(user, request);
                        return Result<ToolChatMessage>.Success(
                        new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result))
                        );
                    }

                case "UpdateTrainingSessionAsync":
                    {
                        var request = args?["request"]?.Deserialize<UpdateTrainingSessionRequest>();
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for UpdateTrainingSession.");
                        var result = await _trainingSessionService.UpdateTrainingSessionAsync(user, request);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "CreateMovementAsync":
                    {
                        var request = args?["request"]?.Deserialize<CreateMovementRequest>();
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for CreateMovement.");
                        var result = await _movementService.CreateMovementAsync(user, request);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "UpdateMovementAsync":
                    {
                        var request = args?["request"]?.Deserialize<UpdateMovementRequest>();
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for UpdateMovement.");
                        var result = await _movementService.UpdateMovementAsync(user, request);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "DeleteMovementAsync":
                    {
                        var idStr = args?["movementId"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var id))
                            return Result<ToolChatMessage>.Error("Missing or invalid movementId.");
                        var result = await _movementService.DeleteMovementAsync(user, id);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "CreateSetEntryAsync":
                    {
                        var request = args?["request"]?.Deserialize<CreateSetEntryRequest>();
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for CreateSetEntry.");
                        var result = await _setEntryService.CreateSetEntryAsync(user, request);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "UpdateSetEntryAsync":
                    {
                        var request = args?["request"]?.Deserialize<UpdateSetEntryRequest>();
                        if (request == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid request for UpdateSetEntry.");
                        var result = await _setEntryService.UpdateSetEntryAsync(user, request);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "DeleteSetEntryAsync":
                    {
                        var idStr = args?["setEntryId"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var id))
                            return Result<ToolChatMessage>.Error("Missing or invalid setEntryId.");
                        var result = await _setEntryService.DeleteSetEntryAsync(user, id);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "GetMovementBasesAsync":
                    {
                        var result = await _movementService.GetMovementBasesAsync(user);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "GetDailyOuraInfoAsync":
                    {
                        var dateStr = args?["date"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(dateStr) || !DateOnly.TryParse(dateStr, out var date))
                            return Result<ToolChatMessage>.Error("Missing or invalid arguments for GetDailyOuraInfoAsync.");

                        var result = await _ouraService.GetDailyOuraInfoAsync(user, date);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }

                case "GetDailyOuraInfosAsync":
                    {
                        var dateRange = args?["dateRange"]?.Deserialize<DateRangeRequest>();
                        if (dateRange == null)
                            return Result<ToolChatMessage>.Error("Missing or invalid arguments for GetDailyOuraInfosAsync.");

                        var result = await _ouraService.GetDailyOuraInfosAsync(user, dateRange);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                case "GetEquipmentsAsync":
                    {
                        var result = await _movementService.GetEquipmentsAsync(user);
                        return Result<ToolChatMessage>.Success(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                default:
                    return Result<ToolChatMessage>.Error($"Tool function '{fn}' is not implemented.");
            }
        }
        catch (Exception ex)
        {
            return Result<ToolChatMessage>.Error($"Exception in {fn}: {ex.Message}");
        }
    }
    /// <summary>
    /// Private helper method.
    /// Executes a single tool call based on its function name and arguments.
    /// Used for <see cref="ModifyTrainingSessionService"/>
    /// </summary>
    /// <param name="toolCall"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    private async Task<ToolCallResponse> ExecuteModifyTrainingSessionTools(ChatToolCall toolCall, IdentityUser user)
    {
        var fn = toolCall.FunctionName;
        var args = toolCall.FunctionArguments.ToString();

        try
        {
            switch (fn)
            {
                case "GetTrainingSessionAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingSession.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<GetTrainingSessionRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingSession.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _trainingSessionService.GetTrainingSessionAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "UpdateTrainingSessionAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for UpdateTrainingSession.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<UpdateTrainingSessionRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for UpdateTrainingSession.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _trainingSessionService.UpdateTrainingSessionAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "CreateMovementAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for CreateMovement.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<CreateMovementRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for CreateMovement.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _movementService.CreateMovementAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "UpdateMovementAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for UpdateMovement.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<UpdateMovementRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for UpdateMovement.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _movementService.UpdateMovementAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "DeleteMovementAsync":
                    {
                        var idStr = JsonSerializer.Deserialize<JsonObject>(args);
                        if (idStr == null)
                        {
                            var errorMsg = "Missing or invalid movementId.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        if (!Guid.TryParse(idStr.ToJsonString(), out var id))
                        {
                            var errorMsg = "Missing or invalid movementId.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _movementService.DeleteMovementAsync(user, id);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "CreateSetEntryAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for CreateSetEntry.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<CreateSetEntryRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for CreateSetEntry.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _setEntryService.CreateSetEntryAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "UpdateSetEntryAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for UpdateSetEntry.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<UpdateSetEntryRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for UpdateSetEntry.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _setEntryService.UpdateSetEntryAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "DeleteSetEntryAsync":
                    {
                        var idStr = JsonSerializer.Deserialize<JsonObject>(args);
                        if (idStr == null || !Guid.TryParse(idStr.ToJsonString(), out var id))
                        {
                            var errorMsg = "Missing or invalid setEntryId.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _setEntryService.DeleteSetEntryAsync(user, id);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                default:
                    {
                        var errorMsg = $"Tool function '{fn}' is not implemented.";
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                    }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Exception in {fn}: {ex.Message}";
            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
        }
    }

    /// <summary>
    /// Intakes a list of tool calls and executes them sequentially.
    /// Used for chat interface tools from <see cref="OpenAiToolRetriever.GetChatTools"/>
    /// </summary>
    /// <param name="toolCalls">List of chat tool calls to execute</param>
    /// <param name="user">The current user</param>
    /// <returns>List of tool call responses</returns>
    public async Task<List<ToolCallResponse>> ExecuteChatToolCallsAsync(IReadOnlyList<ChatToolCall> toolCalls, IdentityUser user)
    {
        var results = new List<ToolCallResponse>();
        foreach (var toolCall in toolCalls)
        {
            var result = await ExecuteChatToolsAsync(toolCall, user);
            results.Add(result);
        }
        return results;
    }

    /// <summary>
    /// Private helper method.
    /// Executes a single chat tool call based on its function name and arguments.
    /// Used for tools from <see cref="OpenAiToolRetriever.GetChatTools"/>
    /// </summary>
    /// <param name="toolCall">The tool call to execute</param>
    /// <param name="user">The current user</param>
    /// <returns>A tool call response</returns>
    private async Task<ToolCallResponse> ExecuteChatToolsAsync(ChatToolCall toolCall, IdentityUser user)
    {
        var fn = toolCall.FunctionName;
        var args = toolCall.FunctionArguments.ToString();

        try
        {
            switch (fn)
            {
                // Training Program tools
                case "GetTrainingProgramAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingProgram.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<GetTrainingProgramRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingProgram.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _trainingProgramService.GetTrainingProgramAsync(user, request.TrainingProgramID);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "GetTrainingProgramsAsync":
                    {
                        var result = await _trainingProgramService.GetTrainingProgramsAsync(user);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }

                // Training Session tools
                case "GetTrainingSessionAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingSession.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<GetTrainingSessionRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingSession.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _trainingSessionService.GetTrainingSessionAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "GetTrainingSessionsByDateRangeAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingSessionsByDateRange.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<DateRangeRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingSessionsByDateRange.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _trainingSessionService.GetTrainingSessionsByDateRangeAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "GetTrainingSessionsAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetTrainingSessions.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var programIdStr = requestNode["trainingProgramID"]?.ToString();
                        if (string.IsNullOrEmpty(programIdStr) || !Guid.TryParse(programIdStr, out var programId))
                        {
                            var errorMsg = "Missing or invalid trainingProgramID for GetTrainingSessions.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _trainingSessionService.GetTrainingSessionsAsync(user, programId);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }

                // Movement tools
                case "GetMovementsAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetMovements.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var sessionIdStr = requestNode["sessionId"]?.ToString();
                        if (string.IsNullOrEmpty(sessionIdStr) || !Guid.TryParse(sessionIdStr, out var sessionId))
                        {
                            var errorMsg = "Missing or invalid sessionId for GetMovements.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _movementService.GetMovementsAsync(user, sessionId);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "GetMovementBasesAsync":
                    {
                        var result = await _movementService.GetMovementBasesAsync(user);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }
                case "GetEquipmentsAsync":
                    {
                        var result = await _movementService.GetEquipmentsAsync(user);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }

                // Wellness tools
                case "GetWellnessStatesAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetWellnessStates.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<DateRangeRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for GetWellnessStates.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _wellnessService.GetWellnessStatesAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }

                // Oura tools
                case "GetDailyOuraInfosAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetDailyOuraInfos.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<DateRangeRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for GetDailyOuraInfos.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _ouraService.GetDailyOuraInfosAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }

                // Activity tools
                case "GetActivitiesAsync":
                    {
                        var requestNode = JsonSerializer.Deserialize<JsonObject>(args);
                        if (requestNode == null)
                        {
                            var errorMsg = "Missing or invalid request for GetActivities.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var request = JsonSerializer.Deserialize<DateRangeRequest>(requestNode.ToJsonString(), _jsonOptions);
                        if (request == null)
                        {
                            var errorMsg = "Missing or invalid request for GetActivities.";
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        var result = await _activityService.GetActivitiesAsync(user, request);
                        if (!result.IsSuccess)
                        {
                            var errorMsg = string.Join(", ", result.Errors);
                            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
                        }
                        return new ToolCallResponse(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result.Value)), true);
                    }

                default:
                    return new ToolCallResponse(new ToolChatMessage(toolCall.Id, $"Tool function '{fn}' is not implemented."), false);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Exception in {fn}: {ex.Message}";
            return new ToolCallResponse(new ToolChatMessage(toolCall.Id, errorMsg), false);
        }
    }
}
