using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using lionheart.Model.DTOs;
using Model.McpServer;

namespace lionheart.Services.AI
{
    public interface IToolCallExecutor
    {
        Task<object?> ExecuteAsync(string functionName, JsonElement argsJson, IdentityUser user);
    }

    public class ToolCallExecutor : IToolCallExecutor
    {
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly IMovementService _movementService;
        private readonly ISetEntryService _setEntryService;

        public ToolCallExecutor(
            ITrainingProgramService trainingProgramService,
            ITrainingSessionService trainingSessionService,
            IMovementService movementService,
            ISetEntryService setEntryService)
        {
            _trainingProgramService = trainingProgramService;
            _trainingSessionService = trainingSessionService;
            _movementService = movementService;
            _setEntryService = setEntryService;
        }

        public async Task<object?> ExecuteAsync(string functionName, JsonElement argsJson, IdentityUser user)
        {
            try
            {
                switch (functionName)
                {
                    case "CreateTrainingProgramAsync":
                        var progReq = argsJson.GetProperty("request").Deserialize<CreateTrainingProgramRequest>();
                        return await _trainingProgramService.CreateTrainingProgramAsync(user, progReq!);

                    case "UpdateTrainingProgramAsync":
                        var updateProg = argsJson.GetProperty("request").Deserialize<UpdateTrainingProgramRequest>();
                        return await _trainingProgramService.UpdateTrainingProgramAsync(user, updateProg!);

                    case "DeleteTrainingProgramAsync":
                        var deleteProgId = argsJson.GetProperty("trainingProgramId").GetGuid();
                        return await _trainingProgramService.DeleteTrainingProgramAsync(user, deleteProgId);

                    case "CreateTrainingSessionAsync":
                        var sessReq = argsJson.GetProperty("request").Deserialize<CreateTrainingSessionRequest>();
                        return await _trainingSessionService.CreateTrainingSessionAsync(user, sessReq!);

                    case "UpdateTrainingSessionAsync":
                        var updateSess = argsJson.GetProperty("request").Deserialize<UpdateTrainingSessionRequest>();
                        return await _trainingSessionService.UpdateTrainingSessionAsync(user, updateSess!);

                    case "DeleteTrainingSessionAsync":
                        var deleteSessId = argsJson.GetProperty("trainingSessionID").GetGuid();
                        return await _trainingSessionService.DeleteTrainingSessionAsync(user, deleteSessId);

                    case "CreateMovementAsync":
                        var moveReq = argsJson.GetProperty("request").Deserialize<CreateMovementRequest>();
                        return await _movementService.CreateMovementAsync(user, moveReq!);

                    case "UpdateMovementAsync":
                        var updateMove = argsJson.GetProperty("request").Deserialize<UpdateMovementRequest>();
                        return await _movementService.UpdateMovementAsync(user, updateMove!);

                    case "DeleteMovementAsync":
                        var deleteMoveId = argsJson.GetProperty("movementId").GetGuid();
                        return await _movementService.DeleteMovementAsync(user, deleteMoveId);

                    case "CreateSetEntryAsync":
                        var setReq = argsJson.GetProperty("request").Deserialize<CreateSetEntryRequest>();
                        return await _setEntryService.CreateSetEntryAsync(user, setReq!);

                    case "UpdateSetEntryAsync":
                        var updateSet = argsJson.GetProperty("request").Deserialize<UpdateSetEntryRequest>();
                        return await _setEntryService.UpdateSetEntryAsync(user, updateSet!);

                    case "DeleteSetEntryAsync":
                        var deleteSetId = argsJson.GetProperty("setEntryId").GetGuid();
                        return await _setEntryService.DeleteSetEntryAsync(user, deleteSetId);

                    default:
                        throw new InvalidOperationException($"Unknown tool function: {functionName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tool execution error ({functionName}): {ex.Message}");
                return null;
            }
        }
    }
}
