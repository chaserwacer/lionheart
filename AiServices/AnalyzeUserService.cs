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
    public interface IAnalyzeUserService
    {
        Task<Result<string>> AnalyzeUserState(IdentityUser user, DateOnly request);
    }
    /// <summary>
    /// This service is responsible for modifying training sessions.
    /// It uses the OpenAI API to generate modifications and execute tool calls as needed.
    /// </summary>
    public class AnalyzeUserService : IAnalyzeUserService
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

        public AnalyzeUserService(
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

     
        public async Task<Result<string>> AnalyzeUserState(IdentityUser user, DateOnly request)
        {


            var dateRange = new DateRangeRequest
            {
                StartDate = request.AddDays(-7),
                EndDate = request
            };

            // First Prompt
            var analyzeUserSystem = PromptBuilder.AnalyzeUserWellness().Build();
            var userContextResult = await BuildUserContext(user, dateRange);
            if (!userContextResult.IsSuccess)
            {
                return Result<string>.Error(userContextResult.Errors.ToString());
            }

            List<ChatMessage> chatMessages = new List<ChatMessage>
            {
                new SystemChatMessage(analyzeUserSystem),
                new UserChatMessage("User context: " + JsonSerializer.Serialize(userContextResult.Value)),

            };

            ChatCompletionOptions options = new();
            options.Temperature = .2f;
            return await RunAiLoopAsync(chatMessages, options, user);
           


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
                                        return Result<string>.Error("Too many failed tool calls: " + string.Join(", ", result.ToolChatMessage.Content));
                                    }
                                }
                                messages.Add(result.ToolChatMessage);
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
