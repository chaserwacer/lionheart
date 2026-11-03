using System.Text.Json;
using lionheart.Data;
using lionheart.Model.Chat.Memory;
using lionheart.Model.Chat.Thread;

namespace lionheart.Tests.Serialization;

/// <summary>
/// The bytes the chat thread's JSON columns hold — a memory's origin and standing, and a session's
/// consolidation — written out by hand, for the reason <see cref="StorageFormatTests"/> gives: a
/// serializer-level change round-trips happily while orphaning every stored row.
/// </summary>
public class ChatMemoryStorageFormatTests
{
    private static readonly Guid Id = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTime At = new(2026, 11, 3, 18, 12, 0, DateTimeKind.Utc);

    private static string Store<T>(T value) => JsonSerializer.Serialize(value, ValueConversions.StorageJson);

    [Fact]
    public void A_distilled_origin_stores_its_session_and_sources()
    {
        Assert.Equal(
            """{"$type":"distilled","SessionId":"11111111-2222-3333-4444-555555555555","SourceMessageIds":["11111111-2222-3333-4444-555555555555"]}""",
            Store<MemoryOrigin>(new MemoryOrigin.Distilled(Id, [Id])));
        Assert.Equal("""{"$type":"corrected"}""", Store<MemoryOrigin>(new MemoryOrigin.Corrected()));
    }

    [Fact]
    public void Every_standing_stores_exactly()
    {
        Assert.Equal("""{"$type":"current"}""", Store<MemoryStanding>(new MemoryStanding.Current()));
        Assert.Equal(
            """{"$type":"superseded","By":"11111111-2222-3333-4444-555555555555","At":"2026-11-03T18:12:00Z"}""",
            Store<MemoryStanding>(new MemoryStanding.Superseded(Id, At)));
        Assert.Equal(
            """{"$type":"retired","At":"2026-11-03T18:12:00Z","Reason":"Done."}""",
            Store<MemoryStanding>(new MemoryStanding.Retired(At, "Done.")));
    }

    [Fact]
    public void Every_consolidation_stores_exactly()
    {
        Assert.Equal("""{"$type":"pending"}""", Store<SessionConsolidation>(new SessionConsolidation.Pending()));
        Assert.Equal(
            """{"$type":"consolidated","At":"2026-11-03T18:12:00Z","Remembered":2,"Revised":1,"Retired":0}""",
            Store<SessionConsolidation>(new SessionConsolidation.Consolidated(At, 2, 1, 0)));
        Assert.Equal(
            """{"$type":"failed","LastAttemptAt":"2026-11-03T18:12:00Z","Attempts":3,"Reason":"model down"}""",
            Store<SessionConsolidation>(new SessionConsolidation.Failed(At, 3, "model down")));
    }

    /// <summary>The wire and the column both carry the kind as its ordinal.</summary>
    [Fact]
    public void Memory_kind_ordinals_are_frozen() =>
        Assert.Equal(
            [0, 1, 2, 3, 4, 5],
            new[] { MemoryKind.Goal, MemoryKind.Injury, MemoryKind.Preference, MemoryKind.Commitment, MemoryKind.Decision, MemoryKind.Fact }
                .Select(kind => (int)kind));
}
