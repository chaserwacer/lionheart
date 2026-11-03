using System.Globalization;
using System.Text;
using lionheart.Model.Chat.Memory;
using lionheart.Services.Chat.Memory;

namespace lionheart.Services.Chat.Thread;

/// <summary>
/// The system prompt a thread session opens with, and keeps unchanged until it ends.
/// </summary>
/// <remarks>
/// Three parts, in order of how long they hold. The card's prompt comes first, so its voice and
/// stable profile lead the prefix as they do in a standalone conversation. Memory follows it: what
/// the athlete told the chat, kept apart from the card because the card is what his data shows and
/// memory is what he said, and the model should weigh each as what it is. The session's own date
/// comes last.
/// </remarks>
public static class ThreadSessionPrompt
{
    private const string MemoryPolicy = """
        This is one continuing thread with the athlete. Earlier sessions are not loaded; what he told you in them was distilled into the memories below. The card above is his recorded data; these memories are what he said. Where one bears on the other, read them together, and where a memory and a recorded figure disagree, the figure is the measurement and the memory is what he believed when he said it.
        - Use memories the way an analyst uses notes from earlier meetings: apply them when they bear on the question, without reciting them or announcing that you remember.
        - What he says in this session outranks a memory that it contradicts.
        - The memories were current when this session opened. When a question turns on something he may have told you before that is not listed, call SearchMemory before saying you do not know.
        """;

    public static string Render(string cardPrompt, RecalledMemories recall, DateTime openedAt, TimeZoneInfo zone)
    {
        var text = new StringBuilder(cardPrompt.TrimEnd())
            .AppendLine()
            .AppendLine()
            .AppendLine("=== MEMORY: WHAT THE ATHLETE HAS TOLD YOU ===")
            .AppendLine(MemoryPolicy.TrimEnd());

        if (recall.IsEmpty)
        {
            text.AppendLine().AppendLine("Nothing is remembered yet.");
        }
        else
        {
            Section(text, "Standing (goals, current injuries, preferences):", recall.Core, zone);
            Section(text, "Recalled for this session:", recall.Relevant, zone);
        }

        var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(openedAt, DateTimeKind.Utc)), zone);
        return text
            .AppendLine()
            .AppendLine("--- SESSION ---")
            .Append(CultureInfo.InvariantCulture, $"Opened {local:dddd d MMMM yyyy, HH:mm} ({local:zzz}).")
            .ToString();
    }

    private static void Section(StringBuilder text, string heading, IReadOnlyList<ChatMemory> memories, TimeZoneInfo zone)
    {
        if (memories.Count == 0) return;

        text.AppendLine().AppendLine(heading);
        foreach (var memory in memories)
        {
            var recorded = TimeZoneInfo.ConvertTime(
                new DateTimeOffset(DateTime.SpecifyKind(memory.RecordedAt, DateTimeKind.Utc)), zone);
            text.AppendLine(CultureInfo.InvariantCulture, $"- [{memory.Kind.Word}, {recorded:yyyy-MM-dd}] {memory.Text}");
        }
    }
}
