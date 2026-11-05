using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Tools;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Completion;
using Services.Chat;

namespace lionheart.Tests.ChatChanges;

/// <summary>
/// The tool loop with a proposing tool: offered only where a card can be answered, its draft carried
/// out of the exchange to be stored, and a call to it refused anywhere else whatever the model asks.
/// </summary>
public sealed class ProposingExchangeTests : IDisposable
{
    private readonly FakeChatServer _server = new();
    private readonly TestToolbox _toolbox = new();

    public void Dispose()
    {
        _server.Dispose();
        _toolbox.Dispose();
    }

    private Task<Result<ChatCompletionResponse, Problem>> Complete(ToolAccess tools) =>
        new ChatCompletionService(_server.CreateChatModel(), _toolbox.Registry, _toolbox.Executor)
            .GenerateChatCompletionAsync(
                new IdentityUser("tester@lionheart.local"),
                new ChatCompletionRequest
                {
                    Conversation = AConversation.With("plan wednesday"),
                    MaxOutputTokens = 1024,
                    Tools = tools,
                });

    [Fact]
    public async Task A_proposal_comes_out_of_the_exchange_and_the_model_is_told_nothing_is_written_yet()
    {
        _server.Responds(CannedChat.ToolCall("propose", """{"phrase":"Plan a run"}"""));
        _server.Responds(CannedChat.Content("It is on a card for you."));

        var response = ResultAssert.Succeeded(await Complete(ToolAccess.ReadAndPropose));

        var proposal = Assert.Single(response.Proposals);
        Assert.Equal("Plan a run", proposal.Summary.Title);
        Assert.Contains("Nothing is written until he confirms it", _server.ReceivedRequests[1]);
        Assert.Contains(proposal.ToldToModel, Assert.IsType<LHChatToolCallResult>(response.CompletionGeneratedMessages[0]).Content);
    }

    [Fact]
    public async Task A_refused_draft_is_a_failed_call_the_model_hears_and_no_proposal()
    {
        _server.Responds(CannedChat.ToolCall("propose", """{"phrase":"refuse"}"""));
        _server.Responds(CannedChat.Content("Which day?"));

        var response = ResultAssert.Succeeded(await Complete(ToolAccess.ReadAndPropose));

        Assert.Empty(response.Proposals);
        Assert.Contains("Tool call failed: Say what to change.", _server.ReceivedRequests[1]);
    }

    [Fact]
    public async Task A_conversation_that_only_reads_is_not_offered_the_proposing_tools()
    {
        _server.Responds(CannedChat.Content("Four sessions."));

        await Complete(ToolAccess.Read);

        Assert.DoesNotContain("\"propose\"", _server.ReceivedRequests[0]);
        Assert.Contains("\"echo\"", _server.ReceivedRequests[0]);
    }

    [Fact]
    public async Task A_proposing_tool_called_where_it_was_not_offered_is_refused_and_never_runs()
    {
        _server.Responds(CannedChat.ToolCall("propose", """{"phrase":"Plan a run"}"""));
        _server.Responds(CannedChat.Content("I cannot do that here."));

        var response = ResultAssert.Succeeded(await Complete(ToolAccess.Read));

        Assert.Empty(response.Proposals);
        Assert.Empty(_toolbox.Tools.Calls);
        Assert.Contains("not available in this conversation", _server.ReceivedRequests[1]);
    }
}
