namespace lionheart.Model.Chat.Thread;

/// <summary>The thread's newest session as a message arrives, with the conversation that holds its turns.</summary>
public sealed record SessionTip(ChatThreadSession Session, LHChatConversation Conversation)
{
    /// <summary>The last exchange that landed, or the opening when none has.</summary>
    public DateTime LastActivityAt => Conversation.LastUpdate;

    /// <summary>
    /// The session's size as the completion service measures it when it packs a prompt: the system
    /// prompt and every stored turn, tool results included, by their stored token counts. The same
    /// units as that budget, so a session that ends within the cap is one the service sends whole.
    /// </summary>
    public int TranscriptTokens =>
        Conversation.ChatSystemMessage.TokenCount
        + Conversation.GetAllNonSystemMessagesInChronologicalOrder().Sum(message => message.TokenCount);
}

/// <summary>Why a session ended.</summary>
public enum SessionEnding
{
    /// <summary>Nothing was said for <see cref="ChatSessionPolicy.IdleGap"/>.</summary>
    Idle,

    /// <summary>The transcript reached <see cref="ChatSessionPolicy.MaxTranscriptTokens"/>.</summary>
    Full
}

/// <summary>
/// Whether a message arriving now continues the thread's newest session or starts the next one.
/// </summary>
/// <remarks>
/// Decided when a message arrives rather than by a clock: a session has no end of its own, only
/// a next message that comes too late or into too full a transcript to belong to it.
/// </remarks>
public abstract record SessionBoundary
{
    private protected SessionBoundary() { }

    /// <summary>The thread has no session yet: this is the athlete's first message to it.</summary>
    public sealed record NoSessionYet : SessionBoundary;

    public sealed record Continues(SessionTip Tip) : SessionBoundary;

    /// <summary>The newest session is over; it is due to be distilled before the next one opens.</summary>
    public sealed record Ended(SessionTip Tip, SessionEnding Why) : SessionBoundary;

    public static SessionBoundary Decide(SessionTip? tip, DateTime now) =>
        tip switch
        {
            null => new NoSessionYet(),
            _ when now - tip.LastActivityAt >= ChatSessionPolicy.IdleGap => new Ended(tip, SessionEnding.Idle),
            _ when tip.TranscriptTokens >= ChatSessionPolicy.MaxTranscriptTokens => new Ended(tip, SessionEnding.Full),
            _ => new Continues(tip),
        };
}
