using lionheart.Model.Chat.Memory;
using lionheart.Model.Chat.Thread;
using lionheart.Services.Chat.Thread;
using lionheart.Tests.Support;

namespace lionheart.Tests.ChatThread;

/// <summary>
/// Distilling an ended session, driven by canned consolidation answers: what the changes do to
/// memory, and what a failure leaves behind.
/// </summary>
public sealed class SessionConsolidatorTests : IDisposable
{
    private readonly ThreadHarness _thread = new();

    public void Dispose() => _thread.Dispose();

    private async Task<ConsolidationRun> ConsolidateAsync()
    {
        await using var context = _thread.Database.CreateContext();
        return await _thread.Consolidator(context).ConsolidateEndedAsync(_thread.AthleteId, CancellationToken.None);
    }

    [Fact]
    public async Task Saying_the_knee_is_fine_revises_the_knee_memory_instead_of_adding_a_second_one()
    {
        var knee = _thread.SeedMemory(m => m.OfKind(MemoryKind.Injury).Saying("Left knee pain on deep squats."));
        var session = _thread.SeedSession(Some.Clock.AddDays(10), "My knee is fine now, squatted deep with no pain.");
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"revise","memoryId":"k1","kind":"fact","text":"Left knee pain on deep squats resolved as of 2026-07-28.","sourceMessageIds":["m1"]}]}
            """));

        var run = await ConsolidateAsync();

        Assert.Equal(1, run.Consolidated);
        var memories = _thread.Memories();
        var current = Assert.Single(memories, memory => memory.IsCurrent);
        Assert.Equal(MemoryKind.Fact, current.Kind);
        Assert.Equal(knee.LineageId, current.LineageId);
        Assert.Equal(session.MessageIds, Assert.IsType<MemoryOrigin.Distilled>(current.Origin).SourceMessageIds);

        var old = Assert.Single(memories, memory => memory.MemoryId == knee.MemoryId);
        Assert.Equal(current.MemoryId, Assert.IsType<MemoryStanding.Superseded>(old.Standing).By);

        Assert.Equal(new SessionConsolidation.Consolidated(Some.Clock, 0, 1, 0), _thread.Session(session.SessionId).Consolidation);
    }

    [Fact]
    public async Task What_is_remembered_is_embedded_as_it_is_written()
    {
        _thread.SeedSession(Some.Clock, "My goal is a 405 squat by spring.");
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"remember","memoryId":"","kind":"goal","text":"Squat 405 lb by spring 2027.","sourceMessageIds":["m1"]}]}
            """));

        await ConsolidateAsync();

        var goal = Assert.Single(_thread.Memories());
        Assert.True(goal.IsEmbedded);
        Assert.Contains("Squat 405 lb by spring 2027.", _thread.Embeddings.Requested);
    }

    [Fact]
    public async Task A_memory_is_kept_unembedded_when_the_embedding_server_is_down()
    {
        _thread.Embeddings.FailWith = "connection refused";
        _thread.SeedSession(Some.Clock, "My goal is a 405 squat by spring.");
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"remember","memoryId":"","kind":"goal","text":"Squat 405 lb by spring 2027.","sourceMessageIds":["m1"]}]}
            """));

        var run = await ConsolidateAsync();

        Assert.Equal(1, run.Consolidated);
        Assert.False(Assert.Single(_thread.Memories()).IsEmbedded);
    }

    [Fact]
    public async Task Two_changes_to_one_memory_apply_the_first_and_set_aside_the_second()
    {
        _thread.SeedMemory(m => m.OfKind(MemoryKind.Goal).Saying("Bench 275."));
        _thread.SeedSession(Some.Clock, "Bench goal is 285 now. Actually make it 295.");
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[
              {"action":"revise","memoryId":"k1","kind":"goal","text":"Bench 285.","sourceMessageIds":["m1"]},
              {"action":"revise","memoryId":"k1","kind":"goal","text":"Bench 295.","sourceMessageIds":["m1"]}
            ]}
            """));

        await ConsolidateAsync();

        Assert.Equal("Bench 285.", Assert.Single(_thread.Memories(), memory => memory.IsCurrent).Text);
        Assert.Equal(2, _thread.Memories().Count);
    }

    [Fact]
    public async Task Remembering_what_is_already_kept_adds_nothing()
    {
        _thread.SeedMemory(m => m.Saying("He trains at 6am."));
        _thread.SeedSession(Some.Clock, "Reminder, I train at 6am.");
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"remember","memoryId":"","kind":"fact","text":"he trains at 6am.","sourceMessageIds":["m1"]}]}
            """));

        await ConsolidateAsync();

        Assert.Single(_thread.Memories());
    }

    [Fact]
    public async Task A_model_that_cannot_be_reached_leaves_memory_untouched_and_the_session_owed()
    {
        var kept = _thread.SeedMemory(m => m.OfKind(MemoryKind.Injury).Saying("Left knee pain."));
        var session = _thread.SeedSession(Some.Clock, "Knee is fine.");
        _thread.Model.RespondsWithTransportFailure();

        var run = await ConsolidateAsync();

        Assert.Equal(0, run.Consolidated);
        Assert.Single(run.Failures);
        var memory = Assert.Single(_thread.Memories());
        Assert.Equal(kept.MemoryId, memory.MemoryId);
        Assert.True(memory.IsCurrent);
        Assert.Equal(1, Assert.IsType<SessionConsolidation.Failed>(_thread.Session(session.SessionId).Consolidation).Attempts);
    }

    [Fact]
    public async Task Earlier_sessions_are_distilled_first_so_a_later_one_can_revise_what_they_remembered()
    {
        _thread.SeedSession(Some.Clock, "Bench goal is 275.");
        _thread.SeedSession(Some.Clock.AddDays(3), "Bench goal is 285 now.");
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"remember","memoryId":"","kind":"goal","text":"Bench 275.","sourceMessageIds":["m1"]}]}
            """));
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"revise","memoryId":"k1","kind":"goal","text":"Bench 285.","sourceMessageIds":["m1"]}]}
            """));

        var run = await ConsolidateAsync();

        Assert.Equal(2, run.Consolidated);
        Assert.Contains("Bench goal is 275.", _thread.Model.ReceivedRequests[0]);
        Assert.Equal("Bench 285.", Assert.Single(_thread.Memories(), memory => memory.IsCurrent).Text);
    }

    [Fact]
    public async Task The_background_pass_leaves_the_newest_session_alone_until_its_gap_has_passed()
    {
        var older = _thread.SeedSession(Some.Clock.AddHours(-30), "Old session.");
        var newest = _thread.SeedSession(Some.Clock.AddMinutes(-30), "Still talking.");
        _thread.Model.Responds(CannedChat.Content("""{"changes":[]}"""));

        await using (var context = _thread.Database.CreateContext())
        {
            Assert.Equal(1, (await _thread.Consolidator(context).ConsolidateAllEndedAsync(CancellationToken.None)).Consolidated);
        }

        Assert.True(_thread.Session(older.SessionId).IsConsolidated);
        Assert.False(_thread.Session(newest.SessionId).IsConsolidated);

        _thread.Clock.Advance(ChatSessionPolicy.IdleGap);
        _thread.Model.Responds(CannedChat.Content("""{"changes":[]}"""));
        await using (var context = _thread.Database.CreateContext())
        {
            await _thread.Consolidator(context).ConsolidateAllEndedAsync(CancellationToken.None);
        }

        Assert.True(_thread.Session(newest.SessionId).IsConsolidated);
    }
}
