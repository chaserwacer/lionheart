using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat.Changes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Changes;

/// <summary>The athlete's answers to what the chat proposed: the only way a proposal is ever written.</summary>
public interface IChangeProposalService
{
    /// <summary>
    /// Writes the change and records the confirmation. A write its service refuses still answers the
    /// proposal — as refused, with the service's reason — since the athlete did say yes.
    /// </summary>
    Task<Result<ChangeProposalView, Problem>> ConfirmAsync(IdentityUser user, Guid changeProposalId, CancellationToken ct);

    Task<Result<ChangeProposalView, Problem>> DeclineAsync(IdentityUser user, Guid changeProposalId, CancellationToken ct);
}

/// <summary>
/// Guard → write and settle → commit, the write and the settled answer in one transaction, so a
/// confirmation is never recorded without its write or written twice.
/// </summary>
/// <remarks>
/// The write goes through the page's service, which saves as it goes; the transaction around it is
/// what keeps the two saves one. Two confirmations racing both pass the guard, but the answer is a
/// concurrency token, so the second one's settle finds the row changed and its write is rolled back.
/// </remarks>
public sealed class ChangeProposalService(ModelContext context, ChangeWriter writer, TimeProvider clock) : IChangeProposalService
{
    private readonly ModelContext _context = context;
    private readonly ChangeWriter _writer = writer;
    private readonly TimeProvider _clock = clock;

    public Task<Result<ChangeProposalView, Problem>> ConfirmAsync(IdentityUser user, Guid changeProposalId, CancellationToken ct) =>
        WaitingAsync(user, changeProposalId, ct)
            .BindAsync(proposal => _context.InTransactionAsync(() => WriteAndSettleAsync(user, proposal, ct), ct))
            .MapAsync(ChangeProposalView.Of);

    public Task<Result<ChangeProposalView, Problem>> DeclineAsync(IdentityUser user, Guid changeProposalId, CancellationToken ct) =>
        WaitingAsync(user, changeProposalId, ct)
            .BindAsync(proposal => proposal.Settle(new ProposalAnswer.Declined(Now())))
            .BindAsync(proposal => SaveAsync(proposal, ct))
            .MapAsync(ChangeProposalView.Of);

    // ---- Guard ----

    /// <summary>The proposal, if it is his and still waiting. Another athlete's is indistinguishable from none.</summary>
    private async Task<Result<ChangeProposal, Problem>> WaitingAsync(IdentityUser user, Guid changeProposalId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var proposal = await _context.ChangeProposals
            .FirstOrDefaultAsync(p => p.ChangeProposalId == changeProposalId && p.UserId == userId, ct);

        return proposal switch
        {
            null => Result<ChangeProposal, Problem>.Failure(new Problem.NotFound("Proposed change not found.")),
            { Answer: ProposalAnswer.Waiting } => Result<ChangeProposal, Problem>.Success(proposal),
            _ => Result<ChangeProposal, Problem>.Failure(new Problem.Conflict("That change has already been answered.")),
        };
    }

    // ---- Write and settle ----

    /// <summary>The answer the write earns — confirmed, or refused with its service's reason — recorded on the proposal.</summary>
    private Task<Result<ChangeProposal, Problem>> WriteAndSettleAsync(IdentityUser user, ChangeProposal proposal, CancellationToken ct) =>
        _writer.WriteAsync(user, proposal.Change, ct)
            .TapErrorAsync(_ => AbandonStagedWrite(proposal))
            .MapAsync(_ => (ProposalAnswer)new ProposalAnswer.Confirmed(Now()))
            .RecoverAsync(refusal => new ProposalAnswer.Refused(Now(), refusal.Description))
            .BindAsync(proposal.Settle)
            .BindAsync(settled => SaveAsync(settled, ct));

    /// <summary>
    /// A service may stage part of a write before refusing it, trusting the context to be thrown
    /// away. This context is about to save the refusal, so everything but the proposal is let go
    /// first, or the half-staged write would be saved with it.
    /// </summary>
    private void AbandonStagedWrite(ChangeProposal proposal)
    {
        foreach (var entry in _context.ChangeTracker.Entries().Where(entry => !ReferenceEquals(entry.Entity, proposal)).ToList())
            entry.State = EntityState.Detached;
    }

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    // ---- Commit ----

    private async Task<Result<ChangeProposal, Problem>> SaveAsync(ChangeProposal proposal, CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return Result<ChangeProposal, Problem>.Success(proposal);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ChangeProposal, Problem>.Failure(new Problem.Conflict("That change has already been answered."));
        }
    }
}
