using System.Text.Json.Serialization;

namespace lionheart.Model.Chat.Memory;

/// <summary>
/// Whether a version of a memory is the one that holds now.
/// </summary>
/// <remarks>
/// A memory changes by being superseded, never by being edited in place: the version that held
/// before stays, dated, so "his knee hurt on deep squats in October and was fine by November" is
/// still in the record after the second statement replaced the first as the current one.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Current), "current")]
[JsonDerivedType(typeof(Superseded), "superseded")]
[JsonDerivedType(typeof(Retired), "retired")]
public abstract record MemoryStanding
{
    private protected MemoryStanding() { }

    public sealed record Current : MemoryStanding;

    /// <summary>Replaced at <paramref name="At"/> by the version <paramref name="By"/>.</summary>
    public sealed record Superseded(Guid By, DateTime At) : MemoryStanding;

    /// <summary>No longer true, and nothing replaced it — a commitment kept, a plan dropped.</summary>
    public sealed record Retired(DateTime At, string Reason) : MemoryStanding;
}
