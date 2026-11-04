using System.Diagnostics;
using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Services.Llm;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Completion;
using Model.Chat.Tools;
using Model.Tools;

namespace Services.Chat
{
    /// <summary>
    /// Runs one exchange with the chat model: offers it the tool registry, resolves each round of
    /// tool calls it asks for, and stops when it answers.
    /// </summary>
    public interface IChatCompletionService
    {
        Task<Result<ChatCompletionResponse, Problem>> GenerateChatCompletionAsync(
            IdentityUser user, ChatCompletionRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The tool loop, and the only place that decides how many rounds a model gets and what a
    /// failed tool is told.
    /// </summary>
    /// <remarks>
    /// Provider-agnostic by construction: everything it says to a model it says through
    /// <see cref="IChatModel"/>, so the same loop drives Anthropic and a local LM Studio server.
    /// </remarks>
    public class ChatCompletionService(
        IChatModel chatModel,
        ToolRegistry toolRegistry,
        ChatToolCallExecutor toolCallExecutor) : IChatCompletionService
    {
        private const int CONTENT_WINDOWN_TOKENS = 400_000;
        private const int TOKEN_BUFFER = CONTENT_WINDOWN_TOKENS / 10;
        private const int MAX_ALLOWED_TOKENS = CONTENT_WINDOWN_TOKENS - TOKEN_BUFFER;
        private const int MAX_INPUT_TOKENS = MAX_ALLOWED_TOKENS / 4;

        /// <summary>
        /// How many times the model may ask for tools before the exchange is abandoned. A model
        /// that keeps calling tools without ever answering would otherwise bill a request per round
        /// indefinitely.
        /// </summary>
        private const int MAX_TOOL_ROUNDS = 12;

        private readonly IChatModel _chatModel = chatModel;
        private readonly ToolRegistry _toolRegistry = toolRegistry;
        private readonly ChatToolCallExecutor _toolCallExecutor = toolCallExecutor;

        public Task<Result<ChatCompletionResponse, Problem>> GenerateChatCompletionAsync(
            IdentityUser user, ChatCompletionRequest request, CancellationToken cancellationToken = default) =>
            Prompt(request.Conversation)
                .BindAsync(prompt => lionheart.Functional.Result.UnfoldAsync(
                    Exchange.Opening(request.Conversation.ChatConversationID, prompt),
                    (exchange, ct) => AdvanceAsync(user, request, exchange, ct),
                    cancellationToken));

        /// <summary>
        /// One round: ask the model, and either finish with its answer or run what it asked for and
        /// carry the conversation forward.
        /// </summary>
        private Task<Result<Loop<Exchange, ChatCompletionResponse>, Problem>> AdvanceAsync(
            IdentityUser user, ChatCompletionRequest request, Exchange exchange, CancellationToken cancellationToken) =>
            // Not Upstream: nothing upstream failed, this application gave up. Upstream would reach
            // the client as 502 "an upstream service failed", which tells the athlete to blame the
            // model for a limit chosen here. Nothing is persisted at the cap, so the conversation
            // survives intact and the turn can be asked again.
            exchange.Rounds >= MAX_TOOL_ROUNDS
                ? Task.FromResult(Result<Loop<Exchange, ChatCompletionResponse>, Problem>.Failure(
                    new Problem.Unexpected(
                        $"The chat model asked for tools {MAX_TOOL_ROUNDS} times without answering.")))
                : _chatModel
                    .RespondAsync(
                        ChatTurnRequest.Conversation(
                            exchange.SystemPrompt,
                            exchange.Messages,
                            _toolRegistry.GetAllToolDefinitions(),
                            request.MaxOutputTokens) with { Briefing = request.Briefing },
                        cancellationToken)
                    .BindAsync(turn => ActOnAsync(user, exchange, turn, cancellationToken));

        private async Task<Result<Loop<Exchange, ChatCompletionResponse>, Problem>> ActOnAsync(
            IdentityUser user,
            Exchange exchange,
            ModelTurn turn,
            CancellationToken cancellationToken)
        {
            switch (turn)
            {
                case ModelTurn.Answered answered:
                    return Result<Loop<Exchange, ChatCompletionResponse>, Problem>.Success(
                        new Loop<Exchange, ChatCompletionResponse>.Done(exchange.Concluded(answered)));

                case ModelTurn.RequestedTools requested:
                    var outcomes = await RunToolsAsync(user, requested, cancellationToken);
                    return Result<Loop<Exchange, ChatCompletionResponse>, Problem>.Success(
                        new Loop<Exchange, ChatCompletionResponse>.Continue(
                            exchange.After(requested, outcomes)));

                default:
                    throw new UnreachableException();
            }
        }

        /// <summary>
        /// Every tool of one round, in order. Total rather than fallible: a tool that refuses does
        /// not end the exchange, it answers with its reason instead, which is the model's only cue
        /// to retry with different arguments or answer without it.
        /// </summary>
        private async Task<IReadOnlyList<ToolOutcome>> RunToolsAsync(
            IdentityUser user,
            ModelTurn.RequestedTools requested,
            CancellationToken cancellationToken)
        {
            var outcomes = new List<ToolOutcome>(requested.Calls.Count);

            foreach (var call in requested.Calls)
            {
                outcomes.Add(await _toolCallExecutor
                    .ExecuteToolCallAsync(call, user, allowedToolNames: null, cancellationToken)
                    .MatchAsync(
                        produced => produced,
                        refused => ToolOutcome.Refused(call.CallId, DescribeToolFailure(refused))));
            }

            return outcomes;
        }

        /// <summary>
        /// What the model is told when a tool call fails. It is the model's only signal to
        /// recover — retry with different arguments, or answer without the tool — so it has to
        /// say what went wrong.
        /// </summary>
        private static string DescribeToolFailure(Problem problem) =>
            string.IsNullOrWhiteSpace(problem.Description)
                ? "Tool call failed."
                : $"Tool call failed: {problem.Description}";

        /// <summary>
        /// The conversation history packed to fit the input budget, with the system prompt's own
        /// budget reserved first.
        /// </summary>
        private static Result<ChatPrompt, Problem> Prompt(LHChatConversation conversation)
        {
            var systemPrompt = conversation.ChatSystemMessage;
            if (systemPrompt.TokenCount > MAX_INPUT_TOKENS)
            {
                return Result<ChatPrompt, Problem>.Failure(
                    new Problem.Unexpected("System prompt exceeds maximum allowed tokens."));
            }

            // The system prompt carries the Athlete Context Card, so its budget is reserved first
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

            return Result<ChatPrompt, Problem>.Success(new ChatPrompt(
                systemPrompt.Content ?? string.Empty,
                [.. selected.OrderBy(m => m.CreationTime).Select(AsTurn)]));
        }

        /// <summary>
        /// A stored message as the model should hear it again.
        /// </summary>
        private static ModelMessage AsTurn(LHChatMessage message) => message switch
        {
            LHUserChatMessage user => new ModelMessage.UserSaid(user.Content ?? string.Empty),
            LHModelChatMessage model => new ModelMessage.AssistantSaid(model.Content ?? string.Empty),

            // Replayed as an assistant statement rather than a tool result: the request that asked
            // for it is deliberately not stored, and a tool result whose request is absent is
            // rejected by every provider. See LHChatToolCallResult.
            LHChatToolCallResult tool => new ModelMessage.AssistantSaid(tool.Content ?? string.Empty),

            _ => throw new UnreachableException()
        };

        /// <summary>The unchanging half of every round: the system prompt and the packed history.</summary>
        private sealed record ChatPrompt(string SystemPrompt, IReadOnlyList<ModelMessage> History);

        /// <summary>
        /// What one exchange carries from round to round: the conversation as the model will next
        /// see it, the messages worth persisting, and how many rounds have gone by.
        /// </summary>
        private sealed record Exchange(
            Guid ConversationId,
            string SystemPrompt,
            IReadOnlyList<ModelMessage> Messages,
            IReadOnlyList<LHChatMessage> Generated,
            int Rounds)
        {
            public static Exchange Opening(Guid conversationId, ChatPrompt prompt) =>
                new(conversationId, prompt.SystemPrompt, prompt.History, [], 0);

            /// <summary>
            /// The next round's state: the model's request for tools and the results answering it
            /// are appended to the conversation, and the results alone are kept for persistence.
            /// </summary>
            public Exchange After(ModelTurn.RequestedTools requested, IReadOnlyList<ToolOutcome> outcomes) =>
                this with
                {
                    Messages =
                    [
                        .. Messages,
                        new ModelMessage.AssistantActed(requested.Turn),
                        new ModelMessage.ToolsAnswered(outcomes),
                    ],
                    // A failed call is told to the model in this exchange and then forgotten. It is
                    // not stored, because stored messages are replayed as history on later turns,
                    // where "that tool failed" is noise about a call the athlete never saw.
                    Generated = [.. Generated, .. outcomes.Where(o => !o.Failed).Select(Persisted)],
                    Rounds = Rounds + 1,
                };

            /// <summary>The model's answer, plus every tool result the exchange collected on the way.</summary>
            public ChatCompletionResponse Concluded(ModelTurn.Answered answered)
            {
                var message = new LHModelChatMessage
                {
                    ChatMessageItemID = Guid.NewGuid(),
                    ChatConversationID = ConversationId,
                    CreationTime = DateTime.UtcNow,
                    TokenCount = answered.OutputTokens,
                    Content = answered.Text,
                };

                return new ChatCompletionResponse
                {
                    CompletionGeneratedMessages = [.. Generated, message],
                    ModelFinalChatMessageID = message.ChatMessageItemID,
                };
            }

            /// <summary>
            /// A tool result, stored. The token count is an estimate because a tool result is this
            /// application's own text, not something the model reported usage for.
            /// </summary>
            private LHChatToolCallResult Persisted(ToolOutcome outcome) => new()
            {
                ChatMessageItemID = Guid.NewGuid(),
                ChatConversationID = ConversationId,
                CreationTime = DateTime.UtcNow,
                TokenCount = outcome.Content.Length / 4,
                Content = outcome.Content,
            };
        }
    }
}
