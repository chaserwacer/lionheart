using System.Globalization;
using System.Text;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Memory;

namespace lionheart.Services.Chat.Thread;

/// <summary>
/// What the consolidating model is asked, and the handles that map its answer back onto real
/// messages and memories.
/// </summary>
/// <remarks>
/// Messages and memories are given short handles (<c>m4</c>, <c>k2</c>) rather than their ids: a
/// model copying thirty-six-character GUIDs back gets one wrong often enough to matter, and a wrong
/// short handle simply fails to resolve.
/// </remarks>
public sealed record ConsolidationPrompt(
    string SystemPrompt,
    string UserText,
    IReadOnlyDictionary<string, Guid> Messages,
    IReadOnlyDictionary<string, ChatMemory> Memories)
{
    public const string SchemaName = "memory_consolidation";

    /// <summary>
    /// A flat change rather than one shape per action: every field is required and closed, which is
    /// the subset of JSON Schema both providers' structured output enforces strictly.
    /// </summary>
    public const string Schema = """
        {
          "type": "object",
          "properties": {
            "changes": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "action": { "type": "string", "enum": ["remember", "revise", "retire"] },
                  "memoryId": { "type": "string" },
                  "kind": { "type": "string", "enum": ["goal", "injury", "preference", "commitment", "decision", "fact"] },
                  "text": { "type": "string" },
                  "sourceMessageIds": { "type": "array", "items": { "type": "string" } }
                },
                "required": ["action", "memoryId", "kind", "text", "sourceMessageIds"],
                "additionalProperties": false
              }
            }
          },
          "required": ["changes"],
          "additionalProperties": false
        }
        """;

    /// <summary>
    /// How much of one of the model's own answers is shown. The athlete's words are what get
    /// remembered; an answer is there only so "yes, do that" can be read against what "that" was.
    /// </summary>
    private const int AnswerExcerptLength = 1200;

    private const string Instructions = """
        You keep the long-term memory of Tempo, a coaching chat with one athlete. A chat session has ended. Decide what the athlete told the chat that is worth remembering in later sessions, and how it changes what is already remembered.

        Remember only what the athlete stated, decided or agreed to: goals; injuries and niggles; standing preferences about how he trains or how he wants answers; commitments he made; decisions about his training, with the reason where he gave one; and durable facts about him such as schedule, equipment, circumstances and history. Do not remember:
        - the coach's own findings, figures or advice, unless the athlete adopted them as a decision or a commitment;
        - training data the app records anyway: sessions, sets, records, ring scores, logged injuries;
        - one-off questions, small talk, or anything true only for the moment;
        - a length or format he asks for in one message, unless he says he wants it from now on.

        Write each memory as one standalone sentence, in the third person, that a later reader understands without the session: name the body part, lift or goal, and date anything that will change ("As of 2026-11-03, ..."). Keep it under 300 characters.

        Kept memories have handles k1, k2, and so on; the session's messages have handles m1, m2, and so on. Each change takes one action:
        - remember: something new. memoryId is "".
        - revise: the session updates, corrects or adds to a kept memory. memoryId is its handle, and text is the whole replacement: keep what still holds and date what changed. An injury that has healed is revised into a fact recording when it healed, rather than kept as an injury.
        - retire: a kept memory is no longer true and nothing replaces it, such as a commitment kept or abandoned or a plan dropped. memoryId is its handle, and text says why.
        Never remember what a kept memory already says. Never make two changes to one kept memory. kind is the memory's kind after the change; for retire, repeat the kept memory's kind. sourceMessageIds lists the handles of the messages the change rests on, the athlete's own first.

        If nothing in the session is worth remembering, return no changes.
        """;

    /// <param name="transcript">The athlete's and the model's turns, oldest first. Tool results are not given: they are the app's data, not anything the athlete said.</param>
    /// <param name="kept">The current memories the session may revise or retire.</param>
    /// <param name="zone">The athlete's time zone, so the session's times read as his day.</param>
    public static ConsolidationPrompt For(
        DateTime sessionOpenedAt, IReadOnlyList<LHChatMessage> transcript, IReadOnlyList<ChatMemory> kept, TimeZoneInfo zone)
    {
        var memories = kept
            .Select((memory, index) => (Handle: $"k{index + 1}", Memory: memory))
            .ToDictionary(pair => pair.Handle, pair => pair.Memory);
        var messages = transcript
            .Select((message, index) => (Handle: $"m{index + 1}", Message: message))
            .ToList();

        var text = new StringBuilder()
            .AppendLine($"Session opened {Local(sessionOpenedAt, zone, "dddd d MMMM yyyy, HH:mm (zzz)")}.")
            .AppendLine()
            .AppendLine("Kept memories:");

        if (memories.Count == 0) text.AppendLine("(none)");
        foreach (var (handle, memory) in memories)
        {
            text.AppendLine($"{handle} [{memory.Kind.Word}, recorded {Local(memory.RecordedAt, zone, "yyyy-MM-dd")}] {memory.Text}");
        }

        text.AppendLine().AppendLine("Session:");
        foreach (var (handle, message) in messages)
        {
            text.AppendLine($"{handle} {Speaker(message)} ({Local(message.CreationTime, zone, "HH:mm")}): {Said(message)}");
        }

        return new ConsolidationPrompt(
            Instructions,
            text.ToString(),
            messages.ToDictionary(pair => pair.Handle, pair => pair.Message.ChatMessageItemID),
            memories);
    }

    private static string Speaker(LHChatMessage message) => message is LHUserChatMessage ? "athlete" : "lionheart";

    private static string Said(LHChatMessage message)
    {
        var content = (message.Content ?? string.Empty).Trim();
        return message is LHUserChatMessage || content.Length <= AnswerExcerptLength
            ? content
            : content[..AnswerExcerptLength] + "…";
    }

    private static string Local(DateTime utc, TimeZoneInfo zone, string format) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), zone)
            .ToString(format, CultureInfo.InvariantCulture);
}
