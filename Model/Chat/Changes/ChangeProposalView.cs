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
    public static ChangeProposalView Of(ChangeProposal proposal) =>
        Of(proposal.ChangeProposalId, proposal.ProposedAt, proposal.Area, proposal.Summary, proposal.Answer);

    public static ChangeProposalView Of(
        Guid changeProposalId, DateTime proposedAt, ChangeArea area, ProposalSummary summary, ProposalAnswer answer) =>
        new(changeProposalId, proposedAt, area, summary.Title, summary.Lines, answer);

    /// <summary>What the card says, as one passage.</summary>
    public string Spoken => new ProposalSummary(Title, Lines).Spoken;
}
