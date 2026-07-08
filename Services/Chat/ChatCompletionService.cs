using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Chat;
using lionheart.Services.Chat;
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
                var completionResult = (await _chatClient.CompleteChatAsync(messages, request.Options)).ToResult();
                if (completionResult.IsError())
                {
                    return Result.Error(string.Join("; ", completionResult.Errors));
                }
                ChatCompletion completion = completionResult.Value;

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        {
                            newlyGeneratedMessages.Add(new LHModelChatMessage{
                                ChatMessageItemID = Guid.NewGuid(),
                                ChatConversationID = request.Conversation.ChatConversationID,
                                CreationTime = DateTime.UtcNow,
                                TokenCount = completion.Usage.OutputTokenCount,
                                Content = string.Join("", completion.Content.Select(c => c.Text))
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
                            /*
                            In order to save token usage, we will only persist the data the model retreived from tool calls.
                            This means we will not save the intermediate assistant messages that request tool calls.
                            */


                            // add assistant message with tool calls to the conversation history.
                            // dont save this for persistence.
                            messages.Add(new AssistantChatMessage(completion));
                     
                            // add a new tool message for each tool call that is resolved.
                            foreach (ChatToolCall toolCall in completion.ToolCalls)
                            {
                                var toolCallResult = await _toolCallExecutor.ExecuteToolCallAsync(toolCall, user);
                                if (toolCallResult.IsSuccess)
                                {
                                    messages.Add(toolCallResult.Value);
                                    newlyGeneratedMessages.Add(new LHChatToolCallResult{
                                        ChatMessageItemID =  Guid.NewGuid(),
                                        ChatConversationID = request.Conversation.ChatConversationID,
                                        CreationTime = DateTime.UtcNow,
                                        TokenCount = (toolCallResult.Value.Content?[0].Text ?? string.Empty).Length / 4,
                                        Content = toolCallResult.Value.Content?[0].Text,
                                    });   
                                }
                                else
                                {
                                    messages.Add(new ToolChatMessage(toolCall.Id, toolCallResult.Errors.ToString()));
                                    
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


            // The system prompt now carries the Athlete Context Card, so its budget is reserved first
            // (it is never packed/dropped here). History then fills MAX_INPUT_TOKENS minus the card.
            var tokenCount = systemPrompt.TokenCount;
            var selected = new List<LHChatMessage>();

            // Pass 1: prioritize conversational continuity — pack user/model turns newest-first.
            foreach (var message in conversation.GetUserModelMessagesInChronologicalOrder()
                         .OrderByDescending(m => m.CreationTime))
            {
                if (tokenCount + message.TokenCount > MAX_INPUT_TOKENS)
                {
                    continue;
                }
                selected.Add(message);
                tokenCount += message.TokenCount;
            }

            // Pass 2: fill any remaining budget with persisted tool-call results, newest-first.
            // These are the largest, stalest payloads and the baseline they captured is already
            // represented in the card, so they are the first thing dropped when space is tight.
            // (They remain in the DB for audit; they just stop being re-sent.)
            foreach (var message in conversation.ToolMessages.OrderByDescending(m => m.CreationTime))
            {
                if (tokenCount + message.TokenCount > MAX_INPUT_TOKENS)
                {
                    continue;
                }
                selected.Add(message);
                tokenCount += message.TokenCount;
            }

            // Restore chronological order and prepend the (card-bearing) system prompt.
            var compressedChatConversation = selected
                .OrderBy(m => m.CreationTime)
                .Select(m => m.ToChatMessage())
                .ToList();
            compressedChatConversation.Insert(0, systemPrompt.ToChatMessage());
            return Result.Success(compressedChatConversation);
           
        }
            
    }
}