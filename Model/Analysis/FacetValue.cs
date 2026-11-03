namespace lionheart.Model.Analysis;

/// <summary>
/// A canonical vocabulary entry for one <see cref="FacetAxis"/>. The resolve step snaps
/// LLM-proposed values to the nearest canonical (accumulating <see cref="Aliases"/>) or
/// admits a new canonical when nothing is close enough — the vocabulary grows deliberately,
/// never by spelling drift.
/// </summary>
public sealed record FacetValue(
    FacetAxis Axis,
    string Canonical,
    IReadOnlyList<string> Aliases,
    float[] Embedding);
