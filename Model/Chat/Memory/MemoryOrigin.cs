using System.Text.Json.Serialization;

namespace lionheart.Model.Chat.Memory;

/// <summary>Where one version of a memory came from.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Distilled), "distilled")]
[JsonDerivedType(typeof(Corrected), "corrected")]
public abstract record MemoryOrigin
{
    private protected MemoryOrigin() { }

    /// <summary>
    /// Distilled from a thread session, out of the messages in <paramref name="SourceMessageIds"/>.
    /// The list can be empty when the model named no message, and the session still says where to look.
    /// </summary>
    public sealed record Distilled(Guid SessionId, IReadOnlyList<Guid> SourceMessageIds) : MemoryOrigin;

    /// <summary>Written by the athlete himself, correcting what the chat had kept.</summary>
    public sealed record Corrected : MemoryOrigin;
}
