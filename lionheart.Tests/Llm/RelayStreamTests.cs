using System.Text.Json.Nodes;
using lionheart.Services.Llm.Relay;

namespace lionheart.Tests.Llm;

/// <summary>A complete answer told again as the API's event stream.</summary>
public sealed class RelayStreamTests
{
    [Theory]
    [InlineData("Train.")]
    [InlineData("  Leading space, double  spaces and a trailing one ")]
    [InlineData("Line one.\n\n- a list item\n- another")]
    public void Words_rejoin_to_exactly_the_text(string text) =>
        Assert.Equal(text, string.Concat(RelayStream.Words(text)));

    [Fact]
    public void The_stream_opens_with_an_empty_message_and_closes_with_its_stop_reason()
    {
        var events = RelayStream.Events(Message("""[{"type":"text","text":"Rest today."}]""", "end_turn"));

        var opening = events[0].Data;
        Assert.Equal("message_start", (string?)opening["type"]);
        Assert.Empty(opening["message"]!["content"]!.AsArray());
        Assert.Null(opening["message"]!["stop_reason"]);

        Assert.Equal("end_turn", (string?)events[^2].Data["delta"]!["stop_reason"]);
        Assert.Equal("message_stop", (string?)events[^1].Data["type"]);
    }

    [Fact]
    public void Only_the_answers_words_are_paced()
    {
        var events = RelayStream.Events(Message(
            """[{"type":"thinking","thinking":"hm","signature":"sig"},{"type":"text","text":"Rest today."}]""", "end_turn"));

        var paced = events.Where(e => e.Paced).Select(e => (string?)e.Data["delta"]!["text"]);
        Assert.Equal(["Rest ", "today."], paced);
    }

    [Fact]
    public void A_frame_names_its_event_and_carries_its_data_on_one_line()
    {
        var frame = RelayStream.Events(Message("""[{"type":"text","text":"a\nb"}]""", "end_turn"))
            .First(e => e.Paced).Frame();

        Assert.StartsWith("event: content_block_delta\ndata: {", frame);
        Assert.EndsWith("}\n\n", frame);
        Assert.Equal(3, frame.Split('\n').Length - 1);
    }

    private static JsonObject Message(string content, string stopReason) =>
        JsonNode.Parse($$$"""
            {"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":{{{content}}},
             "stop_reason":"{{{stopReason}}}","stop_sequence":null,"usage":{"input_tokens":3,"output_tokens":4}}
            """)!.AsObject();
}
