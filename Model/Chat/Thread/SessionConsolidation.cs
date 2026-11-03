using System.Text.Json.Serialization;

namespace lionheart.Model.Chat.Thread;

/// <summary>
/// Where a thread session stands with respect to memory: not yet distilled, distilled, or tried
/// and failed.
/// </summary>
/// <remarks>
/// A session is distilled once, after it ends. Failure is its own case rather than a pending one
/// with a counter beside it, because a failed session is retried on a back-off, and stops being
/// retried after <see cref="ChatSessionPolicy.ConsolidationAttempts"/>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Pending), "pending")]
[JsonDerivedType(typeof(Consolidated), "consolidated")]
[JsonDerivedType(typeof(Failed), "failed")]
public abstract record SessionConsolidation
{
    private protected SessionConsolidation() { }

    public sealed record Pending : SessionConsolidation;

    /// <summary>
    /// Distilled at <paramref name="At"/>. The counts say what it changed, and all three may be
    /// zero: a session that said nothing worth keeping is still consolidated.
    /// </summary>
    public sealed record Consolidated(DateTime At, int Remembered, int Revised, int Retired) : SessionConsolidation;

    /// <summary>The last attempt failed for <paramref name="Reason"/>, after <paramref name="Attempts"/> tries.</summary>
    public sealed record Failed(DateTime LastAttemptAt, int Attempts, string Reason) : SessionConsolidation;
}
