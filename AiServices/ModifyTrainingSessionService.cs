using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using OpenAI.Chat;
using Model.Tools;
using OpenAI;
using Ardalis.Result;
using lionheart.Model.Prompt;
using lionheart.Model.DTOs;
using System.Text.Json;
using k8s.KubeConfigModels;
using lionheart.Model.TrainingProgram;


namespace lionheart.Services.AI
{
    public interface IModifyTrainingSessionService
    {
        Task<Result<TrainingSessionDTO>> ModifySessionAsync(IdentityUser user, GetTrainingSessionRequest request);
    }
    /// <summary>
    /// This service is responsible for modifying training sessions.
    /// It uses the OpenAI API to generate modifications and execute tool calls as needed.
    /// </summary>
    public class ModifyTrainingSessionService : IModifyTrainingSessionService
    {
        private readonly ChatClient _chatClient;
        private readonly IToolCallExecutor _toolCallExecutor;
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly IMovementService _movementService;
        private readonly ISetEntryService _setEntryService;
        private readonly IOuraService _ouraService;
        private readonly IActivityService _activityService;
        private readonly IWellnessService _wellnessService;

        public ModifyTrainingSessionService(
            IConfiguration config,
            IToolCallExecutor toolCallExecutor,
            ITrainingProgramService trainingProgramService,
            ITrainingSessionService trainingSessionService,
            IMovementService movementService,
            ISetEntryService setEntryService,
            IOuraService ouraService,
            IActivityService activityService,
            IWellnessService wellnessService
        )
        {

            // TODO: Declare chat client in program.cs as (transient??) and inject it here 
            _chatClient = new(model: "gpt-4o", apiKey: config["OpenAI:ApiKey"]);
            _toolCallExecutor = toolCallExecutor;
            _trainingProgramService = trainingProgramService;
            _trainingSessionService = trainingSessionService;
            _movementService = movementService;
            _setEntryService = setEntryService;
            _ouraService = ouraService;
            _activityService = activityService;
            _wellnessService = wellnessService;
        }


        public async Task<Result<TrainingSessionDTO>> ModifySessionAsync(IdentityUser user, GetTrainingSessionRequest request)
        {
            // Duplicate the training session, modify the duplicate.
            var duplicateSessionResponse = await _trainingSessionService.DuplicateTrainingSessionAsync(user, request.TrainingSessionID);
            if (!duplicateSessionResponse.IsSuccess)
            {
                return Result<TrainingSessionDTO>.Error(duplicateSessionResponse.Errors.ToString());
            }
            var sessionToModify = duplicateSessionResponse.Value;
            
            var dateRange = new DateRangeRequest
            {
                StartDate = sessionToModify.Date.AddDays(-7),
                EndDate = sessionToModify.Date
            };


            var systemPrompt = PromptBuilder.ModifyTrainingSessionSys().Build();
            var userPrompt = "Modify the provided training session based on the user’s recent data and preferences. Use the provided tools for all modifications. Do not alter the session directly.";
            var userContextResult = await BuildUserContext(user, dateRange);
            if (!userContextResult.IsSuccess)
            {
                return Result<TrainingSessionDTO>.Error(userContextResult.Errors.ToString());
            }
            var tools = OpenAiToolRetriever.GetModifyTrainingSessionTools();
            List<ChatMessage> messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt),
                new UserChatMessage("User context: " + JsonSerializer.Serialize(userContextResult.Value)),
                new UserChatMessage("Training Program ID: " + sessionToModify.TrainingProgramID),
                new UserChatMessage("Session to modify[use trainingSessionID as UID]: " + JsonSerializer.Serialize(sessionToModify)),
            };
            ChatCompletionOptions options = new()
            {

            };
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            var response = await RunAiLoopAsync(messages, options, user);

            // Validate updated session exists, update its status to AIModified
            var getUpdatedSession = new GetTrainingSessionRequest
            {
                TrainingSessionID = sessionToModify.TrainingSessionID,
                TrainingProgramID = sessionToModify.TrainingProgramID
            };
            var updatedSession = await _trainingSessionService.GetTrainingSessionAsync(user, getUpdatedSession);
            if (!updatedSession.IsSuccess)
            {
                return Result<TrainingSessionDTO>.Error("Failed to retrieve updated session: " + string.Join(", ", updatedSession.Errors));
            }
            var updateSessionStatusToAIModified = new UpdateTrainingSessionRequest()
            {
                TrainingSessionID = updatedSession.Value.TrainingSessionID,
                TrainingProgramID = updatedSession.Value.TrainingProgramID,
                Status = TrainingSessionStatus.AIModified,
                Date = updatedSession.Value.Date,
                Notes = updatedSession.Value.Notes
            };
            var updateResponse = await _trainingSessionService.UpdateTrainingSessionAsync(user, updateSessionStatusToAIModified);
            if (!updateResponse.IsSuccess)
            {
                return Result<TrainingSessionDTO>.Error("Failed to update session status to AIModified: " + string.Join(", ", updateResponse.Errors));
            }
            return updateResponse;

        }

        private async Task<Result<string>> RunAiLoopAsync(List<ChatMessage> messages, ChatCompletionOptions options, IdentityUser user)
        {
            bool requiresAction;

            do
            {
                requiresAction = false;
                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        {
                            // Add the assistant message to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));

                            var content = completion.Content[0].Text;
                            if (content is null)
                            {
                                return Result<string>.Error("No content returned from AI model.");
                            }

                            return Result<string>.Success(content);
                        }

                    case ChatFinishReason.ToolCalls:
                        {
                            // First, add the assistant message with tool calls to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));

                            var toolCallResults = await _toolCallExecutor.ExecuteModifyTrainingSessionToolCallsAsync(completion.ToolCalls, user);
                            foreach (var result in toolCallResults)
                            {
                                if (!result.IsSuccess)
                                {
                                    return Result<string>.Error(result.Errors.ToString());
                                }
                                messages.Add(result.Value);
                            }

                            requiresAction = true;
                            break;
                        }

                    case ChatFinishReason.Length:
                        return Result<string>.Error(completion.FinishReason.ToString() + ": " + completion.Content.ToString());

                    case ChatFinishReason.ContentFilter:
                        return Result<string>.Error(completion.FinishReason.ToString() + ": " + completion.Content.ToString());
                    default:
                        return Result<string>.Error(completion.FinishReason.ToString() + ": " + completion.FinishReason.ToString());
                }
            } while (requiresAction);
            return Result<string>.Error("Unexpected completion finish reason");
        }

        private async Task<Result<string>> BuildUserContext(IdentityUser user, DateRangeRequest dateRange)
        {
            // Fetch activities
            var activitiesResult = await _activityService.GetActivitiesAsync(user, dateRange);
            if (!activitiesResult.IsSuccess)
            {
                return Result<string>.Error("Failed to fetch activities: " + string.Join(", ", activitiesResult.Errors));
            }

            // Fetch Oura data
            var ouraDataResult = await _ouraService.GetDailyOuraInfosAsync(user, dateRange);
            if (!ouraDataResult.IsSuccess)
            {
                return Result<string>.Error("Failed to fetch Oura data: " + string.Join(", ", ouraDataResult.Errors));
            }

            // Fetch wellness states
            var wellnessStatesResult = await _wellnessService.GetWellnessStatesAsync(user, dateRange);
            if (!wellnessStatesResult.IsSuccess)
            {
                return Result<string>.Error("Failed to fetch wellness states: " + string.Join(", ", wellnessStatesResult.Errors));
            }

            // Fetch training sessions
            var trainingSessionsResult = await _trainingSessionService.GetTrainingSessionsByDateRangeAsync(user, dateRange);
            if (!trainingSessionsResult.IsSuccess)
            {
                return Result<string>.Error("Failed to fetch training sessions: " + string.Join(", ", trainingSessionsResult.Errors));
            }

            // Aggregate data into a single object
            var userContext = new
            {
                Activities = activitiesResult.Value,
                OuraData = ouraDataResult.Value,
                WellnessStates = wellnessStatesResult.Value,
                TrainingSessions = trainingSessionsResult.Value
            };

            // Serialize the aggregated data into a JSON string
            return Result<string>.Success(JsonSerializer.Serialize(userContext));
        }



    }
}
