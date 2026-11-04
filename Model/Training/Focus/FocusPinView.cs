namespace lionheart.Model.Training.Focus;

/// <summary>
/// A focus pin on the wire.
/// </summary>
/// <remarks>
/// The role, the target and the status are the domain's own records, which already carry their
/// discriminators, so only the envelope is restated — the way a session view sends its sets. The
/// horizon is flattened into the envelope as the requests state it. <c>UserId</c> is left out:
/// the caller is the owner on every route.
/// </remarks>
public sealed record FocusPinView(
    Guid FocusPinId,
    Guid MovementDataId,
    FocusRole Role,
    DateOnly Starts,
    DateOnly Ends,
    int SessionsPerWeek,
    FocusTarget Target,
    FocusStatus Status,
    DateTime PinnedAt)
{
    public static FocusPinView Of(FocusPin pin) => new(
        pin.FocusPinId,
        pin.MovementDataId,
        pin.Role,
        pin.Terms.Horizon.Starts,
        pin.Terms.Horizon.Ends,
        pin.Terms.SessionsPerWeek,
        pin.Terms.Target,
        pin.Status,
        pin.PinnedAt);
}
