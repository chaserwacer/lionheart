using System.Diagnostics;
using lionheart.Functional;

namespace lionheart.Model.Training.Focus;

/// <summary>
/// A movement the athlete has pinned over a horizon, at a frequency, perhaps toward a target: the
/// priority lift of push, pull or legs for a block, or a movement emphasised on its own.
/// </summary>
/// <remarks>
/// A sealed class rather than a record for the reason <see cref="PersonalRecords.PersonalRecord"/>
/// gives: a pin has identity and a lifecycle, so value equality would break change tracking as it
/// moves. Everything it holds is an immutable record, and only the terms and the status move,
/// through the transitions below.
/// <para>
/// The movement is fixed for the pin's life. Pinning a different movement is a different focus,
/// and the history of the old one is the point of keeping unpinned pins at all. Only its id moves,
/// when the library merges two rows that were one movement (<see cref="FollowMerge"/>).
/// </para>
/// </remarks>
public sealed class FocusPin
{
    private FocusPin(
        Guid focusPinId, Guid userId, Guid movementDataId, FocusRole role, FocusTerms terms, FocusStatus status,
        DateTime pinnedAt)
    {
        FocusPinId = focusPinId;
        UserId = userId;
        MovementDataId = movementDataId;
        Role = role;
        Terms = terms;
        Status = status;
        PinnedAt = pinnedAt;
    }

    public Guid FocusPinId { get; private init; }

    public Guid UserId { get; private init; }

    public Guid MovementDataId { get; private set; }

    public FocusRole Role { get; private init; }

    public FocusTerms Terms { get; private set; }

    public FocusStatus Status { get; private set; }

    /// <summary>When the pin was made — the write instant, unlike the horizon, which is the athlete's.</summary>
    public DateTime PinnedAt { get; private init; }

    public bool IsPinned => Status is FocusStatus.Pinned;

    public static FocusPin Pin(
        Guid focusPinId, Guid userId, Guid movementDataId, FocusRole role, FocusTerms terms, DateTime pinnedAt) =>
        new(focusPinId, userId, movementDataId, role, terms, new FocusStatus.Pinned(), pinnedAt);

    /// <summary>
    /// Lets this pin go because <paramref name="successor"/> takes its family over, on the day before
    /// the successor's horizon starts — or on this pin's own first day, if the successor starts
    /// before it, so a pin is never unpinned before it began.
    /// </summary>
    public Result<FocusPin, Problem> SupersededBy(FocusPin successor)
    {
        var dayBefore = successor.Terms.Horizon.Starts.AddDays(-1);
        return Unpin(dayBefore < Terms.Horizon.Starts ? Terms.Horizon.Starts : dayBefore);
    }

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

    /// <summary>
    /// Follows the pin's movement into the one it was merged with: the same movement, kept under
    /// the other's id, so the pin is still on what it was always on.
    /// </summary>
    public void FollowMerge(Guid survivingMovementDataId) => MovementDataId = survivingMovementDataId;

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
