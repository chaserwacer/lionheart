using lionheart.Functional;
using OpenAI.Chat;

namespace lionheart.Services.Llm.LmStudio;

/// <summary>
/// The port over LM Studio's OpenAI-compatible API — the development and test provider, and a
/// locally hosted model, so nothing leaves the machine.
/// </summary>
/// <remarks>
/// The whole of this application's knowledge of the OpenAI SDK lives here and in
/// <see cref="OpenAiCompletionResult"/>.
/// </remarks>
public sealed class LmStudioChatModel(ChatClient client) : IChatModel
{
    private readonly ChatClient _client = client;

    public Task<Result<ModelTurn, Problem>> RespondAsync(
        ChatTurnRequest request, CancellationToken cancellationToken = default) =>
        Transcribe(request)
            .BindAsync(messages => _client
                .CompleteAsResultAsync(messages, Options(request), cancellationToken)
                .BindAsync(ReadTurn));

    /// <summary>
    /// The conversation as the SDK wants it: the system prompt first, then each message in order.
    /// </summary>
    /// <remarks>
    /// The briefing joins the system prompt rather than following the athlete's words: many local
    /// models' chat templates refuse a system message anywhere but first. The local server's prefix
    /// cache costs nothing to miss, so the reason Anthropic places it later does not apply here.
    /// </remarks>
    private static Result<List<ChatMessage>, Problem> Transcribe(ChatTurnRequest request) =>
        request.Messages
            .Traverse(Transcribe)
            .Map(transcribed =>
            {
                var system = string.IsNullOrWhiteSpace(request.Briefing)
                    ? request.SystemPrompt
                    : $"{request.SystemPrompt}\n\n{request.Briefing}";
                var messages = new List<ChatMessage> { new SystemChatMessage(system) };
                messages.AddRange(transcribed.SelectMany(group => group));
                return messages;
            });

    /// <summary>
    /// One application message can be several SDK ones: a round of tool results is one SDK message
    /// per result, which is why this returns a list rather than a message.
    /// </summary>
    private static Result<IReadOnlyList<ChatMessage>, Problem> Transcribe(ModelMessage message) =>
        message switch
        {
            ModelMessage.UserSaid user =>
                Result<IReadOnlyList<ChatMessage>, Problem>.Success([new UserChatMessage(user.Text)]),

            ModelMessage.AssistantSaid assistant =>
                Result<IReadOnlyList<ChatMessage>, Problem>.Success([new AssistantChatMessage(assistant.Text)]),

            ModelMessage.AssistantActed { Turn: OpenAiAssistantTurn turn } =>
                Result<IReadOnlyList<ChatMessage>, Problem>.Success(
                    [new AssistantChatMessage(turn.Completion)]),

            // Only reachable if a conversation were carried across a provider change mid-round,
            // which configuration does not allow — but it is an upstream shape, not an invariant
            // this class can assert, so it refuses rather than throwing.
            ModelMessage.AssistantActed foreign =>
                Result<IReadOnlyList<ChatMessage>, Problem>.Failure(new Problem.Upstream(
                    $"Cannot replay a {foreign.Turn.GetType().Name} turn to LM Studio.")),

            ModelMessage.ToolsAnswered answered =>
                Result<IReadOnlyList<ChatMessage>, Problem>.Success(
                    [.. answered.Outcomes.Select(o => new ToolChatMessage(o.CallId, o.Content))]),

            _ => Result<IReadOnlyList<ChatMessage>, Problem>.Failure(
                new Problem.Unexpected($"Unhandled message kind {message.GetType().Name}."))
        };

    // OPENAI001: ReasoningEffortLevel is still marked experimental in the SDK. Suppressed here
    // only, so the rest of the codebase keeps warning about experimental OpenAI surface.
#pragma warning disable OPENAI001
    private static ChatCompletionOptions Options(ChatTurnRequest request)
    {
        var options = new ChatCompletionOptions { MaxOutputTokenCount = request.MaxOutputTokens };

        foreach (var tool in request.Tools)
        {
            options.Tools.Add(ChatTool.CreateFunctionTool(
                tool.Name, tool.Description, BinaryData.FromString(tool.ParametersJson)));
        }

        if (request.Output is OutputContract.JsonObject json)
        {
            options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: json.Name,
                jsonSchema: BinaryData.FromString(json.SchemaJson),
                jsonSchemaIsStrict: true);
            options.Temperature = 0.1f;
        }

        if (request.Reasoning is ReasoningDepth.Minimal)
        {
            // Load-bearing, not a tuning knob: a reasoning model routes the schema-conformant JSON
            // to `reasoning_content` and leaves `content` empty, which reads here as an empty
            // response. "none" is not one of the SDK's presets; it is what LM Studio accepts to
            // suppress thinking. See the "Traps" section of docs/data-remodel.md.
            options.ReasoningEffortLevel = new ChatReasoningEffortLevel("none");
        }

        return options;
    }
#pragma warning restore OPENAI001

    private static Result<ModelTurn, Problem> ReadTurn(ChatCompletion completion) =>
        completion.FinishReason switch
        {
            ChatFinishReason.Stop => Result<ModelTurn, Problem>.Success(new ModelTurn.Answered(
                string.Concat(completion.Content.Select(c => c.Text)),
                (int)completion.Usage.OutputTokenCount)),

            ChatFinishReason.ToolCalls => ReadToolCalls(completion),

            ChatFinishReason.Length => Result<ModelTurn, Problem>.Failure(new Problem.Upstream(
                "Incomplete model output due to MaxTokens parameter or token limit exceeded.")),

            ChatFinishReason.ContentFilter => Result<ModelTurn, Problem>.Failure(
                new Problem.Upstream("Omitted content due to a content filter flag.")),

            var other => Result<ModelTurn, Problem>.Failure(
                new Problem.Upstream(other.ToString()))
        };

    private static Result<ModelTurn, Problem> ReadToolCalls(ChatCompletion completion) =>
        completion.ToolCalls.Count == 0
            ? Result<ModelTurn, Problem>.Failure(new Problem.Upstream(
                "Chat model stopped for tool calls but asked for none."))
            : Result<ModelTurn, Problem>.Success(new ModelTurn.RequestedTools(
                [.. completion.ToolCalls.Select(call => new ToolInvocation(
                    call.Id, call.FunctionName, call.FunctionArguments?.ToString() ?? "{}"))],
                new OpenAiAssistantTurn(completion)));

    /// <summary>
    /// An assistant turn as the OpenAI SDK will take it back: the completion itself, which
    /// <c>AssistantChatMessage</c> knows how to replay including its tool calls.
    /// </summary>
    private sealed record OpenAiAssistantTurn(ChatCompletion Completion) : AssistantTurn;
}
