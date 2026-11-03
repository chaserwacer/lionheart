using System.Diagnostics;
using lionheart.Functional;

namespace lionheart.Model.Chat.Memory;

/// <summary>
/// One version of something the athlete told the chat and Lionheart keeps: a goal, an injury, a
/// preference, a commitment, a decision or a fact, dated and traced to where it came from.
/// </summary>
/// <remarks>
/// Memory is what he said; the athlete context card is what his data shows. The two reach the model
/// side by side and are never merged, because a stated goal and a measured trend are different
/// kinds of evidence.
/// <para>
/// Versions of one memory share a <see cref="LineageId"/>. Only the newest is
/// <see cref="MemoryStanding.Current"/>; a revision supersedes it rather than rewriting it, so a
/// memory that changed carries its own history. A sealed class with private setters for the reason
/// <see cref="Training.Focus.FocusPin"/> gives.
/// </para>
/// </remarks>
public sealed class ChatMemory
{
    /// <summary>
    /// Long enough for a dated sentence with its reason, short enough that thirty of them are a
    /// paragraph of a system prompt rather than a page.
    /// </summary>
    public const int MaxTextLength = 400;

    private ChatMemory(
        Guid memoryId, Guid userId, Guid lineageId, MemoryKind kind, string text, DateTime recordedAt,
        MemoryOrigin origin, MemoryStanding standing, float[] vector)
    {
        MemoryId = memoryId;
        UserId = userId;
        LineageId = lineageId;
        Kind = kind;
        Text = text;
        RecordedAt = recordedAt;
        Origin = origin;
        Standing = standing;
        Vector = vector;
    }

    public Guid MemoryId { get; private init; }

    public Guid UserId { get; private init; }

    /// <summary>The first version's id, shared by every version after it.</summary>
    public Guid LineageId { get; private init; }

    public MemoryKind Kind { get; private init; }

    public string Text { get; private init; }

    /// <summary>When this version was written.</summary>
    public DateTime RecordedAt { get; private init; }

    public MemoryOrigin Origin { get; private init; }

    public MemoryStanding Standing { get; private set; }

    /// <summary>
    /// The text's embedding, or empty when the embedding server could not be reached as it was
    /// written. An unembedded memory is still recalled — by recency, and by words for a search —
    /// and is embedded the next time memory is written.
    /// </summary>
    public float[] Vector { get; private set; }

    public bool IsCurrent => Standing is MemoryStanding.Current;

    public bool IsEmbedded => Vector.Length > 0;

    public static Result<ChatMemory, Problem> Record(
        Guid memoryId, Guid userId, MemoryKind kind, string text, MemoryOrigin origin, DateTime recordedAt) =>
        Statement(kind, text).Map(statement => new ChatMemory(
            memoryId, userId, memoryId, kind, statement, recordedAt, origin, new MemoryStanding.Current(), []));

    /// <summary>
    /// The version that replaces this one. Only the current version can be revised: revising one
    /// already superseded would fork the memory into two that both claim to hold.
    /// </summary>
    public Result<ChatMemory, Problem> Revise(
        Guid successorId, MemoryKind kind, string text, MemoryOrigin origin, DateTime at) =>
        WhileCurrent("revised")
            .Bind(_ => Statement(kind, text))
            .Map(statement =>
            {
                Standing = new MemoryStanding.Superseded(successorId, at);
                return new ChatMemory(
                    successorId, UserId, LineageId, kind, statement, at, origin, new MemoryStanding.Current(), []);
            });

    /// <summary>Ends the memory with nothing in its place.</summary>
    public Result<ChatMemory, Problem> Retire(DateTime at, string reason) =>
        WhileCurrent("retired").Map(memory =>
        {
            memory.Standing = new MemoryStanding.Retired(at, reason.Trim());
            return memory;
        });

    public ChatMemory Embedded(float[] vector)
    {
        Vector = vector;
        return this;
    }

    private Result<ChatMemory, Problem> WhileCurrent(string action) =>
        Standing switch
        {
            MemoryStanding.Current => Result<ChatMemory, Problem>.Success(this),
            MemoryStanding.Superseded superseded => Result<ChatMemory, Problem>.Failure(new Problem.Conflict(
                $"This memory was replaced on {superseded.At:yyyy-MM-dd} and cannot be {action}.")),
            MemoryStanding.Retired retired => Result<ChatMemory, Problem>.Failure(new Problem.Conflict(
                $"This memory was retired on {retired.At:yyyy-MM-dd} and cannot be {action}.")),
            _ => throw new UnreachableException(),
        };

    /// <summary>The text as it will be kept, or why it cannot be.</summary>
    private static Result<string, Problem> Statement(MemoryKind kind, string text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        return !Enum.IsDefined(kind)
            ? Result<string, Problem>.Failure(new Problem.Invalid(nameof(Kind), $"{(int)kind} is not a kind of memory."))
            : trimmed.Length == 0
                ? Result<string, Problem>.Failure(new Problem.Invalid(nameof(Text), "A memory needs something to say."))
                : trimmed.Length > MaxTextLength
                    ? Result<string, Problem>.Failure(new Problem.Invalid(
                        nameof(Text), $"A memory is at most {MaxTextLength} characters."))
                    : Result<string, Problem>.Success(trimmed);
    }
}
