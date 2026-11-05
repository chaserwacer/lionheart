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
            (await context.ChangeProposals
                .AsNoTracking()
                .Where(proposal => conversationIds.Contains(proposal.ChatConversationId))
                .OrderBy(proposal => proposal.ProposedAt)
                .ToListAsync(ct))
            .ToLookup(proposal => proposal.ChatConversationId, ChangeProposalView.Of);

        /// <summary>
        /// What the model hears about with a message: everything proposed in this session, and his
        /// cards from earlier sessions of the last fortnight that still wait for an answer.
        /// </summary>
        public async Task<IReadOnlyList<ChangeProposal>> ProposalsToTellAsync(
            Guid userId, Guid conversationId, DateTime now, CancellationToken ct)
        {
            var since = now - WaitingRemembered;
            var recent = await context.ChangeProposals
                .AsNoTracking()
                .Where(proposal => proposal.UserId == userId
                                   && (proposal.ChatConversationId == conversationId || proposal.ProposedAt >= since))
                .ToListAsync(ct);

            // The answer is a JSON column, so which earlier ones still wait is read here.
            return [.. recent.Where(proposal =>
                proposal.ChatConversationId == conversationId || proposal.Answer is ProposalAnswer.Waiting)];
        }
    }
}
