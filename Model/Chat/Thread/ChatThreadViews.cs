using System.ComponentModel.DataAnnotations;

namespace lionheart.Model.Chat.Thread;

/// <summary>
/// A stretch of the thread, oldest session first. The client draws it as one transcript, with the
/// sessions' openings as the only seams.
/// </summary>
/// <param name="HasEarlier">Whether sessions older than the first one here exist, to be read with <see cref="ThreadPage.Before"/>.</param>
public sealed record ChatThreadPage(IReadOnlyList<ThreadSessionView> Sessions, bool HasEarlier);

/// <summary>One session's turns — the athlete's and the model's, in order. Tool results are not turns.</summary>
public sealed record ThreadSessionView(Guid SessionId, DateTime OpenedAt, IReadOnlyList<LHChatMessageDTO> Messages)
{
    public static ThreadSessionView Of(LHChatConversation session) =>
        new(
            session.ChatConversationID,
            session.CreatedAt,
            [.. session.GetUserModelMessagesInChronologicalOrder().Select(message => new LHChatMessageDTO(message))]);
}

/// <summary>Which stretch of the thread to read.</summary>
public abstract record ThreadPage
{
    private protected ThreadPage() { }

    /// <summary>The newest sessions.</summary>
    public sealed record Latest : ThreadPage;

    /// <summary>The sessions that opened before <paramref name="OpenedBefore"/> — the next stretch back.</summary>
    public sealed record Before(DateTime OpenedBefore) : ThreadPage;
}

/// <summary>
/// The thread read's query: nothing for the newest sessions, or <c>before</c> — the opening instant
/// of the oldest session already shown — for the stretch before it.
/// </summary>
/// <remarks>An offset rather than a bare date-time, so an instant sent in UTC is not bound as local time.</remarks>
public sealed record ChatThreadQuery
{
    public DateTimeOffset? Before { get; init; }

    public ThreadPage ToPage() =>
        Before is { } before ? new ThreadPage.Before(before.UtcDateTime) : new ThreadPage.Latest();
}

/// <summary>One message to the thread. Which session it lands in is the server's to decide.</summary>
public sealed record SendThreadMessageRequest
{
    [Required]
    public required string Content { get; init; }
}
