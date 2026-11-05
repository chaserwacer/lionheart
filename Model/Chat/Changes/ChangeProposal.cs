using System.Text.Json.Serialization;
using lionheart.Functional;

namespace lionheart.Model.Chat.Changes;

/// <summary>
/// A change the chat model proposed in a thread session, as the athlete was shown it, and what he
/// made of it.
/// </summary>
/// <remarks>
/// Its own aggregate rather than a kind of chat message: it is answered after the exchange that
/// made it has been stored, from a card, and its answer is the only thing that moves. A sealed class
/// for the reason <see cref="Training.Focus.FocusPin"/> gives — identity and a lifecycle.
/// <para>
/// The summary is stored beside the change rather than rendered from it on every read, because it
/// is what the athlete agreed to: names resolved when it was proposed stay as he read them, even if
/// a movement is renamed before he answers.
/// </para>
/// </remarks>
public sealed class ChangeProposal
{
    private ChangeProposal(
        Guid changeProposalId, Guid userId, Guid chatConversationId, DateTime proposedAt,
        ProposedChange change, ChangeArea area, ProposalSummary summary, ProposalAnswer answer)
    {
        ChangeProposalId = changeProposalId;
        UserId = userId;
        ChatConversationId = chatConversationId;
        ProposedAt = proposedAt;
        Change = change;
        Area = area;
        Summary = summary;
        Answer = answer;
    }

    public Guid ChangeProposalId { get; private init; }

    public Guid UserId { get; private init; }

    /// <summary>The thread session it was proposed in.</summary>
    public Guid ChatConversationId { get; private init; }

    public DateTime ProposedAt { get; private init; }

    public ProposedChange Change { get; private init; }

    /// <summary>
    /// The change's area, kept beside it so the thread reads its cards without reading the change:
    /// a stored change that no longer deserializes then costs only its own confirmation.
    /// </summary>
    public ChangeArea Area { get; private init; }

    public ProposalSummary Summary { get; private init; }

    public ProposalAnswer Answer { get; private set; }

    /// <summary>A proposal as an exchange drafted it, now part of the session it was made in.</summary>
    public static ChangeProposal Open(ProposalDraft draft, Guid userId, Guid chatConversationId, DateTime proposedAt) =>
        new(draft.ChangeProposalId, userId, chatConversationId, proposedAt, draft.Change, draft.Change.Area, draft.Summary,
            new ProposalAnswer.Waiting());

    /// <summary>
    /// Records the athlete's answer. Once only: a second confirmation would write the change twice,
    /// and a decline after a confirmation would say something false about a write that happened.
    /// </summary>
    public Result<ChangeProposal, Problem> Settle(ProposalAnswer answer)
    {
        if (answer is ProposalAnswer.Waiting)
            throw new ArgumentException("A proposal is settled with an answer, not with waiting.", nameof(answer));
        if (Answer is not ProposalAnswer.Waiting)
            return Result<ChangeProposal, Problem>.Failure(new Problem.Conflict("That change has already been answered."));

        Answer = answer;
        return Result<ChangeProposal, Problem>.Success(this);
    }
}

/// <summary>A proposal as the tool that made it hands it to the exchange: not yet in any session.</summary>
public sealed record ProposalDraft(Guid ChangeProposalId, ProposedChange Change, ProposalSummary Summary)
{
    /// <summary>What the model is told the tool did: put a card in front of the athlete, and written nothing.</summary>
    public string ToldToModel =>
        $"Proposed to the athlete, on a card in the chat: {Summary.Spoken} Nothing is written until he confirms it there; he may decline.";
}

/// <summary>
/// What the athlete reads on the card: a title naming the change, and one line per thing it sets
/// or replaces, in his units.
/// </summary>
public sealed record ProposalSummary(string Title, IReadOnlyList<string> Lines)
{
    /// <summary>The summary as one passage, for the model and for VoiceOver.</summary>
    public string Spoken => Sentence(Lines.Count == 0 ? Title : $"{Title}: {string.Join("; ", Lines)}");

    /// <summary>A note the model wrote often ends in its own full stop, which a second would only double.</summary>
    private static string Sentence(string text) => text.EndsWith('.') ? text : $"{text}.";
}

/// <summary>Where a proposal stands: waiting for the athlete, or answered one of three ways.</summary>
/// <remarks>
/// Refused is its own case rather than a confirmation with an error beside it: the athlete said yes
/// and nothing was written, which the card and the model both have to say differently from either a
/// yes that landed or a no.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Waiting), "waiting")]
[JsonDerivedType(typeof(Confirmed), "confirmed")]
[JsonDerivedType(typeof(Declined), "declined")]
[JsonDerivedType(typeof(Refused), "refused")]
public abstract record ProposalAnswer
{
    private protected ProposalAnswer() { }

    public sealed record Waiting : ProposalAnswer;

    public sealed record Confirmed(DateTime At) : ProposalAnswer;

    public sealed record Declined(DateTime At) : ProposalAnswer;

    /// <param name="Reason">What the service that would have written it said.</param>
    public sealed record Refused(DateTime At, string Reason) : ProposalAnswer;
}
