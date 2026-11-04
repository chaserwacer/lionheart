using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Services.Llm;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Completion;
using Services.Chat;

namespace lionheart.Tests.Llm;

/// <summary>
/// The answer arriving as it is written, over the Anthropic provider's stream: what the athlete is
/// shown while the exchange runs has to end as exactly what is stored.
/// </summary>
public sealed class AnswerStreamingTests : IDisposable
{
    private readonly FakeAnthropicServer _server = new();
    private readonly TestToolbox _toolbox = new();
    private readonly Heard _heard = new();

    public void Dispose()
    {
        _server.Dispose();
        _toolbox.Dispose();
    }

    private Task<Result<ChatCompletionResponse, Problem>> Complete() =>
        new ChatCompletionService(_server.CreateChatModel(), _toolbox.Registry, _toolbox.Executor)
            .GenerateChatCompletionAsync(
                new IdentityUser("tester@lionheart.local"),
                new ChatCompletionRequest
                {
                    Conversation = AConversation.With("how did last week go?"),
                    MaxOutputTokens = 1024,
                    Observer = _heard,
                });

    private static string Stored(ChatCompletionResponse response) =>
        response.CompletionGeneratedMessages.OfType<LHModelChatMessage>().Single().Content!;

    [Fact]
    public async Task The_answer_arrives_in_pieces_that_make_up_exactly_the_stored_answer()
    {
        _server.Responds(CannedAnthropic.ThinkingThenText("Weighing it.", "Four sessions, squat volume up on the week."));

        var response = ResultAssert.Succeeded(await Complete());

        Assert.True(_heard.Written.Count > 1);
        Assert.Equal(Stored(response), string.Concat(_heard.Written));
    }

    [Fact]
    public async Task Reasoning_is_never_written_to_the_athlete()
    {
        _server.Responds(CannedAnthropic.ThinkingThenText("private reasoning", "Train."));

        await Complete();

        Assert.DoesNotContain(_heard.Written, piece => piece.Contains("reasoning"));
    }

    [Fact]
    public async Task A_round_of_tools_is_announced_after_its_preamble_and_before_the_answer()
    {
        _server.Responds(CannedAnthropic.TextThenToolUse("Let me look.", "echo", """{"phrase":"sled pulls"}"""));
        _server.Responds(CannedAnthropic.Text("Got it."));

        var response = ResultAssert.Succeeded(await Complete());

        var consulting = Assert.Single(_heard.Progress.OfType<ExchangeProgress.Consulting>());
        Assert.Equal(["echo"], consulting.Tools);
        Assert.Equal("Let me look.", _heard.WrittenBefore(consulting));
        Assert.Equal(Stored(response), _heard.WrittenAfter(consulting));
    }

    [Fact]
    public async Task A_round_asking_for_one_tool_twice_names_it_once()
    {
        _server.Responds(CannedAnthropic.ToolUses(
            ("echo", """{"phrase":"a"}""", "toolu_1"),
            ("echo", """{"phrase":"b"}""", "toolu_2")));
        _server.Responds(CannedAnthropic.Text("Both."));

        await Complete();

        Assert.Equal(["echo"], Assert.Single(_heard.Progress.OfType<ExchangeProgress.Consulting>()).Tools);
    }

    [Fact]
    public async Task A_refused_stream_fails_the_exchange_as_a_refused_request_does()
    {
        _server.Responds(CannedAnthropic.Refusal("Declined."));

        var problem = ResultAssert.Failed(await Complete());

        Assert.IsType<Problem.Upstream>(problem);
    }

    private sealed class Heard : IExchangeObserver
    {
        public List<ExchangeProgress> Progress { get; } = [];

        public IReadOnlyList<string> Written =>
            [.. Progress.OfType<ExchangeProgress.Wrote>().Select(wrote => wrote.Text)];

        public void Observe(ExchangeProgress progress) => Progress.Add(progress);

        public string WrittenBefore(ExchangeProgress mark) =>
            string.Concat(Progress.TakeWhile(p => p != mark).OfType<ExchangeProgress.Wrote>().Select(w => w.Text));

        public string WrittenAfter(ExchangeProgress mark) =>
            string.Concat(Progress.SkipWhile(p => p != mark).Skip(1).OfType<ExchangeProgress.Wrote>().Select(w => w.Text));
    }
}
