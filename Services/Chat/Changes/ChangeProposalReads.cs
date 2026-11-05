using lionheart.Data;
using lionheart.Model.Chat.Changes;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Changes;

/// <summary>The thread's reads of what was proposed in it.</summary>
internal static class ChangeProposalReads
{
    /// <summary>How long a card left unanswered in an earlier session is still worth telling the model about.</summary>
    public static readonly TimeSpan WaitingRemembered = TimeSpan.FromDays(14);

    extension(ModelContext context)
    {
        /// <summary>The proposals made in these sessions, by session, oldest first.</summary>
        public async Task<ILookup<Guid, ChangeProposalView>> ProposalViewsAsync(IReadOnlyCollection<Guid> conversationIds, CancellationToken ct) =>
            (await Cards(context.ChangeProposals.Where(proposal => conversationIds.Contains(proposal.ChatConversationId)))
                .ToListAsync(ct))
            .ToLookup(card => card.ChatConversationId, card => card.View);

        /// <summary>
        /// What the model hears about with a message: everything proposed in this session, and his
        /// cards from earlier sessions of the last fortnight that still wait for an answer.
        /// </summary>
        public async Task<IReadOnlyList<ChangeProposalView>> ProposalsToTellAsync(
            Guid userId, Guid conversationId, DateTime now, CancellationToken ct)
        {
            var since = now - WaitingRemembered;
            var recent = await Cards(context.ChangeProposals.Where(proposal =>
                    proposal.UserId == userId
                    && (proposal.ChatConversationId == conversationId || proposal.ProposedAt >= since)))
                .ToListAsync(ct);

            // The answer is a JSON column, so which earlier ones still wait is read here.
            return [.. recent
                .Where(card => card.ChatConversationId == conversationId || card.View.Answer is ProposalAnswer.Waiting)
                .Select(card => card.View)];
        }
    }

    /// <summary>
    /// Proposals as cards, oldest first, read without the change itself, so a stored change that no
    /// longer deserializes cannot take the thread down with it.
    /// </summary>
    private static IQueryable<Card> Cards(IQueryable<ChangeProposal> proposals) =>
        proposals
            .AsNoTracking()
            .OrderBy(proposal => proposal.ProposedAt)
            .Select(proposal => new Card(
                proposal.ChatConversationId,
                ChangeProposalView.Of(proposal.ChangeProposalId, proposal.ProposedAt, proposal.Area, proposal.Summary, proposal.Answer)));

    private sealed record Card(Guid ChatConversationId, ChangeProposalView View);
}
