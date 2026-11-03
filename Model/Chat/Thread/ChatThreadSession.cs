using System.Diagnostics;
using lionheart.Functional;

namespace lionheart.Model.Chat.Thread;

/// <summary>
/// One session of the athlete's continuing chat thread: the run of messages the model sees as a
/// conversation, and what has become of it in memory.
/// </summary>
/// <remarks>
/// A session is stored as an ordinary chat conversation — its system prompt frozen when it opened,
/// its turns in the conversation message tables — and this is the row that makes that conversation
/// part of the thread rather than a standalone one. Freezing the prompt for the session's life is
/// what keeps its prefix byte-stable, and so cacheable, from the first message to the last.
/// <para>
/// A sealed class for the reason <see cref="Training.Focus.FocusPin"/> gives: it has identity and a
/// lifecycle, and only the consolidation moves.
/// </para>
/// </remarks>
public sealed class ChatThreadSession
{
    private ChatThreadSession(Guid chatConversationId, Guid userId, DateTime openedAt, SessionConsolidation consolidation)
    {
        ChatConversationId = chatConversationId;
        UserId = userId;
        OpenedAt = openedAt;
        Consolidation = consolidation;
    }

    /// <summary>The conversation that holds this session's prompt and turns.</summary>
    public Guid ChatConversationId { get; private init; }

    public Guid UserId { get; private init; }

    public DateTime OpenedAt { get; private init; }

    public SessionConsolidation Consolidation { get; private set; }

    public bool IsConsolidated => Consolidation is SessionConsolidation.Consolidated;

    public static ChatThreadSession Open(Guid chatConversationId, Guid userId, DateTime openedAt) =>
        new(chatConversationId, userId, openedAt, new SessionConsolidation.Pending());

    /// <summary>
    /// Whether this session, already ended, should be distilled now: never tried, or failed and
    /// past its back-off with attempts left.
    /// </summary>
    public bool IsDueForConsolidation(DateTime now) =>
        Consolidation switch
        {
            SessionConsolidation.Pending => true,
            SessionConsolidation.Consolidated => false,
            SessionConsolidation.Failed failed =>
                failed.Attempts < ChatSessionPolicy.ConsolidationAttempts
                && now - failed.LastAttemptAt >= ChatSessionPolicy.ConsolidationRetryAfter,
            _ => throw new UnreachableException(),
        };

    /// <summary>
    /// Records what distilling the session changed. Once only: a second consolidation would
    /// remember the same things twice.
    /// </summary>
    public Result<ChatThreadSession, Problem> Consolidated(DateTime at, int remembered, int revised, int retired) =>
        Consolidation switch
        {
            SessionConsolidation.Consolidated done => Result<ChatThreadSession, Problem>.Failure(new Problem.Conflict(
                $"This session was already consolidated at {done.At:u}.")),
            SessionConsolidation.Pending or SessionConsolidation.Failed => Settle(
                new SessionConsolidation.Consolidated(at, remembered, revised, retired)),
            _ => throw new UnreachableException(),
        };

    /// <summary>
    /// Records a failed attempt, counting it toward the session's allowance. A session already
    /// consolidated keeps that: a late failure from a racing attempt does not undo a success.
    /// </summary>
    public ChatThreadSession ConsolidationFailed(DateTime at, string reason)
    {
        Consolidation = Consolidation switch
        {
            SessionConsolidation.Consolidated done => done,
            SessionConsolidation.Pending => new SessionConsolidation.Failed(at, 1, reason),
            SessionConsolidation.Failed failed => new SessionConsolidation.Failed(at, failed.Attempts + 1, reason),
            _ => throw new UnreachableException(),
        };
        return this;
    }

    private Result<ChatThreadSession, Problem> Settle(SessionConsolidation consolidated)
    {
        Consolidation = consolidated;
        return Result<ChatThreadSession, Problem>.Success(this);
    }
}
