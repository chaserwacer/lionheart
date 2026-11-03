using lionheart.Model.Chat.Memory;

namespace lionheart.Tests.Support;

/// <summary>A current memory, valid by default, with the one field a test cares about varied.</summary>
public sealed record AMemory(
    Guid UserId,
    MemoryKind Kind = MemoryKind.Fact,
    string Text = "He trains at a commercial gym with a full rack.",
    DateTime? RecordedAt = null,
    float[]? Vector = null)
{
    public static AMemory For(Guid userId) => new(userId);

    public AMemory OfKind(MemoryKind kind) => this with { Kind = kind };

    public AMemory Saying(string text) => this with { Text = text };

    public AMemory RecordedOn(DateTime at) => this with { RecordedAt = at };

    public AMemory EmbeddedAs(float[] vector) => this with { Vector = vector };

    public ChatMemory Build()
    {
        var memory = ResultAssert.Succeeded(ChatMemory.Record(
            Guid.CreateVersion7(), UserId, Kind, Text, new MemoryOrigin.Corrected(), RecordedAt ?? Some.Clock));
        return Vector is { } vector ? memory.Embedded(vector) : memory;
    }
}
