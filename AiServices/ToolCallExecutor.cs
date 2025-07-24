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

public ToolCallExecutor(
    ITrainingSessionService trainingSessionService,
    IMovementService movementService,
    ISetEntryService setEntryService,
    ITrainingProgramService trainingProgramService,
        IOuraService ouraService)
{
    _trainingSessionService = trainingSessionService;
    _movementService = movementService;
    _setEntryService = setEntryService;
    _trainingProgramService = trainingProgramService;
        _ouraService = ouraService;

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
        var parallelizable = new[] { "GetMovementBasesAsync", "GetEquipmentsAsync" };

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
                        if ( dateRange == null)
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
}

