using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
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
public sealed class AnthropicChatModel(AnthropicClient client, string model) : IChatModel
{
    private readonly AnthropicClient _client = client;
    private readonly string _model = model;

    public Task<Result<ModelTurn, Problem>> RespondAsync(
        ChatTurnRequest request, CancellationToken cancellationToken = default) =>
        Transcribe(request)
            .BindAsync(messages => SendAsync(Parameters(request, messages), cancellationToken))
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
        return request.Tools.Count == 0
            ? parameters
            : parameters with { Tools = [.. request.Tools.Select(Describe)] };
    }

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

    private static Messages.ToolUnion Describe(ToolDefinition tool)
    {
        using var document = JsonDocument.Parse(tool.ParametersJson);
        var root = document.RootElement;

        var properties = root.TryGetProperty("properties", out var declared)
            && declared.ValueKind == JsonValueKind.Object
            ? declared.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
            : [];

        var required = root.TryGetProperty("required", out var names)
            && names.ValueKind == JsonValueKind.Array
            ? names.EnumerateArray().Select(n => n.GetString() ?? string.Empty).Where(n => n.Length > 0).ToList()
            : [];

        return new Messages.ToolUnion(
            new Messages.Tool
            {
                Name = tool.Name,
                Description = tool.Description,
                // InputSchema.Type is set by the SDK; declaring it here would be redundant.
                InputSchema = new Messages.InputSchema { Properties = properties, Required = required },
            },
            null);
    }

    private static Result<IReadOnlyList<Messages.MessageParam>, Problem> Transcribe(ChatTurnRequest request) =>
        request.Messages.Traverse(Transcribe);

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

    private static Messages.MessageParam Blocks(
        Messages.MessageParamRole role, IReadOnlyList<Messages.ContentBlockParam> blocks)
    {
        var content = new Messages.MessageContentParam(blocks, null);

        return role is Messages.MessageParamRole.Assistant
            ? new Messages.MessageParam(new Messages.AssistantMessageParam(content), null)
            : new Messages.MessageParam(new Messages.UserMessageParam(content), null);
    }

    /// <summary>
    /// The one place this application knows the Anthropic SDK throws. Caught most-specific-first
    /// and restated as <see cref="Problem.Upstream"/>, which is what every layer above speaks.
    /// </summary>
    private async Task<Result<Messages.Message, Problem>> SendAsync(
        Messages.MessageCreateParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            return Result<Messages.Message, Problem>.Success(
                await _client.Messages.Create(parameters, cancellationToken));
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
