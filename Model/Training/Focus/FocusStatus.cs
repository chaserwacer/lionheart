using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Focus;

/// <summary>
/// Whether the athlete still means a focus pin, or has let it go.
/// </summary>
/// <remarks>
/// The only lifecycle stored on a pin. "Not begun", "under way", "over" and "target met" are all
/// readings the client makes against today and the training log, the way a planned session is
/// read as missed rather than stored as one: storing them would need a job to move every pin at
/// midnight, and a stored reading can disagree with the log it was read off.
/// <para>
/// Unpinning is dated by the athlete rather than stamped, because a focus is often let go some
/// days before anyone says so.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Pinned), "pinned")]
[JsonDerivedType(typeof(Unpinned), "unpinned")]
public abstract record FocusStatus
{
    private protected FocusStatus() { }

    public sealed record Pinned : FocusStatus;

    /// <summary>Let go on <paramref name="On"/>. Kept — the pin is history now, not a mistake.</summary>
    public sealed record Unpinned(DateOnly On) : FocusStatus;
}
