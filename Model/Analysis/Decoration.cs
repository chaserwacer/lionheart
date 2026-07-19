namespace lionheart.Model.Analysis;

public enum Valence { Positive, Negative, Neutral, Mixed }

/// <summary>
/// The fixed set of analysis axes. Values within an axis grow via the <see cref="FacetValue"/>
/// vocabulary; the axes themselves do not — they are the finite query surface exposed to
/// analytics and MCP tools.
/// </summary>
public enum FacetAxis { Modality, MovementPattern, MuscleRegion, Intent, LifeDomain, BodyState }

/// <summary>
/// One (axis, canonical value) annotation. <see cref="Value"/> must be a canonical
/// <see cref="FacetValue"/> spelling — the resolve step guarantees this at ingestion.
/// </summary>
public sealed record Facet(FacetAxis Axis, string Value);

/// <summary>
/// Handle to a stored embedding vector. Vectors live in storage (pgvector), never on domain records.
/// </summary>
public sealed record EmbeddingRef(Guid EmbeddingId);

/// <summary>
/// Machine-generated annotation carried by everything analyzable: closed-vocab facets for
/// deterministic analytics, free tags + embedding + valence for semantic recall.
/// </summary>
public sealed record Decoration(
    IReadOnlyList<Facet> Facets,
    IReadOnlyList<string> FreeTags,
    Valence Valence,
    EmbeddingRef Embedding,
    int DecoratorVersion);

/// <summary>
/// Fill-state of an entry's decoration. Sessions are decorated on completion, contexts on
/// creation; anything not yet processed is <see cref="NotDecorated"/> — never null.
/// </summary>
public abstract record DecorationState
{
    public bool IsDecorated => this is Decorated;
}

public sealed record NotDecorated : DecorationState;

public sealed record Decorated(Decoration Decoration) : DecorationState;
