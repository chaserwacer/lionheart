using lionheart.Model.Chat.Changes;
using lionheart.Services.Chat.Changes;

namespace lionheart.Tests.ChatChanges;

/// <summary>What the model is told about its proposals with a message: each one, and how it stands.</summary>
public sealed class ProposalsNoteTests
{
    private static readonly DateTime At = new(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc);

    private static ChangeProposalView Proposed(string title, int minutes, ProposalAnswer? answer = null) =>
        new(Guid.NewGuid(), At.AddMinutes(minutes), ChangeArea.Plan, title, [], answer ?? new ProposalAnswer.Waiting());

    [Fact]
    public void Nothing_proposed_says_nothing()
    {
        Assert.Equal(string.Empty, ProposalsNote.Render([]));
    }

    [Fact]
    public void Each_proposal_is_told_with_how_it_stands_oldest_first()
    {
        var note = ProposalsNote.Render(
        [
            Proposed("Refused one", 3, new ProposalAnswer.Refused(At, "That day is already marked non-training.")),
            Proposed("Waiting one", 0),
            Proposed("Confirmed one", 1, new ProposalAnswer.Confirmed(At)),
            Proposed("Declined one", 2, new ProposalAnswer.Declined(At)),
        ]);

        Assert.Equal(
            [
                "CHANGES YOU PROPOSED (as of this message; the athlete answers each on its card)",
                "- Waiting one. Waiting for his answer.",
                "- Confirmed one. He confirmed it, and it is written.",
                "- Declined one. He declined it; nothing was written.",
                "- Refused one. He confirmed it, but it could not be written: That day is already marked non-training.",
            ],
            note.Split('\n'));
    }

    [Fact]
    public void A_line_that_ends_its_own_sentence_is_not_given_a_second_full_stop()
    {
        Assert.Equal("Plan a run: Note: with friends.", new ProposalSummary("Plan a run", ["Note: with friends."]).Spoken);
        Assert.Equal("Plan a run: Easy run.", new ProposalSummary("Plan a run", ["Easy run"]).Spoken);
    }
}
