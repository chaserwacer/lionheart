using lionheart.Functional;
using lionheart.Services.Chat;
using lionheart.Tests.Support;
using OpenAI.Chat;

namespace lionheart.Tests.Llm;

/// <summary>
/// The fake transport is load-bearing for every other mocked-LLM test, so it gets its own
/// coverage: canned bodies must arrive as real SDK objects, and the LM Studio failure modes
/// must come back through ToResult as failed Results rather than exceptions.
/// </summary>
public sealed class FakeChatServerTests : IDisposable
{
    private readonly FakeChatServer _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task A_canned_content_response_arrives_as_a_completion()
    {
        _server.Responds(CannedChat.Content("worked up to 235"));

        var result = (await _server.CreateChatClient().CompleteChatAsync([new UserChatMessage("hi")])).ToResult();

        var completion = ResultAssert.Succeeded(result);
        Assert.Equal(ChatFinishReason.Stop, completion.FinishReason);
        Assert.Equal("worked up to 235", string.Join("", completion.Content.Select(c => c.Text)));
        Assert.Equal(7, completion.Usage.OutputTokenCount);
    }

    [Fact]
    public async Task A_canned_tool_call_arrives_with_its_name_and_arguments()
    {
        _server.Responds(CannedChat.ToolCall("get_wellness", """{"days":7}"""));

        var result = (await _server.CreateChatClient().CompleteChatAsync([new UserChatMessage("hi")])).ToResult();

        var completion = ResultAssert.Succeeded(result);
        Assert.Equal(ChatFinishReason.ToolCalls, completion.FinishReason);
        var call = Assert.Single(completion.ToolCalls);
        Assert.Equal("get_wellness", call.FunctionName);
        Assert.Equal("""{"days":7}""", call.FunctionArguments.ToString());
    }

    /// <summary>
    /// The case ToResult exists for: LM Studio reports "model not loaded" as HTTP 200 with an
    /// error object and no choices, which the SDK trusts.
    /// </summary>
    [Fact]
    public async Task An_error_body_returned_with_http_200_becomes_a_failed_result()
    {
        _server.Responds(CannedChat.Error("Model 'qwen' is not loaded."));

        var result = (await _server.CreateChatClient().CompleteChatAsync([new UserChatMessage("hi")])).ToResult();

        Assert.Contains("Model 'qwen' is not loaded.", ResultAssert.Failed(result).Description);
    }

    [Fact]
    public async Task An_empty_choices_array_becomes_a_failed_result()
    {
        _server.Responds(CannedChat.NoChoices());

        var result = (await _server.CreateChatClient().CompleteChatAsync([new UserChatMessage("hi")])).ToResult();

        Assert.Contains("no choices", ResultAssert.Failed(result).Description);
    }

    [Fact]
    public async Task The_request_the_sdk_sent_is_recorded_for_assertions()
    {
        _server.Responds(CannedChat.Content("ok"));

        await _server.CreateChatClient("my-model").CompleteChatAsync([new UserChatMessage("sled pulls")]);

        var request = Assert.Single(_server.ReceivedRequests);
        Assert.Contains("sled pulls", request);
        Assert.Contains("my-model", request);
    }
}
