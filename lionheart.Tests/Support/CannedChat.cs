using System.Text.Json;

namespace lionheart.Tests.Support;

/// <summary>
/// OpenAI chat-completion response bodies, built as JSON rather than as SDK objects so the
/// tests describe what the server sends and the SDK does the interpreting — including the
/// shapes an SDK builder would not let us express (a bad finish_reason, tool arguments that
/// are not valid JSON, a 200 carrying an error).
/// </summary>
/// <remarks>
/// Composed by concatenation rather than raw interpolated strings: JSON is dense in braces,
/// and the escaping needed to interpolate into it is harder to read than the concatenation.
/// </remarks>
public static class CannedChat
{
    public static string Content(string text, string finishReason = "stop") =>
        Envelope(
            "{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":" + Quote(text) + "},"
            + "\"finish_reason\":" + Quote(finishReason) + "}");

    public static string Refusal(string refusal) =>
        Envelope(
            "{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":null,\"refusal\":" + Quote(refusal) + "},"
            + "\"finish_reason\":\"stop\"}");

    /// <param name="argumentsJson">Sent verbatim as the arguments string, valid JSON or not.</param>
    public static string ToolCall(string toolName, string argumentsJson, string callId = "call_1") =>
        ToolCalls((toolName, argumentsJson, callId));

    public static string ToolCalls(params (string Name, string ArgumentsJson, string CallId)[] calls)
    {
        var rendered = string.Join(",", calls.Select(call =>
            "{\"id\":" + Quote(call.CallId) + ",\"type\":\"function\",\"function\":{"
            + "\"name\":" + Quote(call.Name) + ",\"arguments\":" + Quote(call.ArgumentsJson) + "}}"));

        return Envelope(
            "{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[" + rendered + "]},"
            + "\"finish_reason\":\"tool_calls\"}");
    }

    /// <summary>The LM Studio failure mode that returns HTTP 200 with an error body and no choices.</summary>
    public static string Error(string message) =>
        "{\"error\":{\"message\":" + Quote(message) + ",\"type\":\"invalid_request_error\"}}";

    public static string NoChoices() =>
        "{\"id\":\"chatcmpl-1\",\"object\":\"chat.completion\",\"created\":1780000000,"
        + "\"model\":\"test-model\",\"choices\":[]}";

    private static string Envelope(string choice) =>
        "{\"id\":\"chatcmpl-1\",\"object\":\"chat.completion\",\"created\":1780000000,\"model\":\"test-model\","
        + "\"choices\":[" + choice + "],"
        + "\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":7,\"total_tokens\":18}}";

    private static string Quote(string value) => JsonSerializer.Serialize(value);
}
