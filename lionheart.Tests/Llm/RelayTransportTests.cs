using System.Text.Json.Nodes;
using lionheart.Services.Llm;
using lionheart.Services.Llm.Anthropic;
using lionheart.Services.Llm.Relay;
using lionheart.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace lionheart.Tests.Llm;

/// <summary>
/// The relay end to end: the real Anthropic provider sends a request, the request lands in the
/// folder, and an answer written by hand comes back as the model's turn.
/// </summary>
public sealed class RelayTransportTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("lionheart-relay-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private IChatModel Model() =>
        new AnthropicChatModel(RelayTransport.Client(_folder, wordsPerSecond: 1000, NullLogger.Instance), "claude-opus-5-5", NullLogger.Instance);

    private static ChatTurnRequest Chat() => ChatTurnRequest.Conversation(
        "You are Lionheart.",
        [new ModelMessage.UserSaid("should I train today?")],
        [new ToolDefinition("GetTrainingScore", "The score.", """{"type":"object","properties":{}}""")],
        1024) with { Briefing = "TODAY\nNow: Wednesday." };

    /// <summary>Waits for the numbered request to appear, then answers it with the file given.</summary>
    private async Task AnswerAsync(int sequence, string fileName, string contents)
    {
        var request = Path.Combine(_folder, RelayFiles.RequestName(sequence));
        for (var i = 0; i < 200 && !File.Exists(request); i++) await Task.Delay(25);
        await File.WriteAllTextAsync(Path.Combine(_folder, fileName), contents);
    }

    [Fact]
    public async Task A_text_answer_comes_back_as_the_models_turn()
    {
        var turn = Model().RespondAsync(Chat());
        await AnswerAsync(1, RelayFiles.TextResponseName(1), "Train. Readiness is in range.");

        var answered = Assert.IsType<ModelTurn.Answered>(ResultAssert.Succeeded(await turn));
        Assert.Equal("Train. Readiness is in range.", answered.Text);
    }

    [Fact]
    public async Task The_request_file_is_the_body_production_would_send()
    {
        var turn = Model().RespondAsync(Chat());
        await AnswerAsync(1, RelayFiles.TextResponseName(1), "ok");
        await turn;

        var body = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(_folder, RelayFiles.RequestName(1))))!;
        Assert.Equal("claude-opus-5-5", (string?)body["model"]);
        Assert.Equal("GetTrainingScore", (string?)body["tools"]![0]!["name"]);
        Assert.Equal("system", (string?)body["messages"]!.AsArray()[^1]!["role"]);
        Assert.True(File.Exists(Path.Combine(_folder, RelayFiles.ReadableName(1, "chat"))));
    }

    [Fact]
    public async Task A_tool_call_written_without_an_id_is_given_one_the_next_round_can_answer()
    {
        var model = Model();
        var first = model.RespondAsync(Chat());
        await AnswerAsync(1, RelayFiles.ResponseName(1),
            """{"content":[{"type":"tool_use","name":"GetTrainingScore","input":{}}]}""");

        var requested = Assert.IsType<ModelTurn.RequestedTools>(ResultAssert.Succeeded(await first));
        var call = Assert.Single(requested.Calls);
        Assert.Equal("GetTrainingScore", call.ToolName);

        var second = model.RespondAsync(Chat() with
        {
            Messages =
            [
                new ModelMessage.UserSaid("should I train today?"),
                new ModelMessage.AssistantActed(requested.Turn),
                new ModelMessage.ToolsAnswered([ToolOutcome.Produced(call.CallId, "66 of 100")]),
            ],
        });
        await AnswerAsync(2, RelayFiles.TextResponseName(2), "Train.");

        Assert.IsType<ModelTurn.Answered>(ResultAssert.Succeeded(await second));
        Assert.Contains(call.CallId, await File.ReadAllTextAsync(Path.Combine(_folder, RelayFiles.RequestName(2))));
    }

    [Fact]
    public async Task Numbering_continues_after_the_requests_already_in_the_folder()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, RelayFiles.RequestName(7)), "{}");

        var turn = Model().RespondAsync(Chat());
        await AnswerAsync(8, RelayFiles.TextResponseName(8), "ok");

        ResultAssert.Succeeded(await turn);
    }

    [Fact]
    public async Task A_streamed_request_is_answered_word_by_word_and_ends_in_the_same_turn()
    {
        var written = new List<string>();
        var turn = Model().StreamAsync(Chat(), written.Add);
        await AnswerAsync(1, RelayFiles.TextResponseName(1), "Train. Readiness is in range.");

        var answered = Assert.IsType<ModelTurn.Answered>(ResultAssert.Succeeded(await turn));
        Assert.Equal("Train. Readiness is in range.", answered.Text);
        Assert.Equal(["Train. ", "Readiness ", "is ", "in ", "range."], written);
    }

    [Fact]
    public async Task A_streamed_tool_call_comes_back_as_the_same_request_for_tools()
    {
        var written = new List<string>();
        var turn = Model().StreamAsync(Chat(), written.Add);
        await AnswerAsync(1, RelayFiles.ResponseName(1),
            """{"content":[{"type":"tool_use","name":"GetTrainingScore","input":{"From":"2026-01-05"}}]}""");

        var requested = Assert.IsType<ModelTurn.RequestedTools>(ResultAssert.Succeeded(await turn));
        Assert.Equal("""{"From":"2026-01-05"}""", Assert.Single(requested.Calls).ArgumentsJson);
        Assert.Empty(written);
    }

    [Fact]
    public async Task An_abandoned_request_stops_waiting()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Model().RespondAsync(Chat(), cancel.Token));
    }
}
