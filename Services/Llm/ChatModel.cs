using lionheart.Functional;

namespace lionheart.Services.Llm;

/// <summary>
/// One round with a language model: send the conversation so far, get back either an answer or a
/// request to run tools. The only way anything in this application reaches a model.
/// </summary>
/// <remarks>
/// The port is deliberately one round rather than a whole conversation. Driving rounds is
/// application policy — which tool results are persisted, what a failed tool is told, how the
/// token budget is spent — and that policy is identical whoever serves the model, so it lives once
/// in <c>Services/Chat/ChatCompletionService</c> rather than twice in the providers. What the
/// providers own is the part that genuinely differs: building a request, reading a response, and
/// restating that vendor's failures as <see cref="Problem.Upstream"/>. Nothing above this
/// interface references a vendor SDK.
/// </remarks>
public interface IChatModel
{
    Task<Result<ModelTurn, Problem>> RespondAsync(
        ChatTurnRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Everything one round needs. A record so a loop can vary only the messages.</summary>
/// <param name="SystemPrompt">
/// Carried separately rather than as a leading message: Anthropic takes the system prompt as its
/// own request field, and an OpenAI-compatible server takes it as the first message. Keeping it out
/// of <paramref name="Messages"/> means neither provider has to find it there.
/// </param>
/// <param name="Tools">Empty when the model is not allowed to call anything this round.</param>
public sealed record ChatTurnRequest(
    string SystemPrompt,
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<ToolDefinition> Tools,
    OutputContract Output,
    ReasoningDepth Reasoning,
    int MaxOutputTokens)
{
    /// <summary>
    /// Whether the prompt is worth caching on a provider that charges for the privilege. Not
    /// positional, because only conversation asks for it.
    /// </summary>
    public PromptCaching Caching { get; init; } = PromptCaching.None;

    /// <summary>
    /// What is true as this round is asked — the athlete's date and time, his day so far — written
    /// by this application rather than said by him. Empty when a round carries none.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="SystemPrompt"/> because it changes every message while the prompt
    /// is frozen for a whole conversation: inside the prompt it would change the front of every
    /// request and nothing after it could be read from a cache. Anthropic places it after the
    /// athlete's latest words instead; LM Studio, whose local cache costs nothing to miss, appends
    /// it to the system prompt. It is never stored, so history replays without it.
    /// </remarks>
    public string Briefing { get; init; } = string.Empty;

    /// <summary>
    /// One round of a conversation. Cached, because every round resends the round before it plus
    /// a little: the same system prompt, the same history, one more turn.
    /// </summary>
    public static ChatTurnRequest Conversation(
        string systemPrompt,
        IReadOnlyList<ModelMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        int maxOutputTokens) =>
        new(systemPrompt, messages, tools, new OutputContract.FreeText(),
            ReasoningDepth.Standard, maxOutputTokens)
        {
            Caching = PromptCaching.Conversation,
        };

    /// <summary>
    /// A single-shot extraction: one user turn, no tools, an answer that must match a schema.
    /// Asks for the shallowest reasoning the provider allows — extraction gains nothing from
    /// depth, and on both providers deep reasoning is what empties the answer (see the "Traps"
    /// section of docs/data-remodel.md).
    /// </summary>
    public static ChatTurnRequest Extraction(
        string systemPrompt, string userText, string schemaName, string schemaJson, int maxOutputTokens) =>
        new(systemPrompt, [new ModelMessage.UserSaid(userText)], [],
            new OutputContract.JsonObject(schemaName, schemaJson), ReasoningDepth.Minimal, maxOutputTokens);

    /// <summary>
    /// Prose over facts that are already computed: one user turn, no tools, no schema. The facts
    /// arrive decided, so there is nothing here to reason about and reasoning depth is asked at
    /// its minimum — which also stops a provider whose thinking cannot be disabled from spending
    /// the output budget before it writes anything.
    /// </summary>
    public static ChatTurnRequest Prose(string systemPrompt, string userText, int maxOutputTokens) =>
        new(systemPrompt, [new ModelMessage.UserSaid(userText)], [],
            new OutputContract.FreeText(), ReasoningDepth.Minimal, maxOutputTokens);
}

/// <summary>
/// Whether a request's prompt should be cached for the next one. A hint rather than a guarantee: a
/// provider with no explicit cache (LM Studio keeps its own prefix cache) ignores it.
/// </summary>
public enum PromptCaching
{
    /// <summary>A one-off call whose prompt will not be sent again.</summary>
    None,

    /// <summary>
    /// The prompt is a conversation that the next request will resend with a turn appended: cache
    /// everything up to its last block, for long enough that the athlete coming back within the
    /// hour reads the conversation from the cache rather than paying for it again.
    /// </summary>
    Conversation
}

/// <summary>How the model ended its turn. Two outcomes, and the caller owes each a different move.</summary>
public abstract record ModelTurn
{
    private protected ModelTurn() { }

    /// <summary>The model is done talking. <paramref name="Text"/> may legitimately be empty.</summary>
    public sealed record Answered(string Text, int OutputTokens) : ModelTurn;

    /// <summary>
    /// The model wants tools run before it will answer. <paramref name="Turn"/> is the turn itself,
    /// to be sent back ahead of the results — every provider requires the request that asked for a
    /// tool to accompany the result answering it.
    /// </summary>
    public sealed record RequestedTools(
        IReadOnlyList<ToolInvocation> Calls, AssistantTurn Turn) : ModelTurn;
}

/// <summary>
/// One message in the conversation as this application models it — not as either vendor's SDK
/// models it.
/// </summary>
public abstract record ModelMessage
{
    private protected ModelMessage() { }

    public sealed record UserSaid(string Text) : ModelMessage;

    /// <summary>An answer the model gave earlier, read back out of storage.</summary>
    public sealed record AssistantSaid(string Text) : ModelMessage;

    /// <summary>
    /// A turn in which the model asked for tools, echoed back verbatim. Only ever a turn this
    /// process just received, never one rehydrated from storage.
    /// </summary>
    public sealed record AssistantActed(AssistantTurn Turn) : ModelMessage;

    /// <summary>
    /// The results answering the tool calls of the immediately preceding <see cref="AssistantActed"/>.
    /// All of them together, because Anthropic carries them as blocks of one message while an
    /// OpenAI-compatible server carries them as one message each — a provider that received them
    /// one at a time could not tell where the round ended.
    /// </summary>
    public sealed record ToolsAnswered(IReadOnlyList<ToolOutcome> Outcomes) : ModelMessage;
}

/// <summary>
/// A model turn that asked for tools, in whatever form its own provider has to send back. Opaque
/// above the provider boundary: the application carries it from one round to the next and never
/// looks inside.
/// </summary>
/// <remarks>
/// The one type here that providers extend rather than consume, so the hierarchy is open where the
/// rest of this file's are closed. It has to be. Echoing a turn back is the least portable thing
/// either SDK does — Anthropic has no <c>ToParam()</c>, so each content block is rebuilt as its
/// <c>*Param</c> counterpart and a thinking block's signature must survive the trip unaltered or
/// the request is rejected — and none of that survives being flattened into a shape both vendors
/// share. A provider matches its own case and refuses any other; a conversation never spans two
/// providers, since one is configured per process and these never reach storage.
/// </remarks>
public abstract record AssistantTurn
{
    protected AssistantTurn() { }
}

/// <summary>A tool the model asked for, named and with its arguments still as the model wrote them.</summary>
/// <param name="ArgumentsJson">
/// Verbatim, valid JSON or not. A model is free to send malformed arguments; that is a bad tool
/// call to report back to it, not a transport fault, so the text reaches the executor unexamined.
/// </param>
public sealed record ToolInvocation(string CallId, string ToolName, string ArgumentsJson);

/// <summary>What a tool call produced, on its way back to the model.</summary>
/// <param name="Failed">
/// Kept distinct from a message that merely reads like an error, because a model recovers better
/// when told which it is. How far that distinction travels depends on the provider: Anthropic
/// carries it as a flag on the tool result, while an OpenAI-compatible tool message has no field
/// for it, so there it survives only in the text the executor wrote.
/// </param>
public sealed record ToolOutcome(string CallId, string Content, bool Failed)
{
    public static ToolOutcome Produced(string callId, string content) => new(callId, content, Failed: false);

    public static ToolOutcome Refused(string callId, string reason) => new(callId, reason, Failed: true);
}

/// <summary>
/// A tool as this application describes it: a name, a sentence for the model, and a JSON Schema for
/// the arguments.
/// </summary>
/// <param name="ParametersJson">
/// A JSON Schema object. Held as text because that is the form both providers want it in and the
/// form <c>ToolSchemaMapper</c> generates — parsing it here only to re-serialise it would add a
/// failure mode and remove none.
/// </param>
public sealed record ToolDefinition(string Name, string Description, string ParametersJson);

/// <summary>What shape the answer has to arrive in.</summary>
public abstract record OutputContract
{
    private protected OutputContract() { }

    public sealed record FreeText : OutputContract;

    /// <summary>
    /// A JSON object conforming to <paramref name="SchemaJson"/>. The provider is responsible for
    /// asking in whatever way its API actually supports; notably this is <b>not</b> done by forcing
    /// a tool call, which Anthropic no longer permits.
    /// </summary>
    public sealed record JsonObject(string Name, string SchemaJson) : OutputContract;
}

/// <summary>
/// How much thinking the answer is worth, in the application's terms rather than any provider's
/// wire values.
/// </summary>
public enum ReasoningDepth
{
    /// <summary>
    /// As little as the provider allows. For extraction, where depth buys nothing and costs the
    /// answer: a reasoning model routes schema-conformant JSON to its reasoning channel and leaves
    /// the content empty.
    /// </summary>
    Minimal,

    /// <summary>The provider's default. For conversation, where reasoning is the point.</summary>
    Standard
}
