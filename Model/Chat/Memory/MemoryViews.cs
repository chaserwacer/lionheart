using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.Chat.Memory;

/// <summary>Who wrote the current version of a memory.</summary>
public enum MemorySource
{
    /// <summary>Distilled from what he said in a thread session.</summary>
    Chat,

    /// <summary>Written by the athlete, correcting what the chat had kept.</summary>
    Athlete
}

/// <summary>
/// A memory as the athlete reads it: what it says, where it came from in his own words, and what
/// it said before.
/// </summary>
/// <param name="Quotes">The messages it was drawn from, oldest first. Empty for one he wrote himself.</param>
/// <param name="Earlier">The versions it replaced, newest first.</param>
public sealed record MemoryView(
    Guid MemoryId,
    MemoryKind Kind,
    string Text,
    DateTime RecordedAt,
    MemorySource Source,
    IReadOnlyList<MemoryQuote> Quotes,
    IReadOnlyList<MemoryVersionView> Earlier);

/// <summary>A message a memory was drawn from, clipped to a line or two.</summary>
public sealed record MemoryQuote(Guid MessageId, bool ByAthlete, string Said, DateTime At);

/// <summary>A version a memory used to read, and when it stopped holding.</summary>
public sealed record MemoryVersionView(MemoryKind Kind, string Text, DateTime RecordedAt, DateTime ReplacedAt);

/// <summary>
/// The athlete's correction of a memory: what it should say instead. The memory it corrects is kept
/// as history.
/// </summary>
public sealed record CorrectMemoryRequest
{
    [Required]
    public required MemoryKind Kind { get; init; }

    [Required]
    [MaxLength(ChatMemory.MaxTextLength)]
    public required string Text { get; init; }
}

/// <summary>A memory as the chat model is handed it by a search: the kind, the date, the statement.</summary>
public sealed record RecalledMemory(string Kind, string RecordedOn, string Text);
