using lionheart.Model.Chat;
using lionheart.Model.Chat.Memory;
using lionheart.Model.Chat.Thread;
using lionheart.Services.Chat;
using lionheart.Functional;
using lionheart.Services.Chat.Thread;
using lionheart.Tests.Support;

namespace lionheart.Tests.ChatThread;

/// <summary>
/// The thread end to end: a message continues the open session or, after the idle gap, has the
/// ended one distilled into memory and opens a new session that starts with it.
/// </summary>
public sealed class ChatThreadServiceTests : IDisposable
{
    private readonly ThreadHarness _thread = new();

    public void Dispose() => _thread.Dispose();

    [Fact]
    public async Task The_first_message_opens_a_session_and_keeps_both_turns()
    {
        _thread.Model.Responds(CannedChat.Content("Four sessions last week."));

        var session = await _thread.SendAsync("How did last week go?");

        Assert.Equal(["How did last week go?", "Four sessions last week."], session.Messages.Select(m => m.Content));
        Assert.Equal(Some.Clock, session.OpenedAt);
        Assert.Contains("Nothing is remembered yet.", _thread.SystemPromptOf(session.SessionId));
    }

    [Fact]
    public async Task Coming_back_within_the_gap_continues_the_same_session_with_its_history()
    {
        _thread.Model.Responds(CannedChat.Content("Four sessions last week."));
        _thread.Model.Responds(CannedChat.Content("Squat volume was up 8%."));

        var first = await _thread.SendAsync("How did last week go?");
        _thread.Clock.Advance(TimeSpan.FromMinutes(40));
        var second = await _thread.SendAsync("And legs?");

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(4, second.Messages.Count);
        Assert.Contains("Four sessions last week.", _thread.Model.ReceivedRequests[1]);
    }

    [Fact]
    public async Task Coming_back_after_the_gap_distils_the_ended_session_and_opens_a_new_one_that_starts_with_it()
    {
        _thread.Model.Responds(CannedChat.Content("Noted. Box squats keep the knee out of deep flexion."));
        var first = await _thread.SendAsync("My left knee hurts on deep squats.");

        _thread.Clock.Advance(TimeSpan.FromHours(5));
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"remember","memoryId":"","kind":"injury","text":"Left knee pain on deep squats, reported 2026-07-18.","sourceMessageIds":["m1"]}]}
            """));
        _thread.Model.Responds(CannedChat.Content("Hack squat to parallel, two sets."));
        var second = await _thread.SendAsync("What should legs look like today?");

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(2, second.Messages.Count);
        Assert.True(_thread.Session(first.SessionId).IsConsolidated);

        var knee = Assert.Single(_thread.Memories());
        Assert.Equal(MemoryKind.Injury, knee.Kind);
        var origin = Assert.IsType<MemoryOrigin.Distilled>(knee.Origin);
        Assert.Equal(first.SessionId, origin.SessionId);
        Assert.Equal([first.Messages[0].ChatMessageItemID], origin.SourceMessageIds);

        Assert.Contains("Left knee pain on deep squats", _thread.SystemPromptOf(second.SessionId));
        Assert.DoesNotContain("My left knee hurts", _thread.Model.ReceivedRequests[2]);
    }

    [Fact]
    public async Task A_failed_consolidation_does_not_hold_up_the_message_and_is_left_to_retry()
    {
        _thread.Model.Responds(CannedChat.Content("Noted."));
        var first = await _thread.SendAsync("I train at 6am now.");

        _thread.Clock.Advance(TimeSpan.FromHours(5));
        _thread.Model.Responds(CannedChat.Content("Not the JSON you asked for."));
        _thread.Model.Responds(CannedChat.Content("Morning sessions, then."));
        var second = await _thread.SendAsync("Plan this week.");

        Assert.NotEqual(first.SessionId, second.SessionId);
        var failed = Assert.IsType<SessionConsolidation.Failed>(_thread.Session(first.SessionId).Consolidation);
        Assert.Equal(1, failed.Attempts);
        Assert.Empty(_thread.Memories());
    }

    [Fact]
    public async Task A_failed_exchange_writes_nothing_not_even_the_session_opened_for_it()
    {
        _thread.Model.Responds(CannedChat.Error("model crashed"), System.Net.HttpStatusCode.InternalServerError);

        await using var context = _thread.Database.CreateContext();
        var result = await _thread.Thread(context).SendAsync(
            _thread.Athlete, new SendThreadMessageRequest { Content = "Hello?" }, CancellationToken.None);

        ResultAssert.Failed(result);
        await using var after = _thread.Database.CreateContext();
        Assert.Empty(after.ChatThreadSessions);
        Assert.Empty(after.ChatConversations);
        Assert.Empty(after.UserChatMessages);
    }

    [Fact]
    public async Task An_empty_message_is_refused_before_the_model_is_asked()
    {
        await using var context = _thread.Database.CreateContext();

        var problem = ResultAssert.Failed(await _thread.Thread(context).SendAsync(
            _thread.Athlete, new SendThreadMessageRequest { Content = "   " }, CancellationToken.None));

        Assert.IsType<Problem.Invalid>(problem);
        Assert.Equal(0, _thread.Model.RequestCount);
    }

    [Fact]
    public async Task The_thread_reads_back_a_page_of_sessions_at_a_time_oldest_first()
    {
        var openings = Enumerable.Range(0, ChatThreadService.PageSize + 2)
            .Select(day => Some.Clock.AddDays(day))
            .ToList();
        foreach (var opened in openings) _thread.SeedSession(opened, $"Day {opened:dd}");

        await using var context = _thread.Database.CreateContext();
        var newest = ResultAssert.Succeeded(await _thread.Thread(context).ReadAsync(
            _thread.Athlete, new ThreadPage.Latest(), CancellationToken.None));

        Assert.True(newest.HasEarlier);
        Assert.Equal(openings.TakeLast(ChatThreadService.PageSize), newest.Sessions.Select(s => s.OpenedAt));

        var earlier = ResultAssert.Succeeded(await _thread.Thread(context).ReadAsync(
            _thread.Athlete, new ThreadPage.Before(newest.Sessions[0].OpenedAt), CancellationToken.None));

        Assert.False(earlier.HasEarlier);
        Assert.Equal(openings.Take(2), earlier.Sessions.Select(s => s.OpenedAt));
    }

    [Fact]
    public async Task Thread_sessions_never_show_up_as_standalone_conversations()
    {
        _thread.Model.Responds(CannedChat.Content("Four sessions."));
        var session = await _thread.SendAsync("How did last week go?");

        await using var context = _thread.Database.CreateContext();
        var conversations = new ChatConversationService(context, _thread.SystemPrompt);

        Assert.Empty(ResultAssert.Succeeded(await conversations.GetAllChatConversationsAsync(_thread.Athlete)));
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(
            await conversations.DeleteChatConversationAsync(_thread.Athlete, session.SessionId)));
    }
}
