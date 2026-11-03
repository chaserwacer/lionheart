using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Completion;
using Services.Chat;

namespace lionheart.Tests.Llm;

/// <summary>
/// The tool-call loop, driven entirely by canned model output. Everything the model can do to
/// this loop — ask for a tool that does not exist, send arguments that are not JSON, ask twice,
/// answer with nothing at all — is a wire-level response, so all of it is reachable here.
/// </summary>
public sealed class ChatCompletionServiceTests : IDisposable
{
    private readonly FakeChatServer _server = new();
    private readonly TestToolbox _toolbox = new();

    public void Dispose()
    {
        _server.Dispose();
        _toolbox.Dispose();
    }

    private Task<Result<ChatCompletionResponse, Problem>> Complete(string userMessage = "how did last week go?")
    {
        var service = new ChatCompletionService(
            _server.CreateChatModel(), _toolbox.Registry, _toolbox.Executor);

        return service.GenerateChatCompletionAsync(
            new IdentityUser("tester@lionheart.local"),
            new ChatCompletionRequest
            {
                Conversation = AConversation.With(userMessage),
                MaxOutputTokens = 1024,
            });
    }

    [Fact]
    public async Task A_plain_answer_returns_one_model_message()
    {
        _server.Responds(CannedChat.Content("You logged four sessions."));

        var result = await Complete();

        var response = ResultAssert.Succeeded(result);
        var message = Assert.IsType<LHModelChatMessage>(Assert.Single(response.CompletionGeneratedMessages));
        Assert.Equal("You logged four sessions.", message.Content);
        Assert.Equal(message.ChatMessageItemID, response.ModelFinalChatMessageID);
    }

    [Fact]
    public async Task A_tool_call_is_executed_and_its_result_persisted_before_the_final_answer()
    {
        _server.Responds(CannedChat.ToolCall("echo", """{"phrase":"sled pulls"}"""));
        _server.Responds(CannedChat.Content("Got it."));

        var result = await Complete();

        var response = ResultAssert.Succeeded(result);
        Assert.Equal(["echo:sled pulls"], _toolbox.Tools.Calls);
        Assert.Equal(2, _server.RequestCount);

        var toolResult = Assert.IsType<LHChatToolCallResult>(response.CompletionGeneratedMessages[0]);
        Assert.Contains("you said sled pulls", toolResult.Content);
        Assert.IsType<LHModelChatMessage>(response.CompletionGeneratedMessages[1]);
    }

    [Fact]
    public async Task The_tool_result_is_sent_back_to_the_model_on_the_next_round()
    {
        _server.Responds(CannedChat.ToolCall("echo", """{"phrase":"sled pulls"}"""));
        _server.Responds(CannedChat.Content("Got it."));

        await Complete();

        Assert.Contains("you said sled pulls", _server.ReceivedRequests[1]);
    }

    [Fact]
    public async Task Several_tool_calls_in_one_round_all_execute()
    {
        _server.Responds(CannedChat.ToolCalls(
            ("echo", """{"phrase":"a"}""", "call_1"),
            ("count_sets", """{"days":2}""", "call_2")));
        _server.Responds(CannedChat.Content("done"));

        var result = await Complete();

        Assert.True(result.IsSuccess);
        Assert.Equal(["echo:a", "count:2"], _toolbox.Tools.Calls);
    }

    [Fact]
    public async Task A_multi_round_tool_chain_keeps_going_until_the_model_stops()
    {
        _server.Responds(CannedChat.ToolCall("echo", """{"phrase":"one"}""", "call_1"));
        _server.Responds(CannedChat.ToolCall("count_sets", """{"days":3}""", "call_2"));
        _server.Responds(CannedChat.Content("finished"));

        var result = await Complete();

        Assert.True(result.IsSuccess);
        Assert.Equal(["echo:one", "count:3"], _toolbox.Tools.Calls);
        Assert.Equal(3, _server.RequestCount);
    }

    [Fact]
    public async Task An_unknown_tool_does_not_abort_the_conversation()
    {
        _server.Responds(CannedChat.ToolCall("summon_demon", """{"phrase":"x"}"""));
        _server.Responds(CannedChat.Content("I cannot do that, but here is what I know."));

        var result = await Complete();

        Assert.True(result.IsSuccess);
        Assert.Empty(_toolbox.Tools.Calls);
        Assert.Equal(2, _server.RequestCount);
    }

    [Fact]
    public async Task Malformed_tool_arguments_do_not_abort_the_conversation()
    {
        _server.Responds(CannedChat.ToolCall("count_sets", "{not json at all"));
        _server.Responds(CannedChat.Content("Let me try again."));

        var result = await Complete();

        Assert.True(result.IsSuccess);
        Assert.Empty(_toolbox.Tools.Calls);
    }

    [Fact]
    public async Task A_throwing_tool_does_not_abort_the_conversation()
    {
        _server.Responds(CannedChat.ToolCall("boom", """{"phrase":"kaboom"}"""));
        _server.Responds(CannedChat.Content("That tool failed; here is a summary instead."));

        var result = await Complete();

        var response = ResultAssert.Succeeded(result);
        Assert.Equal(["boom:kaboom"], _toolbox.Tools.Calls);
        Assert.Single(response.CompletionGeneratedMessages);
    }

    /// <summary>
    /// A failed tool call is the model's cue to recover, so the reason has to reach it. A
    /// failure channel that stringifies badly once told the model the tool had returned the
    /// literal text "System.String[]".
    /// </summary>
    [Fact]
    public async Task An_unknown_tool_tells_the_model_the_tool_does_not_exist()
    {
        _server.Responds(CannedChat.ToolCall("summon_demon", """{"phrase":"x"}"""));
        _server.Responds(CannedChat.Content("ok"));

        await Complete();

        Assert.DoesNotContain("System.String[]", _server.ReceivedRequests[1]);
        Assert.Contains("No such tool is available.", _server.ReceivedRequests[1]);
    }

    [Fact]
    public async Task Malformed_arguments_tell_the_model_which_argument_was_bad()
    {
        _server.Responds(CannedChat.ToolCall("count_sets", "{not json at all"));
        _server.Responds(CannedChat.Content("ok"));

        await Complete();

        Assert.Contains("Invalid JSON for argument", _server.ReceivedRequests[1]);
        Assert.Contains("days", _server.ReceivedRequests[1]);
    }

    [Fact]
    public async Task A_throwing_tool_tells_the_model_the_call_failed()
    {
        _server.Responds(CannedChat.ToolCall("boom", """{"phrase":"kaboom"}"""));
        _server.Responds(CannedChat.Content("ok"));

        await Complete();

        Assert.DoesNotContain("System.String[]", _server.ReceivedRequests[1]);
        Assert.Contains("Tool call failed", _server.ReceivedRequests[1]);
    }

    [Fact]
    public async Task Empty_model_output_still_succeeds_with_an_empty_message()
    {
        _server.Responds(CannedChat.Content(string.Empty));

        var result = await Complete();

        var response = ResultAssert.Succeeded(result);
        Assert.Equal(string.Empty, Assert.Single(response.CompletionGeneratedMessages).Content);
    }

    [Fact]
    public async Task Whitespace_model_output_is_preserved_rather_than_trimmed_away()
    {
        _server.Responds(CannedChat.Content("   \n  "));

        var result = await Complete();

        var response = ResultAssert.Succeeded(result);
        Assert.Equal("   \n  ", Assert.Single(response.CompletionGeneratedMessages).Content);
    }

    [Fact]
    public async Task A_length_finish_reason_fails_with_a_token_limit_message()
    {
        _server.Responds(CannedChat.Content("truncated mid-sen", finishReason: "length"));

        var result = await Complete();

        Assert.Contains("MaxTokens", ResultAssert.Failed(result).Description);
    }

    [Fact]
    public async Task A_content_filter_finish_reason_fails_with_a_filter_message()
    {
        _server.Responds(CannedChat.Content("", finishReason: "content_filter"));

        var result = await Complete();

        Assert.Contains("content filter", ResultAssert.Failed(result).Description);
    }

    [Fact]
    public async Task A_refusal_with_no_content_still_completes()
    {
        _server.Responds(CannedChat.Refusal("I will not provide medical advice."));

        var result = await Complete();

        var response = ResultAssert.Succeeded(result);
        Assert.Equal(string.Empty, Assert.Single(response.CompletionGeneratedMessages).Content);
    }

    [Fact]
    public async Task A_server_error_body_surfaces_as_a_failed_result()
    {
        _server.Responds(CannedChat.Error("Model 'qwen' is not loaded."));

        var result = await Complete();

        Assert.Contains("not loaded", ResultAssert.Failed(result).Description);
    }

    [Fact]
    public async Task The_system_prompt_is_always_the_first_message_sent()
    {
        _server.Responds(CannedChat.Content("ok"));

        await Complete("how did last week go?");

        var request = Assert.Single(_server.ReceivedRequests);
        Assert.True(
            request.IndexOf("You are a training assistant.", StringComparison.Ordinal)
            < request.IndexOf("how did last week go?", StringComparison.Ordinal));
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
}
