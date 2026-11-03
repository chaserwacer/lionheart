namespace lionheart.Model.Chat.Thread;

/// <summary>
/// The rules that cut the athlete's one continuing thread into the sessions a model actually sees.
/// </summary>
public static class ChatSessionPolicy
{
    /// <summary>
    /// How long the thread has to sit idle before the next message starts a new session.
    /// </summary>
    /// <remarks>
    /// Three hours, because of how the athlete uses it. A question before training and the report
    /// after it belong to one session, and a workout is 45 to 90 minutes plus getting there and
    /// back; coming back five hours later is, in his words, a new session. Three hours sits between
    /// the two. It is longer than the prompt cache's hour, so a return between one and three hours
    /// rereads the session at full price once and is cached again from there — cents, against
    /// losing the thread of a training day.
    /// </remarks>
    public static readonly TimeSpan IdleGap = TimeSpan.FromHours(3);

    /// <summary>
    /// How large a session's transcript may grow, in estimated tokens, before the next message
    /// starts a new one even without an idle gap.
    /// </summary>
    /// <remarks>
    /// A thread he keeps coming back to every couple of hours would otherwise never end, and the
    /// whole point of sessions is that the model reads a focused one rather than a day of
    /// accumulated tool output. Well inside the completion service's input budget, so a session is
    /// always sent whole and its prefix stays cacheable.
    /// </remarks>
    public const int MaxTranscriptTokens = 60_000;

    /// <summary>How many times a session's consolidation is tried before it is left failed.</summary>
    public const int ConsolidationAttempts = 3;

    /// <summary>How long a failed consolidation waits before it is tried again.</summary>
    public static readonly TimeSpan ConsolidationRetryAfter = TimeSpan.FromMinutes(30);
}
