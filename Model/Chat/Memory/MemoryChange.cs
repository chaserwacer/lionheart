namespace lionheart.Model.Chat.Memory;

/// <summary>
/// One change a consolidation makes to the athlete's memory: something new, a revision of
/// something already kept, or the end of something no longer true.
/// </summary>
/// <remarks>
/// Revising and retiring name an existing memory, so a statement that changes an earlier one
/// replaces it instead of sitting beside it as a second, contradicting note.
/// </remarks>
public abstract record MemoryChange
{
    private protected MemoryChange() { }

    /// <summary>The messages of the session the change was drawn from.</summary>
    public abstract IReadOnlyList<Guid> SourceMessageIds { get; }

    public sealed record Remember(MemoryKind Kind, string Text, IReadOnlyList<Guid> SourceMessageIds) : MemoryChange
    {
        public override IReadOnlyList<Guid> SourceMessageIds { get; } = SourceMessageIds;
    }

    public sealed record Revise(Guid MemoryId, MemoryKind Kind, string Text, IReadOnlyList<Guid> SourceMessageIds) : MemoryChange
    {
        public override IReadOnlyList<Guid> SourceMessageIds { get; } = SourceMessageIds;
    }

    public sealed record Retire(Guid MemoryId, string Reason, IReadOnlyList<Guid> SourceMessageIds) : MemoryChange
    {
        public override IReadOnlyList<Guid> SourceMessageIds { get; } = SourceMessageIds;
    }
}
