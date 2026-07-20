namespace lionheart.Model.Analysis;

public enum EntryKind { Session, Context }

/// <summary>
/// Denormalized read model written whenever an <see cref="IAnalyzable"/> is decorated.
/// The single backing store for the retrieve primitive: facet filter (jsonb/GIN),
/// time window, and semantic nearest-neighbor (pgvector) all hit this one table.
/// </summary>
public sealed record AnalyzableEntry(
    Guid EntryId,
    EntryKind Kind,
    Guid UserId,
    Period Period,
    IReadOnlyList<Facet> Facets,
    Valence Valence,
    float[] Embedding,
    string SummaryText,
    int DecoratorVersion)
{
    public EntryKind Kind { get; init; } = Require.ClosedEnum(Kind, nameof(Kind));
    public Period Period { get; init; } = Require.Present(Period, nameof(Period));
    public IReadOnlyList<Facet> Facets { get; init; } = Require.Present(Facets, nameof(Facets));
    public Valence Valence { get; init; } = Require.ClosedEnum(Valence, nameof(Valence));
    public float[] Embedding { get; init; } = Require.Present(Embedding, nameof(Embedding));
    public string SummaryText { get; init; } = Require.Present(SummaryText, nameof(SummaryText));
}
