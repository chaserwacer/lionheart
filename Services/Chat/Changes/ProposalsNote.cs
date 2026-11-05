using System.Diagnostics;
using System.Text;
using lionheart.Model.Chat.Changes;

namespace lionheart.Services.Chat.Changes;

/// <summary>
/// Where the changes the model proposed stand, told to it with each message after the briefing, so
/// it knows which landed, which he declined and which still wait, however long after its own answer
/// he tapped the card.
/// </summary>
/// <remarks>
/// Told rather than stored as a turn, for the briefing's reason: it is a fact about now, and an
/// answer the athlete gives later would leave a stored "still waiting" saying something false. The
/// tool result kept in the session says only what was proposed.
/// </remarks>
public static class ProposalsNote
{
    public static string Render(IReadOnlyList<ChangeProposalView> proposals)
    {
        if (proposals.Count == 0) return string.Empty;

        var text = new StringBuilder("CHANGES YOU PROPOSED (as of this message; the athlete answers each on its card)");
        foreach (var proposal in proposals.OrderBy(p => p.ProposedAt))
            text.AppendLine().Append(System.Globalization.CultureInfo.InvariantCulture, $"- {proposal.Spoken} {Standing(proposal.Answer)}");
        return text.ToString();
    }

    private static string Standing(ProposalAnswer answer) => answer switch
    {
        ProposalAnswer.Waiting => "Waiting for his answer.",
        ProposalAnswer.Confirmed => "He confirmed it, and it is written.",
        ProposalAnswer.Declined => "He declined it; nothing was written.",
        ProposalAnswer.Refused refused => $"He confirmed it, but it could not be written: {refused.Reason}",
        _ => throw new UnreachableException(),
    };
}
