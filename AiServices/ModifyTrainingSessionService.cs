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
        Task<Result<TrainingSessionDTO>> ModifySessionAsync(IdentityUser user, ModifyTrainingSessionWithAIRequest request);
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
            ChatClient chatClient,
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
            // _chatClient = new(model: "gpt-4o", apiKey: config["OpenAI:ApiKey"]);

            _chatClient = chatClient;
            _toolCallExecutor = toolCallExecutor;
            _trainingProgramService = trainingProgramService;
            _trainingSessionService = trainingSessionService;
            _movementService = movementService;
            _setEntryService = setEntryService;
            _ouraService = ouraService;
            _activityService = activityService;
            _wellnessService = wellnessService;
        }

        /// <summary>
        /// Modifies a training session based on user context and AI-generated suggestions.
        /// It duplicates the session, modifies it using AI, and updates the session status to AIModified.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<Result<TrainingSessionDTO>> ModifySessionAsync(IdentityUser user, ModifyTrainingSessionWithAIRequest request)
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

            // First Prompt
            var initialPrompt = PromptBuilder.AnalyzeUserWellness().Build();
            var userContextResult = await BuildUserContext(user, dateRange);
            if (!userContextResult.IsSuccess)
            {
                return Result<TrainingSessionDTO>.Error(userContextResult.Errors.ToString());
            }

            List<ChatMessage> firstPassMessages = new List<ChatMessage>
            {
                new UserChatMessage(initialPrompt),
                new UserChatMessage("User context: " + JsonSerializer.Serialize(userContextResult.Value)),

            };

            ChatCompletionOptions options = new();

            var firstResponse = await RunAiLoopAsync(firstPassMessages, options, user);
            if (!firstResponse.IsSuccess)
            {
                await _trainingSessionService.DeleteTrainingSessionAsync(user, sessionToModify.TrainingSessionID);
                return Result<TrainingSessionDTO>.Error("AI analysis failed: " + firstResponse.Errors.ToString());
            }

            // Second Prompt
            var secondPrompt = PromptBuilder.ModifyTrainingSession().Build();
            List<ChatMessage> secondPassMessages = new()
            {
                new SystemChatMessage(secondPrompt),
                new UserChatMessage("UserContextSummary:\n" + firstResponse.Value),
                new UserChatMessage("UserPrompt: " + request.UserPrompt),
                new UserChatMessage("Training Program ID: " + sessionToModify.TrainingProgramID),
                new UserChatMessage("Session to modify[use trainingSessionID as UID]: " + JsonSerializer.Serialize(sessionToModify)),
                new UserChatMessage("Available movement bases: " + JsonSerializer.Serialize(await _movementService.GetMovementBasesAsync(user))),
                new UserChatMessage("Available equipments: " + JsonSerializer.Serialize(await _movementService.GetEquipmentsAsync(user))),
            };
            var tools = await OpenAiToolRetriever.GetModifyTrainingSessionTools();

            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            var response = await RunAiLoopAsync(secondPassMessages, options, user);
            if (!response.IsSuccess)
            {
                await _trainingSessionService.DeleteTrainingSessionAsync(user, sessionToModify.TrainingSessionID);
                return Result<TrainingSessionDTO>.Error("AI modification failed: " + response.Errors.ToString());
            }
            var updateResponse = await ValidateSessionAndPolish(sessionToModify, user);
            return updateResponse;

        }
        /// <summary>
        /// Run interaction with AI, handling tool calls and building the conversation history as <see cref="ChatCompletion"/>s are received.
        /// </summary>
        /// <param name="messages">Conversation history/messages/prompts exchanged with the AI</param>
        /// <param name="options">Item containing tools available for AI use</param>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task<Result<string>> RunAiLoopAsync(List<ChatMessage> messages, ChatCompletionOptions options, IdentityUser user)
        {
            bool requiresAction;
            var numberFailedToolCalls = 0;
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
                                    numberFailedToolCalls++;
                                    if (numberFailedToolCalls > 5)
                                    {
                                        return Result<string>.Error("Too many failed tool calls: " + string.Join(", ", result.Errors));
                                    }
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

        /// <summary>
        /// Validates the modified session exists, update its <see cref="TrainingSessionDTO.Status"/> to <see cref="TrainingSessionStatus.AIModified"/>.
        /// If something fails, the modified session is deleted.
        /// </summary>
        /// <param name="sessionToModify"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task<Result<TrainingSessionDTO>> ValidateSessionAndPolish(TrainingSessionDTO sessionToModify, IdentityUser user)
        {

            // Validate updated session exists, update its status to AIModified
            var getUpdatedSession = new GetTrainingSessionRequest
            {
                TrainingSessionID = sessionToModify.TrainingSessionID,
                TrainingProgramID = sessionToModify.TrainingProgramID
            };
            var updatedSession = await _trainingSessionService.GetTrainingSessionAsync(user, getUpdatedSession);
            if (!updatedSession.IsSuccess)
            {
                await _trainingSessionService.DeleteTrainingSessionAsync(user, sessionToModify.TrainingSessionID);
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

            return Result<TrainingSessionDTO>.Success(updateResponse.Value);
        }
    }
}
