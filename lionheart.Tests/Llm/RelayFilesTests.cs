using System.Text.Json.Nodes;
using lionheart.Services.Llm.Relay;

namespace lionheart.Tests.Llm;

/// <summary>
/// The shapes the relay reads and writes: which caller a request came from, and a hand-written
/// answer completed into a response the Anthropic SDK accepts.
/// </summary>
public sealed class RelayFilesTests
{
    private static JsonObject Request(string json) => JsonNode.Parse(json)!.AsObject();

    [Theory]
    [InlineData("""{"tools":[{"name":"GetTrainingScore"}],"messages":[]}""", "chat")]
    [InlineData("""{"output_config":{"format":{"type":"json_schema"}},"messages":[]}""", "extract")]
    [InlineData("""{"output_config":{"effort":"low"},"messages":[]}""", "prose")]
    public void A_request_is_labelled_by_its_shape(string json, string kind) =>
        Assert.Equal(kind, RelayFiles.KindOf(Request(json)));

    [Fact]
    public void A_text_answer_ends_the_turn_under_the_requested_model()
    {
        var response = RelayFiles.Completed(RelayFiles.FromText("Train.\n"), Request("""{"model":"claude-opus-5-5"}"""), 3);

        Assert.Equal("end_turn", (string?)response["stop_reason"]);
        Assert.Equal("claude-opus-5-5", (string?)response["model"]);
        Assert.Equal("assistant", (string?)response["role"]);
        Assert.Equal("Train.", (string?)response["content"]![0]!["text"]);
        Assert.NotNull(response["usage"]?["input_tokens"]);
    }

    [Fact]
    public void A_tool_call_stops_for_tool_use_and_gets_an_id_only_where_none_was_written()
    {
        var answer = Request("""
            {"content":[{"type":"tool_use","name":"GetOuraDays","input":{}},
                        {"type":"tool_use","id":"toolu_mine","name":"GetTrainingScore","input":{}}]}
            """);

        var response = RelayFiles.Completed(answer, Request("{}"), 12);

        Assert.Equal("tool_use", (string?)response["stop_reason"]);
        Assert.Equal("toolu_relay_0012_0", (string?)response["content"]![0]!["id"]);
        Assert.Equal("toolu_mine", (string?)response["content"]![1]!["id"]);
    }

    [Fact]
    public void What_the_person_wrote_is_kept_as_written()
    {
        var response = RelayFiles.Completed(
            Request("""{"stop_reason":"max_tokens","content":[{"type":"text","text":"cut"}]}"""), Request("{}"), 1);

        Assert.Equal("max_tokens", (string?)response["stop_reason"]);
    }

    [Fact]
    public void The_readable_request_shows_the_system_note_and_where_the_cache_breakpoints_are()
    {
        var readable = RelayFiles.Readable(Request("""
            {"model":"claude-opus-5-5","max_tokens":10,"system":"You are Lionheart.",
             "messages":[{"role":"user","content":[{"type":"text","text":"train?","cache_control":{"type":"ephemeral","ttl":"1h"}}]},
                         {"role":"system","content":"TODAY"}]}
            """), 4, "chat");

        Assert.Contains("messages[0].content[0] (1h)", readable);
        Assert.Contains("### [1] system", readable);
        Assert.Contains("0004.response.txt", readable);
    }
}
