using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace lionheart.Services.Llm.Relay;

/// <summary>
/// What the relay writes and reads: file names, a request rendered for a person to read, and a
/// hand-written answer completed into the Messages API response the SDK expects.
/// </summary>
/// <remarks>
/// Pure, so the shapes are tested without a folder. The token figures are estimates at four
/// characters a token; they are for comparing one prompt with another, not for billing.
/// </remarks>
public static class RelayFiles
{
    public const string RequestSuffix = ".request.json";

    /// <summary>The exact request body, as production would send it.</summary>
    public static string RequestName(int sequence) => $"{Number(sequence)}{RequestSuffix}";

    /// <summary>The same request laid out for reading, labelled with what kind of call it is.</summary>
    public static string ReadableName(int sequence, string kind) => $"{Number(sequence)}-{kind}.prompt.md";

    /// <summary>An answer in the Messages API's response shape.</summary>
    public static string ResponseName(int sequence) => $"{Number(sequence)}.response.json";

    /// <summary>An answer that is only text.</summary>
    public static string TextResponseName(int sequence) => $"{Number(sequence)}.response.txt";

    public static string Number(int sequence) => sequence.ToString("0000", CultureInfo.InvariantCulture);

    /// <summary>
    /// Which caller sent the request, read off its shape: a chat turn offers tools, an extraction
    /// (log interpretation, memory consolidation) asks for a schema, and a narrative is neither.
    /// </summary>
    public static string KindOf(JsonObject request) =>
        request["output_config"]?["format"] is not null ? "extract"
        : request["tools"] is JsonArray { Count: > 0 } ? "chat"
        : "prose";

    public static int EstimatedTokens(JsonNode? node) =>
        node is null ? 0 : (node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node.ToJsonString()).Length / 4;

    /// <summary>The request laid out for whoever is answering it, with its size by section.</summary>
    public static string Readable(JsonObject request, int sequence, string kind)
    {
        var messages = request["messages"] as JsonArray ?? [];
        var tools = request["tools"] as JsonArray ?? [];
        var notes = messages.Where(m => (string?)m?["role"] == "system").ToList();

        var text = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"# Relay request {Number(sequence)} ({kind})")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture,
                $"model {request["model"]} · max_tokens {request["max_tokens"]} · {messages.Count} messages · {tools.Count} tools")
            .AppendLine(CultureInfo.InvariantCulture,
                $"Estimated input tokens: tools {EstimatedTokens(tools)} · system {EstimatedTokens(request["system"])} · "
                + $"messages {EstimatedTokens(messages)} (system notes {notes.Sum(EstimatedTokens)}) · "
                + $"total {EstimatedTokens(tools) + EstimatedTokens(request["system"]) + EstimatedTokens(messages)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Cache breakpoints: {string.Join(", ", Breakpoints(request).DefaultIfEmpty("none"))}")
            .AppendLine()
            .AppendLine("## System")
            .AppendLine()
            .AppendLine(Flatten(request["system"]))
            .AppendLine()
            .AppendLine("## Messages");

        for (var index = 0; index < messages.Count; index++)
        {
            text.AppendLine()
                .AppendLine(CultureInfo.InvariantCulture, $"### [{index}] {messages[index]?["role"]}")
                .AppendLine()
                .AppendLine(Flatten(messages[index]?["content"]));
        }

        if (tools.Count > 0)
        {
            text.AppendLine().AppendLine("## Tools").AppendLine();
            foreach (var tool in tools)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {tool?["name"]}");
            }
        }

        if (request["output_config"]?["format"]?["schema"] is { } schema)
        {
            text.AppendLine().AppendLine("## Answer schema").AppendLine().AppendLine(schema.ToJsonString());
        }

        return text
            .AppendLine()
            .AppendLine("## Answer")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture,
                $"Write {TextResponseName(sequence)} with the answer's text, or {ResponseName(sequence)} with a content array:")
            .AppendLine("""{"content":[{"type":"text","text":"..."}]} or {"content":[{"type":"tool_use","name":"GetTrainingScore","input":{...}}]}""")
            .ToString();
    }

    /// <summary>A plain-text answer as the one text block it stands for.</summary>
    public static JsonObject FromText(string text) =>
        new() { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text.TrimEnd() }) };

    /// <summary>
    /// A hand-written answer made into a complete Messages API response: every field the person
    /// would otherwise have to copy out by hand (id, role, model, stop reason, usage, tool-use ids)
    /// filled in from the request, and anything they did write left as written.
    /// </summary>
    public static JsonObject Completed(JsonObject answer, JsonObject request, int sequence)
    {
        var response = (JsonObject)answer.DeepClone();
        var content = response["content"] as JsonArray ?? [];
        response["content"] = content;

        for (var index = 0; index < content.Count; index++)
        {
            if (content[index] is JsonObject { } block && (string?)block["type"] == "tool_use" && block["id"] is null)
            {
                block["id"] = $"toolu_relay_{Number(sequence)}_{index}";
            }
        }

        var usesTools = content.Any(block => (string?)block?["type"] == "tool_use");
        response["id"] ??= $"msg_relay_{Number(sequence)}";
        response["type"] ??= "message";
        response["role"] ??= "assistant";
        response["model"] ??= request["model"]?.DeepClone();
        response["stop_reason"] ??= usesTools ? "tool_use" : "end_turn";
        response["stop_sequence"] ??= null;
        response["usage"] ??= new JsonObject
        {
            ["input_tokens"] = EstimatedTokens(request["tools"]) + EstimatedTokens(request["system"]) + EstimatedTokens(request["messages"]),
            ["output_tokens"] = EstimatedTokens(content),
            ["cache_creation_input_tokens"] = 0,
            ["cache_read_input_tokens"] = 0,
        };
        return response;
    }

    /// <summary>Where the request asks for its prompt to be cached, so a reader can check the placement.</summary>
    private static IEnumerable<string> Breakpoints(JsonObject request)
    {
        if (request["cache_control"] is JsonObject top) yield return $"top-level ({Ttl(top)})";

        if (request["messages"] is not JsonArray messages) yield break;
        for (var m = 0; m < messages.Count; m++)
        {
            if (messages[m]?["content"] is not JsonArray blocks) continue;
            for (var b = 0; b < blocks.Count; b++)
            {
                if (blocks[b]?["cache_control"] is JsonObject cache) yield return $"messages[{m}].content[{b}] ({Ttl(cache)})";
            }
        }
    }

    private static string Ttl(JsonObject cache) => (string?)cache["ttl"] ?? "5m";

    /// <summary>Content as text: strings as they are, and each block on its own lines by type.</summary>
    private static string Flatten(JsonNode? content) => content switch
    {
        null => string.Empty,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonArray blocks => string.Join("\n\n", blocks.Select(Block)),
        _ => content.ToJsonString(),
    };

    private static string Block(JsonNode? block) => (string?)block?["type"] switch
    {
        "text" => (string?)block!["text"] ?? string.Empty,
        "tool_use" => $"[tool_use {block!["name"]} id={block["id"]}] {block["input"]?.ToJsonString()}",
        "tool_result" => $"[tool_result id={block!["tool_use_id"]}{((bool?)block["is_error"] == true ? " ERROR" : "")}]\n{Flatten(block["content"])}",
        "thinking" => "[thinking]",
        _ => block?.ToJsonString() ?? string.Empty,
    };
}
