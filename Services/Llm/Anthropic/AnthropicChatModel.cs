using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Helpers;
using lionheart.Functional;
using Messages = Anthropic.Models.Messages;

namespace lionheart.Services.Llm.Anthropic;

/// <summary>
/// The port over Anthropic's Messages API — the production provider, against the owner's own
/// API key.
/// </summary>
/// <remarks>
/// The whole of this application's knowledge of the Anthropic SDK lives in this file, including
/// which of its typed exceptions mean what. Three things about the current models shape the code
/// here more than anything else:
/// <list type="bullet">
/// <item>
/// <b>Forced tool use is gone.</b> A <c>tool_choice</c> of <c>any</c> or <c>tool</c> is a 400, so
/// the old trick of forcing a tool call to get JSON back is not available. Structured output is
/// asked for with <see cref="Messages.JsonOutputFormat"/> instead — see <see cref="Output"/>.
/// </item>
/// <item>
/// <b>Thinking is always on</b> and cannot be disabled or budgeted; both are a 400. So a response
/// routinely carries thinking blocks the answer is not in, and depth is asked for through
/// <c>OutputConfig.Effort</c> rather than switched off. This is the same trap LM Studio sets with
/// <c>reasoning_content</c>, in Anthropic's spelling.
/// </item>
/// <item>
/// <b>There is no <c>ToParam()</c>.</b> Echoing a turn back means rebuilding every block as its
/// <c>*Param</c> counterpart, and a thinking block's signature has to survive unaltered or the
/// request is rejected — see <see cref="Replay"/>.
/// </item>
/// </list>
/// </remarks>
public sealed class AnthropicChatModel(AnthropicClient client, string model, ILogger logger) : IChatModel
{
    private readonly AnthropicClient _client = client;
    private readonly string _model = model;
    private readonly ILogger _logger = logger;

    public Task<Result<ModelTurn, Problem>> RespondAsync(
        ChatTurnRequest request, CancellationToken cancellationToken = default) =>
        Transcribe(request)
            .BindAsync(messages => SendAsync(Parameters(request, messages), cancellationToken))
            .BindAsync(ReadTurn);

    /// <remarks>
    /// The stream is read through the SDK's own aggregator, so the turn it ends with is the
    /// <see cref="Messages.Message"/> a request read whole would have been, and is read by the same
    /// <see cref="ReadTurn"/>: the stop reason first, a tool call's blocks kept whole with their
    /// signatures. Only text deltas are handed on; thinking is never shown to the athlete.
    /// </remarks>
    public Task<Result<ModelTurn, Problem>> StreamAsync(
        ChatTurnRequest request, Action<string> written, CancellationToken cancellationToken = default) =>
        Transcribe(request)
            .BindAsync(messages => StreamAsync(Parameters(request, messages), written, cancellationToken))
            .BindAsync(ReadTurn);

    private Messages.MessageCreateParams Parameters(
        ChatTurnRequest request, IReadOnlyList<Messages.MessageParam> messages)
    {
        var parameters = new Messages.MessageCreateParams
        {
            Model = _model,
            MaxTokens = request.MaxOutputTokens,
            System = new Messages.MessageCreateParamsSystem(request.SystemPrompt, null),
            Messages = messages,
            OutputConfig = Output(request),
        };

        // Added only when tools are actually on offer: the SDK serialises an empty collection as
        // "tools": [], and a request that offers none should not carry the field at all.
        var offered = request.Tools.Count == 0
            ? parameters
            : parameters with { Tools = [.. request.Tools.Select(Describe)] };

        // A round after a tool call also caches its tail, so the next round of the same exchange,
        // seconds later, reads the tool results back instead of paying for them again.
        return (request.Caching, request.Messages is [.., ModelMessage.ToolsAnswered]) switch
        {
            (PromptCaching.None, _) => offered,
            (PromptCaching.Conversation, true) => offered with { CacheControl = RoundCache },
            (PromptCaching.Conversation, false) => offered,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>
    /// The breakpoint on the athlete's latest words: everything up to them is the conversation as
    /// it will be resent next time, and it is kept for an hour.
    /// </summary>
    /// <remarks>
    /// An hour rather than the default five minutes, because the athlete comes back to a
    /// conversation after ten or forty minutes far more often than within five, and the hour's
    /// dearer write (2x input against 1.25x) is repaid by the first read inside it. Only this prefix
    /// earns the hour. A tool round's request and results are replayed later as plain text, not in
    /// the shape this exchange sent them, so nothing after the latest words is ever read again
    /// once the exchange ends.
    /// </remarks>
    private static readonly Messages.CacheControlEphemeral HistoryCache = new() { Ttl = Messages.Ttl.Ttl1h };

    /// <summary>
    /// Automatic caching for a tool loop's tail, at the default five minutes: the API moves the
    /// breakpoint to the last block, and the next round reads it within seconds. It follows the
    /// hour-long breakpoint, which is the order the API requires of mixed lifetimes.
    /// </summary>
    private static readonly Messages.CacheControlEphemeral RoundCache = new();

    /// <summary>
    /// How the answer is asked for. A schema goes in <c>OutputConfig.Format</c>, never through a
    /// forced tool call, and <c>Effort</c> is nested here rather than sitting at the top level.
    /// </summary>
    // Standard depth is left unset so the model's own default applies; Minimal asks for the
    // shallowest reasoning the API offers, since thinking cannot be turned off at all.
    //
    // Temperature is never sent, which is why extraction pins the depth instead: it cannot be
    // combined with thinking, and thinking is always on. Adding one to make a parse more
    // deterministic would make every parse a 400.
    private static Messages.OutputConfig Output(ChatTurnRequest request) =>
        (request.Output, request.Reasoning) switch
        {
            (OutputContract.JsonObject json, ReasoningDepth.Minimal) => new()
            {
                Format = new Messages.JsonOutputFormat { Schema = Schema(json.SchemaJson) },
                Effort = Messages.Effort.Low,
            },
            (OutputContract.JsonObject json, _) => new()
            {
                Format = new Messages.JsonOutputFormat { Schema = Schema(json.SchemaJson) },
            },
            (_, ReasoningDepth.Minimal) => new() { Effort = Messages.Effort.Low },
            _ => new(),
        };

    /// <summary>
    /// The schema as the API wants it: the JSON Schema object's own members, not a string holding
    /// them. The same schema text both providers are given, read one level down.
    /// </summary>
    private static Dictionary<string, JsonElement> Schema(string schemaJson)
    {
        using var document = JsonDocument.Parse(schemaJson);
        return document.RootElement.EnumerateObject()
            .ToDictionary(member => member.Name, member => member.Value.Clone());
    }

    /// <summary>
    /// One tool, described by the schema this application generated for it.
    /// </summary>
    /// <remarks>
    /// The whole schema object is sent, not a rebuilt subset of it. A tool parameter with a nested
    /// complex type — <c>GetInjuryRequest.DateRange</c> is the live example — makes NJsonSchema
    /// emit a <c>$ref</c> into a root <c>definitions</c> block, so sending only <c>properties</c>
    /// and <c>required</c> ships a reference that resolves to nothing. <c>FromRawUnchecked</c> is
    /// the SDK's escape hatch for exactly this: a JSON Schema has more members than the typed
    /// shape names, and the application's schema is already the authority.
    /// <para>
    /// <c>$schema</c> is the one member dropped. NJsonSchema declares the draft-04 dialect, and a
    /// dialect this API never asked about is the kind of meta-keyword a strict validator rejects
    /// outright — while saying nothing about the shape of a parameter, which is the only thing the
    /// model needs. Omitting it leaves the dialect implicit, and the structure here reads the same
    /// in every draft. Everything that describes the shape, <c>definitions</c> included, is sent.
    /// </para>
    /// </remarks>
    private static Messages.ToolUnion Describe(ToolDefinition tool) =>
        new(new Messages.Tool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = Messages.InputSchema.FromRawUnchecked(
                Schema(tool.ParametersJson)
                    .Where(member => member.Key is not "$schema")
                    .ToDictionary(member => member.Key, member => member.Value)),
        },
        null);

    /// <summary>
    /// The history, in the two shapes the Messages API insists on: no empty text block, and a first
    /// message in the user role.
    /// </summary>
    /// <remarks>
    /// Both are reachable from stored history rather than from a bug here. A turn that spends its
    /// whole budget thinking answers with empty text, which is a success this class reports as one
    /// and the chat layer stores verbatim — replayed as an empty text block it would make every
    /// later request in that conversation fail, so one such answer would retire the conversation.
    /// And <c>ChatCompletionService</c> packs history newest-first under a token budget, which can
    /// skip an oversized user message while keeping an older assistant one.
    /// <para>
    /// Dropping is right rather than lossy: an empty message carries nothing, and a leading
    /// assistant message is a reply to a user turn that is no longer in the window. A tool round
    /// cannot be split by either pass — its outcomes are not a user turn either, so they leave with
    /// the turn that asked for them.
    /// </para>
    /// <para>
    /// Dropping everything is possible, and is refused here by name. Two routes reach it: a
    /// message that is only whitespace, which nothing validates on the way in, and a single user
    /// turn larger than the input budget, which the history packer skips like any other. Both would
    /// otherwise go out as an empty transcript, come back rejected, and reach the athlete as a
    /// gateway failure that names nothing.
    /// </para>
    /// </remarks>
    private static Result<IReadOnlyList<Messages.MessageParam>, Problem> Transcribe(ChatTurnRequest request)
    {
        var transcript = request.Messages
            .Where(Speaks)
            .SkipWhile(message => !OpensATranscript(message))
            .ToList();

        var latestWords = transcript.FindLastIndex(message => message is ModelMessage.UserSaid);
        var cached = request.Caching is PromptCaching.Conversation;

        return transcript.Count == 0
            ? Result<IReadOnlyList<Messages.MessageParam>, Problem>.Failure(new Problem.Invalid(
                "messages",
                "There is nothing to send: the message is empty, or too large for the input budget."))
            : transcript
                .Select((message, index) => (Message: message, IsLatestWords: index == latestWords))
                .Traverse(turn => turn is { IsLatestWords: true, Message: ModelMessage.UserSaid said } && cached
                    ? Text(Messages.MessageParamRole.User, said.Text, HistoryCache)
                    : Transcribe(turn.Message))
                .Map(messages => Briefed(messages, latestWords, request.Briefing));
    }

    /// <summary>
    /// The round's briefing as a system message straight after the athlete's latest words.
    /// </summary>
    /// <remarks>
    /// After the hour-long breakpoint on those words, so it is the one part of the conversation
    /// that is never cached and never needs to be: the next message brings a fresh one, and the
    /// history it is replayed into ends at the words, which match the cache byte for byte. A
    /// mid-conversation system message carries operator authority, which is what the briefing is.
    /// The API takes one only straight after a user turn, and only as the last message or directly
    /// before an assistant turn: on the first round it is last, and on every tool round it sits
    /// before the model's own request for tools. Not every model accepts one — Claude Sonnet 5 and
    /// models before Opus 4.8 refuse it — so moving the chat onto one of those means moving the
    /// briefing into the athlete's turn too.
    /// </remarks>
    private static IReadOnlyList<Messages.MessageParam> Briefed(
        IReadOnlyList<Messages.MessageParam> messages, int latestWords, string briefing) =>
        string.IsNullOrWhiteSpace(briefing) || latestWords < 0
            ? messages
            : [.. messages.Take(latestWords + 1), SystemNote(briefing), .. messages.Skip(latestWords + 1)];

    private static Messages.MessageParam SystemNote(string text) =>
        new(new Messages.SystemMessageParam { Content = text }, null);

    /// <summary>Whether a message says anything the model could read.</summary>
    private static bool Speaks(ModelMessage message) =>
        message switch
        {
            ModelMessage.UserSaid user => !string.IsNullOrWhiteSpace(user.Text),
            ModelMessage.AssistantSaid assistant => !string.IsNullOrWhiteSpace(assistant.Text),
            _ => true,
        };

    /// <summary>Whether a message is one the API will accept as the first of a transcript.</summary>
    private static bool OpensATranscript(ModelMessage message) =>
        message switch
        {
            ModelMessage.UserSaid => true,
            _ => false,
        };

    private static Result<Messages.MessageParam, Problem> Transcribe(ModelMessage message) =>
        message switch
        {
            ModelMessage.UserSaid user => Text(Messages.MessageParamRole.User, user.Text),

            ModelMessage.AssistantSaid assistant =>
                Text(Messages.MessageParamRole.Assistant, assistant.Text),

            ModelMessage.AssistantActed { Turn: AnthropicAssistantTurn turn } =>
                Result<Messages.MessageParam, Problem>.Success(Replay(turn.Blocks)),

            // Only reachable if a conversation were carried across a provider change mid-round,
            // which configuration does not allow — but it is an upstream shape, not an invariant
            // this class can assert, so it refuses rather than throwing.
            ModelMessage.AssistantActed foreign =>
                Result<Messages.MessageParam, Problem>.Failure(new Problem.Upstream(
                    $"Cannot replay a {foreign.Turn.GetType().Name} turn to Anthropic.")),

            ModelMessage.ToolsAnswered answered => Result<Messages.MessageParam, Problem>.Success(
                Blocks(Messages.MessageParamRole.User, [.. answered.Outcomes.Select(ResultBlock)])),

            _ => Result<Messages.MessageParam, Problem>.Failure(
                new Problem.Unexpected($"Unhandled message kind {message.GetType().Name}."))
        };

    /// <summary>
    /// The assistant turn, rebuilt block by block as parameters. A thinking block's signature is
    /// carried through byte for byte: the API verifies it, and a turn whose reasoning has been
    /// dropped or altered is rejected rather than merely answered differently.
    /// </summary>
    private static Messages.MessageParam Replay(IReadOnlyList<Messages.ContentBlock> blocks) =>
        Blocks(Messages.MessageParamRole.Assistant, [.. blocks.Select(Replay).OfType<Messages.ContentBlockParam>()]);

    private static Messages.ContentBlockParam? Replay(Messages.ContentBlock block)
    {
        if (block.TryPickText(out var text))
        {
            return new Messages.ContentBlockParam(new Messages.TextBlockParam(text.Text), null);
        }

        if (block.TryPickThinking(out var thinking))
        {
            return new Messages.ContentBlockParam(
                new Messages.ThinkingBlockParam
                {
                    Thinking = thinking.Thinking,
                    Signature = thinking.Signature,
                },
                null);
        }

        if (block.TryPickToolUse(out var toolUse))
        {
            return new Messages.ContentBlockParam(
                new Messages.ToolUseBlockParam
                {
                    ID = toolUse.ID,
                    Name = toolUse.Name,
                    Input = toolUse.Input,
                },
                null);
        }

        // Server-side tool blocks (web search, code execution) are not offered by this
        // application, so anything else here is something it never asked for and cannot replay
        // faithfully. Dropping it is safer than guessing at a param shape for it.
        return null;
    }

    /// <summary>
    /// One tool result. Every <c>tool_use</c> needs exactly one matching <c>tool_result</c> in the
    /// follow-up or the whole request is rejected, which is why the caller sends a round's outcomes
    /// together rather than one at a time.
    /// </summary>
    private static Messages.ContentBlockParam ResultBlock(ToolOutcome outcome) =>
        new(new Messages.ToolResultBlockParam
        {
            ToolUseID = outcome.CallId,
            Content = new Messages.ToolResultBlockParamContent(outcome.Content, null),
            IsError = outcome.Failed,
        }, null);

    private static Result<Messages.MessageParam, Problem> Text(Messages.MessageParamRole role, string text) =>
        Result<Messages.MessageParam, Problem>.Success(
            Blocks(role, [new Messages.ContentBlockParam(new Messages.TextBlockParam(text), null)]));

    /// <summary>A text turn with a cache breakpoint on its block.</summary>
    private static Result<Messages.MessageParam, Problem> Text(
        Messages.MessageParamRole role, string text, Messages.CacheControlEphemeral cache) =>
        Result<Messages.MessageParam, Problem>.Success(
            Blocks(role, [new Messages.ContentBlockParam(new Messages.TextBlockParam(text) { CacheControl = cache }, null)]));

    private static Messages.MessageParam Blocks(
        Messages.MessageParamRole role, IReadOnlyList<Messages.ContentBlockParam> blocks)
    {
        var content = new Messages.MessageContentParam(blocks, null);

        return role is Messages.MessageParamRole.Assistant
            ? new Messages.MessageParam(new Messages.AssistantMessageParam(content), null)
            : new Messages.MessageParam(new Messages.UserMessageParam(content), null);
    }

    private Task<Result<Messages.Message, Problem>> SendAsync(
        Messages.MessageCreateParams parameters, CancellationToken cancellationToken) =>
        GuardedAsync(() => _client.Messages.Create(parameters, cancellationToken), cancellationToken);

    private Task<Result<Messages.Message, Problem>> StreamAsync(
        Messages.MessageCreateParams parameters, Action<string> written, CancellationToken cancellationToken) =>
        GuardedAsync(async () =>
        {
            var aggregator = new MessageContentAggregator();
            await foreach (var streamed in _client.Messages.CreateStreaming(parameters, cancellationToken)
                               .CollectAsync(aggregator))
            {
                if (streamed.TryPickContentBlockDelta(out var block) && block.Delta.TryPickText(out var text))
                {
                    written(text.Text);
                }
            }
            return aggregator.Message();
        }, cancellationToken);

    /// <summary>
    /// The one place this application knows the Anthropic SDK throws. Caught most-specific-first
    /// and restated as <see cref="Problem.Upstream"/>, which is what every layer above speaks.
    /// </summary>
    /// <remarks>
    /// A stream can fail after it has begun: the API sends an <c>error</c> event mid-stream (an
    /// overload, usually), which the SDK raises as <see cref="AnthropicSseException"/>. Whatever
    /// text was handed on before it is the caller's to withdraw.
    /// </remarks>
    private async Task<Result<Messages.Message, Problem>> GuardedAsync(
        Func<Task<Messages.Message>> call, CancellationToken cancellationToken)
    {
        try
        {
            var message = await call();
            Account(message);
            return Result<Messages.Message, Problem>.Success(message);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            return Upstream($"Anthropic rejected the configured API key: {ex.Message}");
        }
        catch (AnthropicRateLimitException ex)
        {
            return Upstream($"Anthropic rate limit reached: {ex.Message}");
        }
        catch (AnthropicBadRequestException ex)
        {
            return Upstream($"Anthropic rejected the request: {ex.Message}");
        }
        catch (Anthropic4xxException ex)
        {
            return Upstream($"Anthropic refused the request: {ex.Message}");
        }
        catch (Anthropic5xxException ex)
        {
            return Upstream($"Anthropic is unavailable: {ex.Message}");
        }
        catch (AnthropicSseException ex)
        {
            return Upstream($"Anthropic failed partway through its answer: {ex.Message}");
        }
        catch (AnthropicIOException ex)
        {
            return Upstream($"Could not reach Anthropic: {ex.Message}");
        }
        catch (AnthropicInvalidDataException ex)
        {
            return Upstream($"Anthropic returned a response that could not be read: {ex.Message}");
        }
        catch (AnthropicException ex)
        {
            return Upstream($"Anthropic request failed: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Upstream("Anthropic did not respond in time.");
        }

        static Result<Messages.Message, Problem> Upstream(string detail) =>
            Result<Messages.Message, Problem>.Failure(new Problem.Upstream(detail));
    }

    /// <summary>
    /// What the call cost, as the API reports it. The cache fields are the only evidence that
    /// conversation caching is working: a regression that makes every request miss the cache
    /// changes nothing but the bill, and these lines are where it shows.
    /// </summary>
    private void Account(Messages.Message message) =>
        _logger.LogInformation(
            "Anthropic {Model}: {Input} input, {CacheWrite} cache write, {CacheRead} cache read, {Output} output tokens.",
            _model,
            message.Usage?.InputTokens,
            message.Usage?.CacheCreationInputTokens,
            message.Usage?.CacheReadInputTokens,
            message.Usage?.OutputTokens);

    /// <summary>
    /// Why the model stopped, and what the caller owes next. The stop reason is read before the
    /// content, because a refusal has no answer in it to read.
    /// </summary>
    private static Result<ModelTurn, Problem> ReadTurn(Messages.Message message) =>
        message.StopReason?.Value() switch
        {
            null => Result<ModelTurn, Problem>.Failure(
                new Problem.Upstream("Anthropic returned a message with no stop reason.")),

            Messages.StopReason.Refusal => Result<ModelTurn, Problem>.Failure(new Problem.Upstream(
                $"Anthropic declined to answer: {message.StopDetails?.Explanation ?? "no explanation given"}")),

            Messages.StopReason.MaxTokens => Result<ModelTurn, Problem>.Failure(new Problem.Upstream(
                "Incomplete model output due to MaxTokens parameter or token limit exceeded.")),

            Messages.StopReason.ModelContextWindowExceeded => Result<ModelTurn, Problem>.Failure(
                new Problem.Upstream("The conversation no longer fits in the model's context window.")),

            Messages.StopReason.ToolUse => ReadToolCalls(message),

            Messages.StopReason.EndTurn or Messages.StopReason.StopSequence =>
                Result<ModelTurn, Problem>.Success(new ModelTurn.Answered(
                    AnswerText(message), (int)(message.Usage?.OutputTokens ?? 0))),

            var other => Result<ModelTurn, Problem>.Failure(
                new Problem.Upstream($"Anthropic stopped for an unhandled reason: {other}."))
        };

    /// <summary>
    /// The answer is the text blocks only. Thinking blocks are present on every turn — reasoning
    /// cannot be disabled — and concatenating them into the answer would put the model's reasoning
    /// in front of the athlete.
    /// </summary>
    private static string AnswerText(Messages.Message message) =>
        string.Concat(message.Content.Select(block => block.TryPickText(out var text) ? text.Text : string.Empty));

    private static Result<ModelTurn, Problem> ReadToolCalls(Messages.Message message)
    {
        List<ToolInvocation> calls = [];
        foreach (var block in message.Content)
        {
            if (block.TryPickToolUse(out var toolUse))
            {
                calls.Add(new ToolInvocation(
                    toolUse.ID, toolUse.Name, JsonSerializer.Serialize(toolUse.Input)));
            }
        }

        return calls.Count == 0
            ? Result<ModelTurn, Problem>.Failure(new Problem.Upstream(
                "Anthropic stopped for tool use but asked for no tools."))
            : Result<ModelTurn, Problem>.Success(new ModelTurn.RequestedTools(
                calls, new AnthropicAssistantTurn(message.Content)));
    }

    /// <summary>
    /// An assistant turn as Anthropic will take it back: its content blocks, kept whole so
    /// <see cref="Replay"/> can rebuild each one with its signature intact.
    /// </summary>
    private sealed record AnthropicAssistantTurn(IReadOnlyList<Messages.ContentBlock> Blocks) : AssistantTurn;
}
