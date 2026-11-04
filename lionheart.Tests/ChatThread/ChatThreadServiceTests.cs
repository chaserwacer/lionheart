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

    /// <summary>
    /// The session's prompt is frozen at its opening, so only the briefing can tell the model what
    /// today is on a later message: it has to be the current one, and never become history.
    /// </summary>
    [Fact]
    public async Task Every_message_goes_with_the_current_briefing_and_none_is_stored()
    {
        _thread.Model.Responds(CannedChat.Content("Rest."));
        _thread.Model.Responds(CannedChat.Content("Train."));

        _thread.Briefing.Text = "TODAY\nNow: Tuesday.";
        await _thread.SendAsync("Should I train?");
        _thread.Clock.Advance(TimeSpan.FromMinutes(40));
        _thread.Briefing.Text = "TODAY\nNow: Wednesday.";
        var session = await _thread.SendAsync("And now?");

        Assert.Contains("Now: Tuesday.", _thread.Model.ReceivedRequests[0]);
        Assert.Contains("Now: Wednesday.", _thread.Model.ReceivedRequests[1]);
        Assert.DoesNotContain("Now: Tuesday.", _thread.Model.ReceivedRequests[1]);
        Assert.DoesNotContain("Now:", _thread.SystemPromptOf(session.SessionId));
        Assert.All(session.Messages, message => Assert.DoesNotContain("TODAY", message.Content));
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
            _thread.Athlete, new SendThreadMessageRequest { Content = "Hello?" }, ExchangeObserver.Unwatched, CancellationToken.None);

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
            _thread.Athlete, new SendThreadMessageRequest { Content = "   " }, ExchangeObserver.Unwatched, CancellationToken.None));

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

    [Fact]
    public async Task A_session_grown_full_ends_without_a_gap_and_the_next_message_opens_a_new_one()
    {
        var full = _thread.SeedSession(Some.Clock.AddMinutes(-30), "A long session.");
        await using (var context = _thread.Database.CreateContext())
        {
            var message = context.UserChatMessages.Single(m => m.ChatConversationID == full.SessionId);
            message.TokenCount = ChatSessionPolicy.MaxTranscriptTokens;
            context.SaveChanges();
        }
        _thread.Model.Responds(CannedChat.Content("""{"changes":[]}"""));
        _thread.Model.Responds(CannedChat.Content("Fresh session."));

        var next = await _thread.SendAsync("Next question.");

        Assert.NotEqual(full.SessionId, next.SessionId);
        Assert.True(_thread.Session(full.SessionId).IsConsolidated);
    }

    [Fact]
    public async Task A_consolidation_is_kept_even_when_the_answer_after_it_fails()
    {
        var ended = _thread.SeedSession(Some.Clock.AddHours(-5), "My goal is a 405 squat by spring.");
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"remember","memoryId":"","kind":"goal","text":"Squat 405 lb by spring 2027.","sourceMessageIds":["m1"]}]}
            """));
        _thread.Model.Responds(CannedChat.Error("model crashed"), System.Net.HttpStatusCode.InternalServerError);

        await using var context = _thread.Database.CreateContext();
        ResultAssert.Failed(await _thread.Thread(context).SendAsync(
            _thread.Athlete, new SendThreadMessageRequest { Content = "Plan legs." }, ExchangeObserver.Unwatched, CancellationToken.None));

        Assert.True(_thread.Session(ended.SessionId).IsConsolidated);
        Assert.Single(_thread.Memories());
        await using var after = _thread.Database.CreateContext();
        Assert.Single(after.ChatThreadSessions);
    }

    /// <summary>
    /// The session's system prompt is frozen when it opens. A prompt that changed between two
    /// requests of one session would miss the cache for everything after it.
    /// </summary>
    [Fact]
    public async Task Every_request_of_a_session_sends_the_same_system_prompt()
    {
        _thread.Model.Responds(CannedChat.Content("Four sessions."));
        _thread.Model.Responds(CannedChat.Content("Legs were up."));

        await _thread.SendAsync("How did last week go?");
        _thread.Clock.Advance(TimeSpan.FromMinutes(40));
        await _thread.SendAsync("And legs?");

        Assert.Equal(SystemPromptSent(0), SystemPromptSent(1));
    }

    private string SystemPromptSent(int request) =>
        System.Text.Json.JsonDocument.Parse(_thread.Model.ReceivedRequests[request]).RootElement
            .GetProperty("messages")[0].GetProperty("content").ToString();
}
