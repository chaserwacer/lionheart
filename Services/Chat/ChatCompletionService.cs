using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Chat;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Completion;
using Model.Tools;
using OpenAI.Chat;

namespace Services.Chat
{
    /// <summary>
    /// Interface defining behavior for a chat completion service.
    /// This service is responsible for generating a LLM chat completion with custom settings and conversation history.
    /// </summary>
    public interface IChatCompletionService
    {
        /// <summary>
        /// Generate chat completion based on request data.
        /// </summary>
        Task<Result<ChatCompletionResponse>> GenerateChatCompletionAsync(IdentityUser user, ChatCompletionRequest request);
    }
    public class ChatCompletionService(ChatClient chatClient, ChatToolCallExecutor toolCallExecutor) : IChatCompletionService
    {
        private const int CONTENT_WINDOWN_TOKENS = 400_000;
        private const int TOKEN_BUFFER = CONTENT_WINDOWN_TOKENS / 10;
        private const int MAX_ALLOWED_TOKENS = CONTENT_WINDOWN_TOKENS - TOKEN_BUFFER;
        private const int MAX_INPUT_TOKENS = MAX_ALLOWED_TOKENS / 4;

        private readonly ChatClient _chatClient = chatClient;
        private readonly ChatToolCallExecutor _toolCallExecutor = toolCallExecutor;

        /// <summary>
        /// Generates a chat completion for the given user and request.
        /// </summary>
        public async Task<Result<ChatCompletionResponse>> GenerateChatCompletionAsync(IdentityUser user, ChatCompletionRequest request)
        {
            bool requiresAction ;

            var conversationHistoryMessagesResult = HandleConversationHistory(request.Conversation);
            if (conversationHistoryMessagesResult.IsError())
            {
                return Result.Error(conversationHistoryMessagesResult.Errors.ToString());
            }
            var messages = conversationHistoryMessagesResult.Value;
            var newlyGeneratedMessages = new List<LHChatMessage>();

            do
            {
                requiresAction = false;
                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, request.Options);
    
                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        {
                            newlyGeneratedMessages.Add(new LHModelChatMessage{
                                ChatMessageItemID = Guid.NewGuid(),
                                ChatConversationID = request.Conversation.ChatConversationID,
                                CreationTime = DateTime.UtcNow,
                                TokenCount = completion.Usage.OutputTokenCount,
                                Content = completion.Content.ToString()
                            });
                            return Result.Success(new ChatCompletionResponse
                            {
                                Completion = completion,
                                CompletionGeneratedMessages = newlyGeneratedMessages,
                                ModelFinalChatMessageID = newlyGeneratedMessages.Last().ChatMessageItemID
                            });
                        }

                    case ChatFinishReason.ToolCalls:
                        {
                            // add assistant message with tool calls to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));
                            newlyGeneratedMessages.Add(new LHModelChatMessage{
                                ChatMessageItemID = Guid.NewGuid(),
                                ChatConversationID = request.Conversation.ChatConversationID,
                                CreationTime = DateTime.UtcNow,
                                TokenCount = completion.Usage.OutputTokenCount,
                                Content = completion.Content.ToString(),
                                ToolCalls = completion.ToolCalls
                            });

                            // add a new tool message for each tool call that is resolved.
                            foreach (ChatToolCall toolCall in completion.ToolCalls)
                            {
                                var toolCallResult = await _toolCallExecutor.ExecuteToolCallAsync(toolCall, user);
                                if (toolCallResult.IsSuccess)
                                {
                                    messages.Add(toolCallResult.Value);
                                    newlyGeneratedMessages.Add(new LHToolChatMessage{
                                        ChatMessageItemID =  Guid.NewGuid(),
                                        ChatConversationID = request.Conversation.ChatConversationID,
                                        CreationTime = DateTime.UtcNow,
                                        TokenCount = (toolCallResult.Value.Content?.ToString() ?? string.Empty).Length / 4,
                                        Content = toolCallResult.Value.Content?.ToString(),
                                        ToolCallID = toolCallResult.Value.ToolCallId
                                    });   
                                }
                                else
                                {
                                    messages.Add(new ToolChatMessage(toolCall.Id, toolCallResult.Errors.ToString()));
                                    var errorContent = toolCallResult.Errors.ToString() ?? "Unknown error during tool call.";
                                    newlyGeneratedMessages.Add(new LHToolChatMessage{
                                        ChatMessageItemID =  Guid.NewGuid(),
                                        ChatConversationID = request.Conversation.ChatConversationID,
                                        CreationTime = DateTime.UtcNow,
                                        TokenCount = errorContent.Length / 4,
                                        Content = errorContent,
                                        ToolCallID = toolCallResult.Value.ToolCallId
                                    });
                                }
                            }

                            requiresAction = true;
                            break;
                        }

                    case ChatFinishReason.Length:
                        return Result.Error("Incomplete model output due to MaxTokens parameter or token limit exceeded.");
                    case ChatFinishReason.ContentFilter:
                        return Result.Error("Omitted content due to a content filter flag.");
                    default:
                        return Result.Error(completion.FinishReason.ToString());
                }
            } while (requiresAction);
            return Result.Error("Failed to generate chat completion.");
        }

        /// <summary>
        /// Given a chat conversation, process and return the list of chat messages to be used in the completion.
        /// This includes handling conversation history compression if needed.
        /// </summary>
        private Result<List<ChatMessage>> HandleConversationHistory(LHChatConversation conversation)
        {
            var systemPrompt = conversation.ChatSystemMessage;
            if (systemPrompt.TokenCount > MAX_INPUT_TOKENS)
            {
                return Result.Error("System prompt exceeds maximum allowed tokens.");   
            }


            var tokenCount = systemPrompt.TokenCount;
            var compressedChatConversation = new List<ChatMessage>();


            var messagesNewToOld = conversation.GetAllNonSystemMessagesInChronologicalOrder().OrderByDescending(m => m.CreationTime).ToList();
            foreach (var message in messagesNewToOld)
            {
                if (tokenCount + message.TokenCount > MAX_INPUT_TOKENS)
                {
                    break;
                }
                compressedChatConversation.Add(message.ToChatMessage());
                tokenCount += message.TokenCount;
            }

            compressedChatConversation.Reverse();
            compressedChatConversation.Insert(0, systemPrompt.ToChatMessage());
            return Result.Success(compressedChatConversation);
           
        }
            
    }
}