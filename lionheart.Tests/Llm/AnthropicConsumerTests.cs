using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Interpretation;
using lionheart.Model.Training.Sets;
using lionheart.Services.Interpretation;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Completion;
using Services.Chat;

namespace lionheart.Tests.Llm;

/// <summary>
/// The real consumers — the tool loop and the interpretation parser — driven over the Anthropic
/// provider rather than LM Studio.
/// </summary>
/// <remarks>
/// The point of these is that nothing in either consumer is provider-specific: the same loop and
/// the same parser that <c>ChatCompletionServiceTests</c> and the live tests exercise against an
/// OpenAI-compatible server have to behave identically here, over a wire format that shares
/// nothing with it.
/// </remarks>
public sealed class AnthropicConsumerTests : IDisposable
{
    private readonly FakeAnthropicServer _server = new();
    private readonly TestToolbox _toolbox = new();

    public void Dispose()
    {
        _server.Dispose();
        _toolbox.Dispose();
    }

    private Task<Result<ChatCompletionResponse, Problem>> Complete(
        string userMessage = "how did last week go?") =>
        new ChatCompletionService(_server.CreateChatModel(), _toolbox.Registry, _toolbox.Executor)
            .GenerateChatCompletionAsync(
                new IdentityUser("tester@lionheart.local"),
                new ChatCompletionRequest
                {
                    Conversation = AConversation.With(userMessage),
                    MaxOutputTokens = 1024,
                });

    // ---- The tool loop ----

    [Fact]
    public async Task A_plain_answer_returns_one_model_message()
    {
        _server.Responds(CannedAnthropic.ThinkingThenText("Checking.", "You logged four sessions."));

        var response = ResultAssert.Succeeded(await Complete());

        var message = Assert.IsType<LHModelChatMessage>(Assert.Single(response.CompletionGeneratedMessages));
        Assert.Equal("You logged four sessions.", message.Content);
        Assert.Equal(message.ChatMessageItemID, response.ModelFinalChatMessageID);
    }

    [Fact]
    public async Task A_tool_call_is_executed_and_its_result_persisted_before_the_final_answer()
    {
        _server.Responds(CannedAnthropic.ThinkingThenToolUse(
            "I should echo it.", "echo", """{"phrase":"sled pulls"}"""));
        _server.Responds(CannedAnthropic.Text("Got it."));

        var response = ResultAssert.Succeeded(await Complete());

        Assert.Equal(["echo:sled pulls"], _toolbox.Tools.Calls);
        Assert.Equal(2, _server.RequestCount);

        var toolResult = Assert.IsType<LHChatToolCallResult>(response.CompletionGeneratedMessages[0]);
        Assert.Contains("you said sled pulls", toolResult.Content);
        Assert.IsType<LHModelChatMessage>(response.CompletionGeneratedMessages[1]);
    }

    [Fact]
    public async Task The_tool_result_is_sent_back_to_the_model_on_the_next_round()
    {
        _server.Responds(CannedAnthropic.ToolUse("echo", """{"phrase":"sled pulls"}"""));
        _server.Responds(CannedAnthropic.Text("Got it."));

        await Complete();

        Assert.Contains("you said sled pulls", _server.ReceivedRequests[1]);
    }

    /// <summary>
    /// The round trip the Anthropic API is strictest about: the turn that asked for the tool has to
    /// come back alongside the result, thinking signature and all, or the request is rejected.
    /// </summary>
    [Fact]
    public async Task The_turn_that_asked_for_the_tool_is_replayed_with_it()
    {
        _server.Responds(CannedAnthropic.ThinkingThenToolUse(
            "I should echo it.", "echo", """{"phrase":"sled pulls"}"""));
        _server.Responds(CannedAnthropic.Text("Got it."));

        await Complete();

        Assert.Contains(CannedAnthropic.ThinkingSignature, _server.ReceivedRequests[1]);
        Assert.Contains("\"tool_use\"", _server.ReceivedRequests[1]);
        Assert.Contains("\"tool_result\"", _server.ReceivedRequests[1]);
    }

    [Fact]
    public async Task Several_tool_calls_in_one_round_all_execute()
    {
        _server.Responds(CannedAnthropic.ToolUses(
            ("echo", """{"phrase":"a"}""", "toolu_1"),
            ("count_sets", """{"days":2}""", "toolu_2")));
        _server.Responds(CannedAnthropic.Text("done"));

        Assert.True((await Complete()).IsSuccess);
        Assert.Equal(["echo:a", "count:2"], _toolbox.Tools.Calls);
    }

    [Fact]
    public async Task A_multi_round_tool_chain_keeps_going_until_the_model_stops()
    {
        _server.Responds(CannedAnthropic.ToolUse("echo", """{"phrase":"one"}""", "toolu_1"));
        _server.Responds(CannedAnthropic.ToolUse("count_sets", """{"days":3}""", "toolu_2"));
        _server.Responds(CannedAnthropic.Text("finished"));

        Assert.True((await Complete()).IsSuccess);
        Assert.Equal(["echo:one", "count:3"], _toolbox.Tools.Calls);
        Assert.Equal(3, _server.RequestCount);
    }

    [Fact]
    public async Task An_unknown_tool_does_not_abort_the_conversation()
    {
        _server.Responds(CannedAnthropic.ToolUse("summon_demon", """{"phrase":"x"}"""));
        _server.Responds(CannedAnthropic.Text("I cannot do that."));

        Assert.True((await Complete()).IsSuccess);
        Assert.Empty(_toolbox.Tools.Calls);
        Assert.Contains("No such tool is available.", _server.ReceivedRequests[1]);
    }

    [Fact]
    public async Task A_throwing_tool_tells_the_model_the_call_failed_without_aborting()
    {
        _server.Responds(CannedAnthropic.ToolUse("boom", """{"phrase":"kaboom"}"""));
        _server.Responds(CannedAnthropic.Text("That tool failed."));

        var response = ResultAssert.Succeeded(await Complete());

        Assert.Equal(["boom:kaboom"], _toolbox.Tools.Calls);
        Assert.Contains("Tool call failed", _server.ReceivedRequests[1]);

        // A failed call is told to the model and then forgotten, never stored as history.
        Assert.Single(response.CompletionGeneratedMessages);
    }

    /// <summary>
    /// A model that keeps calling tools without ever answering would otherwise bill a request per
    /// round forever. The loop gives up and says so.
    /// </summary>
    [Fact]
    public async Task A_model_that_never_stops_calling_tools_is_abandoned_rather_than_looping()
    {
        _server.AlwaysResponds(CannedAnthropic.ToolUse("echo", """{"phrase":"again"}"""));

        var problem = ResultAssert.Failed(await Complete());

        Assert.Contains("without answering", problem.Description);
    }

    [Fact]
    public async Task A_refusal_fails_the_exchange_rather_than_storing_an_empty_answer()
    {
        _server.Responds(CannedAnthropic.Refusal("I will not provide medical advice."));

        Assert.Contains("medical advice", ResultAssert.Failed(await Complete()).Description);
    }

    [Fact]
    public async Task An_oversized_system_prompt_is_rejected_before_the_model_is_called()
    {
        var service = new ChatCompletionService(
            _server.CreateChatModel(), _toolbox.Registry, _toolbox.Executor);

        var result = await service.GenerateChatCompletionAsync(
            new IdentityUser("tester@lionheart.local"),
            new ChatCompletionRequest
            {
                Conversation = AConversation.With("hello", systemTokens: 500_000),
                MaxOutputTokens = 1024,
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(0, _server.RequestCount);
    }

    // ---- Structured output, through the real parser ----

    /// <summary>
    /// The parse path end to end on Anthropic: a schema-conformant object arrives as a text block
    /// behind a thinking block, and has to become drafts. This is the path that used to be built
    /// on a forced tool call, which current models answer with a 400.
    /// </summary>
    [Fact]
    public async Task A_schema_conformant_parse_becomes_drafts()
    {
        _server.Responds(CannedAnthropic.ThinkingThenText("Bench press, three sets.", ParsedBench));

        var parsed = await new InterpretationParser(_server.CreateChatModel()).ParseAsync(
            new InterpretationRequest("Bench press 225x5 today. Felt strong.", DateTime.UtcNow),
            KnownMovements);

        var interpretation = ResultAssert.Succeeded(parsed);
        var session = Assert.IsType<DraftSession>(Assert.Single(interpretation.Entries));

        var set = Assert.IsType<DraftLiftSet>(Assert.Single(session.Sets));
        Assert.Equal("Bench Press", set.Movement.BaseName);
        Assert.Equal(5, set.Reps);
        Assert.Equal(225, set.Weight);
        Assert.Equal("worked up to 225", Assert.IsType<Texted>(set.Source).RawText);
        Assert.NotEmpty(session.Decoration.Facets);
    }

    /// <summary>
    /// The trap, in its Anthropic spelling. Reasoning cannot be switched off, so a turn whose text
    /// block never arrived has to read as an empty response naming the cause — not as a crash, and
    /// not as the model's reasoning parsed as JSON.
    /// </summary>
    [Fact]
    public async Task A_parse_that_came_back_as_reasoning_only_fails_naming_the_cause()
    {
        _server.Responds(CannedAnthropic.ThinkingOnly("The athlete benched 225 for five."));

        var parsed = await new InterpretationParser(_server.CreateChatModel()).ParseAsync(
            new InterpretationRequest("Bench press 225x5 today.", DateTime.UtcNow),
            KnownMovements);

        Assert.Contains("empty response", ResultAssert.Failed(parsed).Description);
    }

    [Fact]
    public async Task A_parse_that_is_not_json_fails_rather_than_throwing()
    {
        _server.Responds(CannedAnthropic.Text("I could not read that log."));

        var parsed = await new InterpretationParser(_server.CreateChatModel()).ParseAsync(
            new InterpretationRequest("Bench press 225x5 today.", DateTime.UtcNow),
            KnownMovements);

        Assert.Contains("malformed JSON", ResultAssert.Failed(parsed).Description);
    }

    private const string KnownMovements =
        "Movements this athlete logs: Bench Press";

    private const string ParsedBench = """
        {"entries":[{"kind":"session","start":null,"end":null,"notes":"Felt strong.",
        "decoration":{"facets":[{"axis":"Modality","value":"lifting"}],"freeTags":["upper"],
        "valence":"Positive","summaryText":"A strong bench session."},
        "sets":[{"kind":"lift","rawText":"worked up to 225","structureConfidence":0.9,"facets":[],
        "movementBaseName":"Bench Press","equipmentName":"Barbell","modifierName":null,
        "reps":5,"weight":225,"weightUnit":"lb","distance":null,"distanceUnit":null,
        "durationSeconds":null,"elevationGainMeters":null,"rpe":null}]}],"warnings":[]}
        """;
}
