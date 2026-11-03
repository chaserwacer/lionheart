using System.Diagnostics;
using lionheart.Functional;

namespace lionheart.Model.Training.Focus;

/// <summary>
/// A movement the athlete has pinned to emphasise over a horizon, at a frequency, perhaps toward
/// a target.
/// </summary>
/// <remarks>
/// A sealed class rather than a record for the reason <see cref="PersonalRecords.PersonalRecord"/>
/// gives: a pin has identity and a lifecycle, so value equality would break change tracking as it
/// moves. Everything it holds is an immutable record, and only the terms and the status move,
/// through the transitions below.
/// <para>
/// The movement is fixed for the pin's life. Pinning a different movement is a different focus,
/// and the history of the old one is the point of keeping unpinned pins at all.
/// </para>
/// </remarks>
public sealed class FocusPin
{
    private FocusPin(
        Guid focusPinId, Guid userId, Guid movementDataId, FocusTerms terms, FocusStatus status, DateTime pinnedAt)
    {
        FocusPinId = focusPinId;
        UserId = userId;
        MovementDataId = movementDataId;
        Terms = terms;
        Status = status;
        PinnedAt = pinnedAt;
    }

    public Guid FocusPinId { get; private init; }

    public Guid UserId { get; private init; }

    public Guid MovementDataId { get; private init; }

    public FocusTerms Terms { get; private set; }

    public FocusStatus Status { get; private set; }

    /// <summary>When the pin was made — the write instant, unlike the horizon, which is the athlete's.</summary>
    public DateTime PinnedAt { get; private init; }

    public bool IsPinned => Status is FocusStatus.Pinned;

    public static FocusPin Pin(
        Guid focusPinId, Guid userId, Guid movementDataId, FocusTerms terms, DateTime pinnedAt) =>
        new(focusPinId, userId, movementDataId, terms, new FocusStatus.Pinned(), pinnedAt);

    /// <summary>
    /// Replaces the terms whole, while the pin is still meant.
    /// </summary>
    /// <remarks>
    /// An unpinned pin is history, and rewriting its horizon or target would change what the
    /// athlete was actually aiming at back then. A conflict rather than a malformed ask: the same
    /// revision was legitimate while the pin stood.
    /// </remarks>
    public Result<FocusPin, Problem> Revise(FocusTerms terms) =>
        WhilePinned("revised").Map(pin =>
        {
            pin.Terms = terms;
            return pin;
        });

    /// <summary>Lets the pin go on the day the athlete names. Once only.</summary>
    public Result<FocusPin, Problem> Unpin(DateOnly on) =>
        WhilePinned("unpinned").Map(pin =>
        {
            pin.Status = new FocusStatus.Unpinned(on);
            return pin;
        });

    private Result<FocusPin, Problem> WhilePinned(string action) =>
        Status switch
        {
            FocusStatus.Pinned => Result<FocusPin, Problem>.Success(this),
            FocusStatus.Unpinned unpinned => Result<FocusPin, Problem>.Failure(new Problem.Conflict(
                $"This focus was unpinned on {unpinned.On:yyyy-MM-dd} and cannot be {action}.")),
            _ => throw new UnreachableException(),
        };
}
