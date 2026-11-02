using System.Net;
using System.Text.Json;
using lionheart.Services.Llm;
using lionheart.Tests.Support;

namespace lionheart.Tests.Llm;

/// <summary>
/// The Anthropic provider, driven entirely by canned wire responses. Covers the three things the
/// current API makes easy to get wrong: thinking blocks are always present and are not the answer,
/// structured output is asked for with a schema rather than a forced tool call, and echoing a turn
/// back means rebuilding its blocks with the thinking signature intact.
/// </summary>
/// <remarks>
/// Live Anthropic calls are not exercised anywhere — that needs the owner's API key — so this is
/// the only check that the request this provider builds is the one the API documents.
/// </remarks>
public sealed class AnthropicChatModelTests : IDisposable
{
    private readonly FakeAnthropicServer _server = new();

    public void Dispose() => _server.Dispose();

    private Task<lionheart.Functional.Result<ModelTurn, lionheart.Functional.Problem>> Respond(
        ChatTurnRequest? request = null) =>
        _server.CreateChatModel().RespondAsync(
            request ?? ChatTurnRequest.Prose("You are a coach.", "how did last week go?", 1024));

    // ---- Reading a turn ----

    [Fact]
    public async Task A_plain_answer_comes_back_as_the_models_text()
    {
        _server.Responds(CannedAnthropic.Text("You logged four sessions."));

        var turn = Assert.IsType<ModelTurn.Answered>(ResultAssert.Succeeded(await Respond()));

        Assert.Equal("You logged four sessions.", turn.Text);
        Assert.Equal(7, turn.OutputTokens);
    }

    /// <summary>
    /// The Anthropic half of the reasoning trap. Thinking cannot be disabled on current models, so
    /// a turn almost always carries it — and concatenating it into the answer would put the
    /// model's reasoning in front of the athlete.
    /// </summary>
    [Fact]
    public async Task Thinking_is_not_part_of_the_answer()
    {
        _server.Responds(CannedAnthropic.ThinkingThenText(
            "The athlete asked about last week. Let me check the sessions.",
            "You logged four sessions."));

        var turn = Assert.IsType<ModelTurn.Answered>(ResultAssert.Succeeded(await Respond()));

        Assert.Equal("You logged four sessions.", turn.Text);
        Assert.DoesNotContain("Let me check", turn.Text);
    }

    [Fact]
    public async Task A_turn_that_is_only_thinking_answers_with_nothing_rather_than_its_reasoning()
    {
        _server.Responds(CannedAnthropic.ThinkingOnly("Still deciding what to say."));

        var turn = Assert.IsType<ModelTurn.Answered>(ResultAssert.Succeeded(await Respond()));

        Assert.Equal(string.Empty, turn.Text);
    }

    [Fact]
    public async Task A_tool_request_comes_back_with_its_id_name_and_arguments()
    {
        _server.Responds(CannedAnthropic.ToolUse("count_sets", """{"days":5}""", "toolu_42"));

        var turn = Assert.IsType<ModelTurn.RequestedTools>(ResultAssert.Succeeded(await Respond()));

        var call = Assert.Single(turn.Calls);
        Assert.Equal("toolu_42", call.CallId);
        Assert.Equal("count_sets", call.ToolName);
        Assert.Contains("\"days\":5", call.ArgumentsJson);
    }

    [Fact]
    public async Task Several_tool_requests_in_one_turn_all_come_back()
    {
        _server.Responds(CannedAnthropic.ToolUses(
            ("echo", """{"phrase":"a"}""", "toolu_1"),
            ("count_sets", """{"days":2}""", "toolu_2")));

        var turn = Assert.IsType<ModelTurn.RequestedTools>(ResultAssert.Succeeded(await Respond()));

        Assert.Equal(["echo", "count_sets"], turn.Calls.Select(c => c.ToolName));
    }

    // ---- Failures, all restated as Upstream ----

    [Fact]
    public async Task A_refusal_fails_with_the_explanation_the_api_gave()
    {
        _server.Responds(CannedAnthropic.Refusal("I will not provide medical advice."));

        var problem = ResultAssert.Failed(await Respond());

        Assert.Contains("medical advice", problem.Description);
    }

    [Fact]
    public async Task A_truncated_answer_fails_with_a_token_limit_message()
    {
        _server.Responds(CannedAnthropic.Text("truncated mid-sen", stopReason: "max_tokens"));

        Assert.Contains("MaxTokens", ResultAssert.Failed(await Respond()).Description);
    }

    [Fact]
    public async Task An_unreachable_api_fails_rather_than_throwing()
    {
        _server.RespondsWithTransportFailure();

        Assert.Contains("Anthropic", ResultAssert.Failed(await Respond()).Description);
    }

    [Fact]
    public async Task A_rejected_api_key_fails_naming_the_key_rather_than_throwing()
    {
        _server.Responds(
            CannedAnthropic.Error("invalid x-api-key", "authentication_error"), HttpStatusCode.Unauthorized);

        Assert.Contains("API key", ResultAssert.Failed(await Respond()).Description);
    }

    [Fact]
    public async Task A_rate_limit_fails_as_a_rate_limit_rather_than_a_generic_refusal()
    {
        _server.Responds(
            CannedAnthropic.Error("rate limited", "rate_limit_error"), HttpStatusCode.TooManyRequests);

        Assert.Contains("rate limit", ResultAssert.Failed(await Respond()).Description);
    }

    [Fact]
    public async Task A_bad_request_surfaces_the_api_s_own_message()
    {
        _server.Responds(
            CannedAnthropic.Error("thinking: Extra inputs are not permitted"), HttpStatusCode.BadRequest);

        Assert.Contains("Extra inputs are not permitted", ResultAssert.Failed(await Respond()).Description);
    }

    [Fact]
    public async Task An_unavailable_api_fails_as_unavailable()
    {
        _server.Responds(
            CannedAnthropic.Error("overloaded", "overloaded_error"), HttpStatusCode.ServiceUnavailable);

        Assert.Contains("unavailable", ResultAssert.Failed(await Respond()).Description);
    }

    // ---- The request this provider builds ----

    [Fact]
    public async Task The_system_prompt_is_sent_as_its_own_field_rather_than_as_a_message()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        await Respond();

        var sent = Request(Assert.Single(_server.ReceivedRequests));
        Assert.Equal("You are a coach.", sent.GetProperty("system").GetString());
        Assert.DoesNotContain(
            sent.GetProperty("messages").EnumerateArray(),
            message => message.GetProperty("role").GetString() == "system");
    }

    /// <summary>
    /// The single most likely way to get this wrong. <c>tool_choice</c> of <c>any</c> or
    /// <c>tool</c> is a 400 on current models, so the provider must never send one — not even to
    /// coax JSON out of the model, which is what structured output is for.
    /// </summary>
    [Fact]
    public async Task Tool_use_is_never_forced()
    {
        _server.Responds(CannedAnthropic.Text("{}"));

        await Respond(ChatTurnRequest.Extraction(
            "Extract.", "bench 225x5", "interpretation", MinimalSchema, 2048));

        Assert.False(Request(Assert.Single(_server.ReceivedRequests)).TryGetProperty("tool_choice", out _));
    }

    /// <summary>
    /// Thinking is always on and both <c>ThinkingConfigDisabled</c> and a token budget are a 400,
    /// so the provider must leave the field off the request entirely.
    /// </summary>
    [Fact]
    public async Task Thinking_is_never_configured()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        await Respond();

        Assert.False(Request(Assert.Single(_server.ReceivedRequests)).TryGetProperty("thinking", out _));
    }

    [Fact]
    public async Task A_schema_is_asked_for_through_output_config_rather_than_a_tool()
    {
        _server.Responds(CannedAnthropic.Text("{}"));

        await Respond(ChatTurnRequest.Extraction(
            "Extract.", "bench 225x5", "interpretation", MinimalSchema, 2048));

        var format = Request(Assert.Single(_server.ReceivedRequests))
            .GetProperty("output_config").GetProperty("format");

        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.Equal("object", format.GetProperty("schema").GetProperty("type").GetString());
        Assert.True(format.GetProperty("schema").GetProperty("properties").TryGetProperty("kind", out _));
    }

    /// <summary>
    /// Extraction gains nothing from a reasoning pass, and asking for depth is what empties the
    /// answer. Effort is nested under <c>output_config</c>, not top-level.
    /// </summary>
    [Fact]
    public async Task Extraction_asks_for_the_shallowest_reasoning_available()
    {
        _server.Responds(CannedAnthropic.Text("{}"));

        await Respond(ChatTurnRequest.Extraction(
            "Extract.", "bench 225x5", "interpretation", MinimalSchema, 2048));

        var sent = Request(Assert.Single(_server.ReceivedRequests));
        Assert.Equal("low", sent.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.False(sent.TryGetProperty("effort", out _));
    }

    /// <summary>Conversation leaves the depth to the model's own default rather than pinning it.</summary>
    [Fact]
    public async Task Conversation_does_not_pin_a_reasoning_depth()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        await Respond();

        var sent = Request(Assert.Single(_server.ReceivedRequests));
        Assert.False(
            sent.TryGetProperty("output_config", out var config) && config.TryGetProperty("effort", out _));
    }

    /// <summary>
    /// A request that offers no tools should not carry the field, rather than carry it empty.
    /// </summary>
    [Fact]
    public async Task Offering_no_tools_leaves_the_field_off_the_request()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        await Respond();

        Assert.False(Request(Assert.Single(_server.ReceivedRequests)).TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task A_tool_is_described_with_its_schema_properties_and_required_names()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        await _server.CreateChatModel().RespondAsync(new ChatTurnRequest(
            "You are a coach.",
            [new ModelMessage.UserSaid("how many sets?")],
            [new ToolDefinition("count_sets", "Counts sets.",
                """{"type":"object","properties":{"days":{"type":"integer"}},"required":["days"]}""")],
            new OutputContract.FreeText(),
            ReasoningDepth.Standard,
            1024));

        var tool = Assert.Single(
            Request(Assert.Single(_server.ReceivedRequests)).GetProperty("tools").EnumerateArray());

        Assert.Equal("count_sets", tool.GetProperty("name").GetString());
        Assert.Equal("Counts sets.", tool.GetProperty("description").GetString());

        var schema = tool.GetProperty("input_schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal("integer", schema.GetProperty("properties").GetProperty("days").GetProperty("type").GetString());
        Assert.Equal(["days"], schema.GetProperty("required").EnumerateArray().Select(n => n.GetString()));
    }

    // ---- Echoing a turn back ----

    /// <summary>
    /// There is no <c>ToParam()</c>, so the turn is rebuilt block by block. The API verifies a
    /// thinking block's signature, and a turn whose reasoning was dropped or altered is rejected
    /// outright — this is the assertion that catches that.
    /// </summary>
    [Fact]
    public async Task Replaying_a_turn_carries_the_thinking_signature_through_unchanged()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        var replayed = await EchoedRequest();

        var thinking = Assert.Single(replayed, b => b.GetProperty("type").GetString() == "thinking");
        Assert.Equal(CannedAnthropic.ThinkingSignature, thinking.GetProperty("signature").GetString());
        Assert.Equal("Let me count the sets.", thinking.GetProperty("thinking").GetString());
    }

    [Fact]
    public async Task Replaying_a_turn_carries_its_tool_request_so_the_result_has_a_match()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        var replayed = await EchoedRequest();

        var toolUse = Assert.Single(replayed, b => b.GetProperty("type").GetString() == "tool_use");
        Assert.Equal("toolu_7", toolUse.GetProperty("id").GetString());
        Assert.Equal("count_sets", toolUse.GetProperty("name").GetString());
        Assert.Equal(5, toolUse.GetProperty("input").GetProperty("days").GetInt32());
    }

    /// <summary>
    /// Every <c>tool_use</c> needs exactly one matching <c>tool_result</c> in the follow-up, and a
    /// failed call has to be marked as one so the model knows to recover rather than to trust it.
    /// </summary>
    [Fact]
    public async Task A_tool_result_is_sent_back_against_the_id_that_asked_for_it()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        await EchoedRequest();

        var messages = Request(_server.ReceivedRequests[^1]).GetProperty("messages").EnumerateArray().ToList();
        var result = Assert.Single(
            messages[^1].GetProperty("content").EnumerateArray().ToList(),
            b => b.GetProperty("type").GetString() == "tool_result");
        Assert.Equal("toolu_7", result.GetProperty("tool_use_id").GetString());
        Assert.Equal("15", result.GetProperty("content").GetString());
        Assert.False(result.GetProperty("is_error").GetBoolean());
    }

    [Fact]
    public async Task A_failed_tool_result_is_marked_as_an_error()
    {
        _server.Responds(CannedAnthropic.Text("ok"));

        await EchoedRequest(ToolOutcome.Refused("toolu_7", "No such tool is available."));

        var messages = Request(_server.ReceivedRequests[^1]).GetProperty("messages").EnumerateArray().ToList();
        var result = Assert.Single(
            messages[^1].GetProperty("content").EnumerateArray().ToList(),
            b => b.GetProperty("type").GetString() == "tool_result");

        Assert.True(result.GetProperty("is_error").GetBoolean());
        Assert.Contains("No such tool", result.GetProperty("content").GetString());
    }

    /// <summary>
    /// Runs one round that asks for a tool, then sends the turn and its result back — the exact
    /// shape the tool loop produces. Returns the replayed assistant turn's blocks.
    /// </summary>
    private async Task<List<JsonElement>> EchoedRequest(ToolOutcome? outcome = null)
    {
        using var first = new FakeAnthropicServer();
        first.Responds(CannedAnthropic.ThinkingThenToolUse(
            "Let me count the sets.", "count_sets", """{"days":5}""", "toolu_7"));

        var requested = Assert.IsType<ModelTurn.RequestedTools>(ResultAssert.Succeeded(
            await first.CreateChatModel().RespondAsync(
                ChatTurnRequest.Prose("You are a coach.", "how many sets?", 1024))));

        await _server.CreateChatModel().RespondAsync(new ChatTurnRequest(
            "You are a coach.",
            [
                new ModelMessage.UserSaid("how many sets?"),
                new ModelMessage.AssistantActed(requested.Turn),
                new ModelMessage.ToolsAnswered([outcome ?? ToolOutcome.Produced("toolu_7", "15")]),
            ],
            [],
            new OutputContract.FreeText(),
            ReasoningDepth.Standard,
            1024));

        var messages = Request(_server.ReceivedRequests[^1]).GetProperty("messages").EnumerateArray().ToList();
        var assistant = Assert.Single(messages, m => m.GetProperty("role").GetString() == "assistant");

        return [.. assistant.GetProperty("content").EnumerateArray()];
    }

    private static JsonElement Request(string body) => JsonDocument.Parse(body).RootElement.Clone();

    private const string MinimalSchema =
        """{"type":"object","properties":{"kind":{"type":"string"}},"required":["kind"]}""";
}
