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
using OllamaSharp;


namespace lionheart.Services.AI
{
    public interface IChatService
    {
        Task<Result<ChatConversationDTO>> ProcessChatMessageAsync(IdentityUser user, ChatRequest request);
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
        private readonly IChatConversationService _chatConversationService;

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
            IWellnessService wellnessService,
            IChatConversationService chatConversationService
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
            _chatConversationService = chatConversationService;
        }


        public async Task<Result<ChatConversationDTO>> ProcessChatMessageAsync(IdentityUser user, ChatRequest request)
        {
            var chatConversationResult = await _chatConversationService.GetChatConversationModelVersionAsync(user, request.ChatConversationId);
            if (!chatConversationResult.IsSuccess || chatConversationResult.Value is null)
            {
                return Result<ChatConversationDTO>.Error((ErrorList)chatConversationResult.Errors);
            }
            var chatConversation = chatConversationResult.Value;

            var handleSystemMessage = await HandleSystemMessageAsync(chatConversation, request, user);
            if (!handleSystemMessage.IsSuccess)
            {
                return Result<ChatConversationDTO>.Error((ErrorList)handleSystemMessage.Errors);
            }

            var userChatMessage = new UserChatMessage(request.Message);
            var userMessageRequest = new AddChatMessageRequest
            {
                ChatConversationId = chatConversation.ChatConversationId,
                ChatMessage = userChatMessage
            };
            var addUserMessageResult = await _chatConversationService.AddChatMessageAsync(user, userMessageRequest);
            if (!addUserMessageResult.IsSuccess)
            {
                return Result<ChatConversationDTO>.Error((ErrorList)addUserMessageResult.Errors);
            }

            chatConversationResult = await _chatConversationService.GetChatConversationModelVersionAsync(user, request.ChatConversationId);
            if (!chatConversationResult.IsSuccess || chatConversationResult.Value is null)
            {
                return Result<ChatConversationDTO>.Error((ErrorList)chatConversationResult.Errors);
            }
            chatConversation = chatConversationResult.Value;



            var options = new ChatCompletionOptions();
            var tools = await OpenAiToolRetriever.GetChatTools();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }
            var aiInteractionResponse = await RunAiLoopAsync(options, user, chatConversation);
            if (!aiInteractionResponse.IsSuccess)
            {
                return Result<ChatConversationDTO>.Error(string.Join(", ", aiInteractionResponse.Errors));
            }

            return await _chatConversationService.GetChatConversationAsync(user, chatConversation.ChatConversationId);

        }

        /// <summary>
        /// Add the <see cref="SystemChatMessage"/> to the chat conversation if it doesn't exist.
        /// </summary>
        private async Task<Result> HandleSystemMessageAsync(ChatConversation chatConversation, ChatRequest request, IdentityUser user)
        {
            if (chatConversation.Messages.Count == 0)
            {

                var systemChatMessage = new SystemChatMessage("You are an athletic training assistant for the Lionheart application. " +
                    "Your purpose is to help users understand their training data, provide insights, " +
                    "and answer questions about their workouts, recovery, and overall fitness progress. " +
                    "You have access to the user's training programs, sessions, movements, set entries, " +
                    "Oura data, wellness scores, and activities. " +
                    "Be friendly, supportive, and provide actionable advice based on the user's data. " +
                    "Always output your responses as clearly structured plain text with no markdown formatting. " +
                    "Do not use asterisks, hashtags, or any markdown syntax. " +
                    "Avoid symbols that will not display properly in a browser." +
                    "The current date is " + DateTime.UtcNow.ToString("yyyy-MM-dd") + ". ");
                var systemChatRequest = new AddChatMessageRequest
                {
                    ChatConversationId = chatConversation.ChatConversationId,
                    ChatMessage = systemChatMessage
                };
                var result = await _chatConversationService.AddChatMessageAsync(user, systemChatRequest);
                if (!result.IsSuccess)
                {
                    return Ardalis.Result.Result.Error((ErrorList)result.Errors);
                }
                return Result.Success();
            }
            return Result.Success();
        }


        /// <summary>
        /// Run interaction with AI, handling tool calls and building the conversation history as ChatCompletions are received.
        /// </summary>
        private async Task<Result<string>> RunAiLoopAsync(

            ChatCompletionOptions options,
            IdentityUser user, ChatConversation chatConversation)
        {
                // Ensure stable chronological order before sending to OpenAI
                var messages = new List<ChatMessage>();
                foreach (var message in chatConversation.Messages.OrderBy(m => m.CreationTime))
                {
                    messages.Add(message.ChatMessage);
                }
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
                // Defer assistant message persistence to switch cases to preserve proper tool-calls chaining

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        {
                            var content = completion.Content?.Count > 0 ? completion.Content[0]?.Text : null;
                            if (string.IsNullOrWhiteSpace(content))
                            {
                                return Result<string>.Error("No content returned from AI model.");
                            }

                            // Persist final assistant text message
                            var finalAssistant = new AssistantChatMessage(content);
                            messages.Add(finalAssistant);
                            var saveFinalReq = new AddChatMessageRequest
                            {
                                ChatConversationId = chatConversation.ChatConversationId,
                                ChatMessage = finalAssistant,
                            };
                            var saveFinalRes = await _chatConversationService.AddChatMessageAsync(user, saveFinalReq);
                            if (!saveFinalRes.IsSuccess)
                            {
                                return Result<string>.Error("Failed to save message to chat conversation: " + string.Join(", ", saveFinalRes.Errors));
                            }

                            return Result<string>.Success(content);
                        }

                    case ChatFinishReason.ToolCalls:
                        {
                            var assistantCallMsg = new AssistantChatMessage(completion);
                            messages.Add(assistantCallMsg);
                            // var saveAssistantCallReq = new AddChatMessageRequest
                            // {
                            //     ChatConversationId = chatConversation.ChatConversationId,
                            //     ChatMessage = assistantCallMsg,
                            // };
                            // var saveAssistantCallRes = await _chatConversationService.AddChatMessageAsync(user, saveAssistantCallReq);
                            // if (!saveAssistantCallRes.IsSuccess)
                            // {
                            //     return Result<string>.Error("Failed to save assistant tool-call message: " + string.Join(", ", saveAssistantCallRes.Errors));
                            // }

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
                                // persist tool message into conversation
                                // var toolMsgReq = new AddChatMessageRequest
                                // {
                                //     ChatConversationId = chatConversation.ChatConversationId,
                                //     ChatMessage = result.ToolChatMessage,
                                // };
                                // var saveToolMsg = await _chatConversationService.AddChatMessageAsync(user, toolMsgReq);
                                // if (!saveToolMsg.IsSuccess)
                                // {
                                //     return Result<string>.Error("Failed to save tool message: " + string.Join(", ", saveToolMsg.Errors));
                                // }
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
