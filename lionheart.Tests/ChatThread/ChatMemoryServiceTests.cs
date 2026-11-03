using lionheart.Functional;
using lionheart.Model.Chat.Memory;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.ChatThread;

/// <summary>The athlete's hands on memory: reading it with its sources, correcting it, forgetting it.</summary>
public sealed class ChatMemoryServiceTests : IDisposable
{
    private readonly ThreadHarness _thread = new();

    public void Dispose() => _thread.Dispose();

    [Fact]
    public async Task Correcting_a_memory_replaces_it_and_keeps_what_it_said_before()
    {
        var knee = _thread.SeedMemory(m => m.OfKind(MemoryKind.Injury).Saying("Right knee pain."));

        await using var context = _thread.Database.CreateContext();
        var corrected = ResultAssert.Succeeded(await _thread.Memory(context).CorrectAsync(
            _thread.Athlete, knee.MemoryId,
            new CorrectMemoryRequest { Kind = MemoryKind.Injury, Text = "Left knee pain, not right." },
            CancellationToken.None));

        Assert.Equal("Left knee pain, not right.", corrected.Text);
        Assert.Equal(MemorySource.Athlete, corrected.Source);
        Assert.Equal("Right knee pain.", Assert.Single(corrected.Earlier).Text);
        Assert.Single(_thread.Memories(), memory => memory.IsCurrent);
    }

    [Fact]
    public async Task A_memory_already_replaced_cannot_be_corrected()
    {
        var knee = _thread.SeedMemory(m => m.Saying("First."));
        await using var context = _thread.Database.CreateContext();
        var service = _thread.Memory(context);
        var request = new CorrectMemoryRequest { Kind = MemoryKind.Fact, Text = "Second." };
        ResultAssert.Succeeded(await service.CorrectAsync(_thread.Athlete, knee.MemoryId, request, CancellationToken.None));

        var problem = ResultAssert.Failed(await service.CorrectAsync(_thread.Athlete, knee.MemoryId, request, CancellationToken.None));

        Assert.IsType<Problem.Conflict>(problem);
    }

    [Fact]
    public async Task Forgetting_a_memory_removes_every_version_of_it()
    {
        var knee = _thread.SeedMemory(m => m.Saying("First."));
        await using (var context = _thread.Database.CreateContext())
        {
            var corrected = ResultAssert.Succeeded(await _thread.Memory(context).CorrectAsync(
                _thread.Athlete, knee.MemoryId, new CorrectMemoryRequest { Kind = MemoryKind.Fact, Text = "Second." },
                CancellationToken.None));

            ResultAssert.Succeeded(await _thread.Memory(context).DeleteAsync(_thread.Athlete, corrected.MemoryId, CancellationToken.None));
        }

        Assert.Empty(_thread.Memories());
    }

    [Fact]
    public async Task Another_athletes_memory_is_not_found()
    {
        var knee = _thread.SeedMemory(m => m.Saying("Mine."));
        await using var context = _thread.Database.CreateContext();
        var stranger = new IdentityUser { Id = Guid.NewGuid().ToString() };

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(
            await _thread.Memory(context).DeleteAsync(stranger, knee.MemoryId, CancellationToken.None)));
    }

    [Fact]
    public async Task A_distilled_memory_is_read_with_the_words_it_came_from()
    {
        var session = _thread.SeedSession(Some.Clock, "My goal is a 405 squat by spring.");
        _thread.Model.Responds(CannedChat.Content("""
            {"changes":[{"action":"remember","memoryId":"","kind":"goal","text":"Squat 405 lb by spring 2027.","sourceMessageIds":["m1"]}]}
            """));
        await using (var context = _thread.Database.CreateContext())
        {
            await _thread.Consolidator(context).ConsolidateEndedAsync(_thread.AthleteId, CancellationToken.None);
        }

        await using var reading = _thread.Database.CreateContext();
        var memory = Assert.Single(ResultAssert.Succeeded(
            await _thread.Memory(reading).GetCurrentAsync(_thread.Athlete, CancellationToken.None)));

        Assert.Equal(MemorySource.Chat, memory.Source);
        var quote = Assert.Single(memory.Quotes);
        Assert.Equal(session.MessageIds[0], quote.MessageId);
        Assert.True(quote.ByAthlete);
        Assert.Equal("My goal is a 405 squat by spring.", quote.Said);
    }

    [Fact]
    public async Task Standing_memories_are_listed_before_the_rest()
    {
        _thread.SeedMemory(m => m.Saying("Newer fact.").RecordedOn(Some.Clock.AddDays(5)));
        _thread.SeedMemory(m => m.OfKind(MemoryKind.Goal).Saying("Older goal.").RecordedOn(Some.Clock));

        await using var context = _thread.Database.CreateContext();
        var listed = ResultAssert.Succeeded(await _thread.Memory(context).GetCurrentAsync(_thread.Athlete, CancellationToken.None));

        Assert.Equal(["Older goal.", "Newer fact."], listed.Select(memory => memory.Text));
    }
}
