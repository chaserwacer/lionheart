using System.Text.Json;

namespace lionheart.Tests.Support;

/// <summary>
/// Anthropic Messages API response bodies, built as JSON rather than as SDK objects so the tests
/// describe what the server sends and the SDK does the interpreting — including the shapes an SDK
/// builder would not let us express (an unknown stop reason, a turn that is nothing but thinking,
/// a tool_use block whose input is not what the schema asked for).
/// </summary>
/// <remarks>
/// Composed by concatenation rather than raw interpolated strings: JSON is dense in braces, and
/// the escaping needed to interpolate into it is harder to read than the concatenation.
/// </remarks>
public static class CannedAnthropic
{
    public const string ThinkingSignature = "sig_abc123";

    public static string Text(string text, string stopReason = "end_turn") =>
        Envelope("[" + TextBlock(text) + "]", stopReason);

    /// <summary>
    /// A turn carrying thinking as well as the answer — the normal shape, since reasoning is
    /// always on and cannot be switched off.
    /// </summary>
    public static string ThinkingThenText(string thinking, string text) =>
        Envelope("[" + ThinkingBlock(thinking) + "," + TextBlock(text) + "]", "end_turn");

    /// <summary>A turn that is only thinking: no answer to read.</summary>
    public static string ThinkingOnly(string thinking) =>
        Envelope("[" + ThinkingBlock(thinking) + "]", "end_turn");

    /// <param name="inputJson">Sent verbatim as the tool input object, whatever it contains.</param>
    public static string ToolUse(string toolName, string inputJson, string callId = "toolu_1") =>
        ToolUses((toolName, inputJson, callId));

    /// <summary>
    /// A tool-use turn preceded by thinking, which is what a real turn looks like. The signature is
    /// what must come back unaltered in the follow-up request.
    /// </summary>
    public static string ThinkingThenToolUse(
        string thinking, string toolName, string inputJson, string callId = "toolu_1") =>
        Envelope(
            "[" + ThinkingBlock(thinking) + ","
            + "{\"type\":\"tool_use\",\"id\":" + Quote(callId) + ",\"name\":" + Quote(toolName)
            + ",\"input\":" + inputJson + "}]",
            "tool_use");

    public static string ToolUses(params (string Name, string InputJson, string CallId)[] calls)
    {
        var rendered = string.Join(",", calls.Select(call =>
            "{\"type\":\"tool_use\",\"id\":" + Quote(call.CallId) + ",\"name\":" + Quote(call.Name)
            + ",\"input\":" + call.InputJson + "}"));

        return Envelope("[" + rendered + "]", "tool_use");
    }

    /// <summary>A refusal, which has no answer in it to read — the stop reason is the whole story.</summary>
    public static string Refusal(string explanation) =>
        "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude-opus-5-5\","
        + "\"content\":[],\"stop_reason\":\"refusal\","
        + "\"stop_details\":{\"type\":\"refusal\",\"explanation\":" + Quote(explanation) + "},"
        + "\"usage\":{\"input_tokens\":11,\"output_tokens\":0}}";

    /// <summary>An API error body, as Anthropic returns it with a 4xx or 5xx status.</summary>
    public static string Error(string message, string type = "invalid_request_error") =>
        "{\"type\":\"error\",\"error\":{\"type\":" + Quote(type) + ",\"message\":" + Quote(message) + "}}";

    private static string TextBlock(string text) =>
        "{\"type\":\"text\",\"text\":" + Quote(text) + "}";

    private static string ThinkingBlock(string thinking) =>
        "{\"type\":\"thinking\",\"thinking\":" + Quote(thinking)
        + ",\"signature\":" + Quote(ThinkingSignature) + "}";

    private static string Envelope(string contentJson, string stopReason) =>
        "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude-opus-5-5\","
        + "\"content\":" + contentJson + ","
        + "\"stop_reason\":" + Quote(stopReason) + ",\"stop_sequence\":null,"
        + "\"usage\":{\"input_tokens\":11,\"output_tokens\":7}}";

    private static string Quote(string value) => JsonSerializer.Serialize(value);
}
