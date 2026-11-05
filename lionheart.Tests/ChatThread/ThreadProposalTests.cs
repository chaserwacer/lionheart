using System.Net;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Changes;
using lionheart.Model.Chat.Thread;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.ChatThread;

/// <summary>
/// A change proposed in the thread: kept with the session it was made in and shown there as a card,
/// told back to the model as it stands with every later message, and gone with a failed exchange.
/// </summary>
public sealed class ThreadProposalTests : IDisposable
{
    private readonly ThreadHarness _thread = new();

    public void Dispose() => _thread.Dispose();

    private async Task<ThreadSessionView> ProposeAsync(string words = "Plan Wednesday's run")
    {
        _thread.Model.Responds(CannedChat.ToolCall("propose", """{"phrase":"Plan a run on Wed"}"""));
        _thread.Model.Responds(CannedChat.Content("It is on a card for you."));
        return await _thread.SendAsync(words);
    }

    [Fact]
    public async Task A_proposal_is_kept_with_its_session_and_shown_there_waiting()
    {
        var session = await ProposeAsync();

        var shown = Assert.Single(session.Proposals);
        Assert.Equal(("Plan a run on Wed", ChangeArea.Plan), (shown.Title, shown.Area));
        Assert.IsType<ProposalAnswer.Waiting>(shown.Answer);

        await using var context = _thread.Database.CreateContext();
        var stored = Assert.Single(await context.ChangeProposals.ToListAsync());
        Assert.Equal((session.SessionId, _thread.AthleteId), (stored.ChatConversationId, stored.UserId));
    }

    [Fact]
    public async Task Reading_the_thread_back_shows_each_card_in_its_session()
    {
        var session = await ProposeAsync();

        await using var context = _thread.Database.CreateContext();
        var page = ResultAssert.Succeeded(
            await _thread.Thread(context).ReadAsync(_thread.Athlete, new ThreadPage.Latest(), default));

        Assert.Equal(session.Proposals.Single().ChangeProposalId, Assert.Single(Assert.Single(page.Sessions).Proposals).ChangeProposalId);
    }

    [Fact]
    public async Task The_next_message_tells_the_model_where_each_proposal_stands()
    {
        var session = await ProposeAsync();
        await using (var context = _thread.Database.CreateContext())
        {
            var proposal = await context.ChangeProposals.SingleAsync();
            proposal.Settle(new ProposalAnswer.Declined(Some.Clock));
            await context.SaveChangesAsync();
        }

        _thread.Clock.Advance(TimeSpan.FromMinutes(5));
        _thread.Model.Responds(CannedChat.Content("Understood."));
        await _thread.SendAsync("Actually not this week.");

        Assert.Contains("Plan a run on Wed: one line. He declined it; nothing was written.", _thread.Model.ReceivedRequests[^1]);
        Assert.DoesNotContain("CHANGES YOU PROPOSED", _thread.Model.ReceivedRequests[0]);
    }

    [Fact]
    public async Task A_failed_exchange_leaves_no_card_behind()
    {
        _thread.Model.Responds(CannedChat.ToolCall("propose", """{"phrase":"Plan a run on Wed"}"""));
        _thread.Model.Responds("{\"error\":\"overloaded\"}", HttpStatusCode.ServiceUnavailable);

        await using (var context = _thread.Database.CreateContext())
        {
            var failed = await _thread.Thread(context).SendAsync(
                _thread.Athlete, new SendThreadMessageRequest { Content = "Plan Wednesday" }, ExchangeObserver.Unwatched, default);
            ResultAssert.Failed(failed);
        }

        await using var read = _thread.Database.CreateContext();
        Assert.Empty(await read.ChangeProposals.ToListAsync());
    }
}
