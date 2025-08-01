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
using Microsoft.Extensions.Caching.Memory;

namespace lionheart.Services.AI
{
    public interface IChatService
    {
        Task<Result<ChatResponse>> ProcessChatMessageAsync(IdentityUser user, ChatRequest request);
    }

    /// <summary>
    /// This service is responsible for handling chat interactions with the LLM.
    /// It maintains conversation history and provides context about the user's training data.
    /// </summary>
    public class ChatService : IChatService
    {
        private const string ConversationCacheKeyPrefix = "ChatConversation_";
        private readonly ChatClient _chatClient;
        private readonly IToolCallExecutor _toolCallExecutor;
        private readonly IMemoryCache _cache;
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly IMovementService _movementService;
        private readonly ISetEntryService _setEntryService;
        private readonly IOuraService _ouraService;
        private readonly IActivityService _activityService;
        private readonly IWellnessService _wellnessService;

        public ChatService(
            IConfiguration config,
            ChatClient chatClient,
            IToolCallExecutor toolCallExecutor,
            IMemoryCache cache,
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
            _cache = cache;
            _trainingProgramService = trainingProgramService;
            _trainingSessionService = trainingSessionService;
            _movementService = movementService;
            _setEntryService = setEntryService;
            _ouraService = ouraService;
            _activityService = activityService;
            _wellnessService = wellnessService;
        }

        private List<ChatMessage> GetOrInitConversation(IdentityUser user)
        {
            var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                return new List<ChatMessage>
                {
                    new SystemChatMessage(
                        "You are an athletic training assistant for the Lionheart application. " +
                        "Your purpose is to help users understand their training data, provide insights, " +
                        "and answer questions about their workouts, recovery, and overall fitness progress. " +
                        "You have access to the user's training programs, sessions, movements, set entries, " +
                        "Oura data, wellness scores, and activities. " +
                        "Be friendly, supportive, and provide actionable advice based on the user's data. " +
                        "When asked for specific metrics or data, use the appropriate tool functions to retrieve accurate information."
                    )
                };
            }) ?? new List<ChatMessage>();
        }

        public async Task<Result<ChatResponse>> ProcessChatMessageAsync(IdentityUser user, ChatRequest request)
        {
            try
            {
                // var messages = GetOrInitConversation(user);
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        "You are an athletic training assistant for the Lionheart application. " +
                        "Your purpose is to help users understand their training data, provide insights, " +
                        "and answer questions about their workouts, recovery, and overall fitness progress. " +
                        "You have access to the user's training programs, sessions, movements, set entries, " +
                        "Oura data, wellness scores, and activities. " +
                        "Be friendly, supportive, and provide actionable advice based on the user's data. " +
                        "Always output your responses as clearly structured plain text with no markdown formatting. " +
                        "Do not use asterisks, hashtags, or any markdown syntax. " +
                        "Use clear headings in all caps (e.g. WELLNESS SCORES), colons for labeling (e.g. Motivation: 3), and bullet points or numbered lists if needed. " +
                        "Avoid symbols that will not display properly in a browser." +
                        "The current date is " + DateTime.UtcNow.ToString("yyyy-MM-dd") + ". " +
                        "You can use the tools provided to access the user's Training Programs, Sessions, Movements, Set Entries, Oura Data, Wellness Scores, and Activities."
                    ),

                    // Add user message to conversation history
                    new UserChatMessage(request.Message)
                };

                // Create options for the AI
                var options = new ChatCompletionOptions();
                options.Temperature = 0.3f;

                // Add all the tools for retrieving user data
                var tools = await OpenAiToolRetriever.GetChatTools();
                foreach (var tool in tools)
                {
                    options.Tools.Add(tool);
                }
                UpdateModelTemperature(options, request);
                // Run the AI conversation loop
                var response = await RunAiLoopAsync(messages, options, user);
                if (!response.IsSuccess)
                {
                    return Result<ChatResponse>.Error(string.Join(", ", response.Errors));
                }

                // Update conversation cache
                var cacheKey = $"{ConversationCacheKeyPrefix}{user.Id}";
                _cache.Set(cacheKey, messages, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });

                // Return the chat response
                return Result<ChatResponse>.Success(new ChatResponse
                {
                    Response = response.Value
                });
            }
            catch (Exception ex)
            {
                return Result<ChatResponse>.Error($"Error processing chat message: {ex.Message}");
            }
        }

        private void UpdateModelTemperature(ChatCompletionOptions options, ChatRequest request)
        {
            if (request.CreativityLevel == 1) options.Temperature = 0.1f;
            else if (request.CreativityLevel == 2) options.Temperature = 0.3f;
            else if (request.CreativityLevel == 3) options.Temperature = 0.5f;
            else if (request.CreativityLevel == 4) options.Temperature = 0.7f;
            else if (request.CreativityLevel == 5) options.Temperature = 1.2f;
        }

        /// <summary>
        /// Run interaction with AI, handling tool calls and building the conversation history as ChatCompletions are received.
        /// </summary>
        private async Task<Result<string>> RunAiLoopAsync(
            List<ChatMessage> messages,
            ChatCompletionOptions options,
            IdentityUser user)
        {
            bool requiresAction;
            var numberFailedToolCalls = 0;

            do
            {
                requiresAction = false;
                ChatCompletion? completion = null;

                try
                {
                    completion = await _chatClient.CompleteChatAsync(messages, options);
                }
                catch (Exception ex)
                {
                    return Result<string>.Error($"Error from OpenAI API: {ex.Message}");
                }

                if (completion == null)
                {
                    return Result<string>.Error("Completion returned null from OpenAI API");
                }

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

                            var toolCallResults = await _toolCallExecutor.ExecuteChatToolCallsAsync(completion.ToolCalls, user);
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
                        return Result<string>.Error($"Response exceeded max length: {completion.Content}");

                    case ChatFinishReason.ContentFilter:
                        return Result<string>.Error($"Content filtered: {completion.Content}");

                    default:
                        return Result<string>.Error($"Unexpected finish reason: {completion.FinishReason}");
                }
            } while (requiresAction);

            return Result<string>.Error("Unexpected completion finish reason");
        }
    }
}
