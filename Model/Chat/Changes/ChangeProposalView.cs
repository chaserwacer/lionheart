namespace lionheart.Model.Chat.Changes;

/// <summary>
/// A proposal on the wire: what the card shows and where it stands. The change itself stays on the
/// server — the client answers by id, so it can confirm only what it was shown.
/// </summary>
public sealed record ChangeProposalView(
    Guid ChangeProposalId,
    DateTime ProposedAt,
    ChangeArea Area,
    string Title,
    IReadOnlyList<string> Lines,
    ProposalAnswer Answer)
{
    public static ChangeProposalView Of(ChangeProposal proposal) => new(
        proposal.ChangeProposalId,
        proposal.ProposedAt,
        proposal.Change.Area,
        proposal.Summary.Title,
        proposal.Summary.Lines,
        proposal.Answer);
}
