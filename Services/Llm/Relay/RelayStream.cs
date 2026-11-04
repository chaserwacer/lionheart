using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace lionheart.Services.Llm.Relay;

/// <summary>
/// A complete Messages API response told again as the event stream the API sends when a request
/// asks to stream: <c>message_start</c>, each content block opened, written and closed, then
/// <c>message_delta</c> with the stop reason and <c>message_stop</c>.
/// </summary>
/// <remarks>
/// Pure, so the relay and the test double of Anthropic tell a stream the same way, and the SDK's
/// own stream reader is what reads it back. Text is cut word by word, each word a delta the
/// relay paces (<see cref="Paced"/>), which is what makes a hand-written answer arrive the way a
/// model's does. Thinking and tool calls go in one delta each: the athlete never sees them written.
/// </remarks>
public static partial class RelayStream
{
    public const string ContentType = "text/event-stream";

    /// <summary>One event of the stream: its data, whose <c>type</c> is also its event name.</summary>
    /// <param name="Paced">Whether the relay waits before sending it: a word of the answer.</param>
    public sealed record Event(JsonObject Data, bool Paced)
    {
        /// <summary>The event in the wire's framing.</summary>
        public string Frame() => $"event: {(string?)Data["type"]}\ndata: {Data.ToJsonString()}\n\n";
    }

    public static IReadOnlyList<Event> Events(JsonObject completed)
    {
        var content = completed["content"] as JsonArray ?? [];
        var opening = (JsonObject)completed.DeepClone();
        opening["content"] = new JsonArray();
        opening["stop_reason"] = null;
        opening["stop_sequence"] = null;

        return
        [
            Unpaced(new JsonObject { ["type"] = "message_start", ["message"] = opening }),
            .. content.SelectMany((block, index) => Block((JsonObject)block!, index)),
            Unpaced(new JsonObject
            {
                ["type"] = "message_delta",
                ["delta"] = new JsonObject
                {
                    ["stop_reason"] = completed["stop_reason"]?.DeepClone(),
                    ["stop_sequence"] = completed["stop_sequence"]?.DeepClone(),
                    ["stop_details"] = completed["stop_details"]?.DeepClone(),
                },
                ["usage"] = new JsonObject { ["output_tokens"] = completed["usage"]?["output_tokens"]?.DeepClone() ?? 0 },
            }),
            Unpaced(new JsonObject { ["type"] = "message_stop" }),
        ];
    }

    /// <summary>The answer's text cut into words, each keeping the space after it, so the pieces rejoin exactly.</summary>
    public static IReadOnlyList<string> Words(string text) =>
        [.. Word().Matches(text).Select(match => match.Value)];

    private static IEnumerable<Event> Block(JsonObject block, int index)
    {
        var kind = (string?)block["type"];
        var opened = kind switch
        {
            "text" => new JsonObject { ["type"] = "text", ["text"] = "" },
            "thinking" => new JsonObject { ["type"] = "thinking", ["thinking"] = "", ["signature"] = "" },
            "tool_use" => new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = block["id"]?.DeepClone(),
                ["name"] = block["name"]?.DeepClone(),
                ["input"] = new JsonObject(),
            },
            _ => (JsonObject)block.DeepClone(),
        };

        yield return Unpaced(new JsonObject { ["type"] = "content_block_start", ["index"] = index, ["content_block"] = opened });

        var deltas = kind switch
        {
            "text" => Words((string?)block["text"] ?? string.Empty)
                .Select(word => new Event(Delta(index, new JsonObject { ["type"] = "text_delta", ["text"] = word }), Paced: true)),
            "thinking" =>
            [
                Unpaced(Delta(index, new JsonObject { ["type"] = "thinking_delta", ["thinking"] = (string?)block["thinking"] ?? "" })),
                Unpaced(Delta(index, new JsonObject { ["type"] = "signature_delta", ["signature"] = (string?)block["signature"] ?? "" })),
            ],
            "tool_use" =>
            [
                Unpaced(Delta(index, new JsonObject
                {
                    ["type"] = "input_json_delta",
                    ["partial_json"] = block["input"]?.ToJsonString() ?? "{}",
                })),
            ],
            _ => Enumerable.Empty<Event>(),
        };

        foreach (var delta in deltas) yield return delta;

        yield return Unpaced(new JsonObject { ["type"] = "content_block_stop", ["index"] = index });
    }

    private static JsonObject Delta(int index, JsonObject delta) =>
        new() { ["type"] = "content_block_delta", ["index"] = index, ["delta"] = delta };

    private static Event Unpaced(JsonObject data) => new(data, Paced: false);

    [GeneratedRegex(@"\s*\S+\s*")]
    private static partial Regex Word();
}
