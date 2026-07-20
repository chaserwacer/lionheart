using System.Text.Json.Serialization;

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
public sealed record Facet(FacetAxis Axis, string Value)
{
    public FacetAxis Axis { get; init; } = Require.ClosedEnum(Axis, nameof(Axis));
    public string Value { get; init; } = Require.Present(Value, nameof(Value));
}

/// <summary>
/// Machine-generated annotation carried by everything analyzable: closed-vocab facets for
/// deterministic analytics, free tags + summary + valence for semantic recall. The
/// embedding vector over <see cref="SummaryText"/> lives on the <see cref="AnalyzableEntry"/>
/// projection, never on domain records.
/// </summary>
public sealed record Decoration(
    IReadOnlyList<Facet> Facets,
    IReadOnlyList<string> FreeTags,
    Valence Valence,
    string SummaryText,
    int DecoratorVersion)
{
    // Every member is redeclared, including the unguarded DecoratorVersion: System.Text.Json
    // writes non-redeclared positional members first, so redeclaring only some of them would
    // reorder the stored payload. This record is persisted as JSON, so that order is a
    // storage contract (pinned in StorageFormatTests).
    public IReadOnlyList<Facet> Facets { get; init; } = Require.Present(Facets, nameof(Facets));
    public IReadOnlyList<string> FreeTags { get; init; } = Require.Present(FreeTags, nameof(FreeTags));
    public Valence Valence { get; init; } = Require.ClosedEnum(Valence, nameof(Valence));
    public string SummaryText { get; init; } = Require.Present(SummaryText, nameof(SummaryText));
    public int DecoratorVersion { get; init; } = DecoratorVersion;
}

/// <summary>
/// Fill-state of an entry's decoration. Sessions are decorated on completion, contexts on
/// creation; anything not yet processed is <see cref="NotDecorated"/> — never null.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NotDecorated), "none")]
[JsonDerivedType(typeof(Decorated), "decorated")]
public abstract record DecorationState
{
    /// <summary>Derived from the subtype, so it is never written to the stored payload.</summary>
    [JsonIgnore]
    public bool IsDecorated => this is Decorated;
}

public sealed record NotDecorated : DecorationState;

public sealed record Decorated(Decoration Decoration) : DecorationState;
