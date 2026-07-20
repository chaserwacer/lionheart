using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Analysis;
using lionheart.Model.Interpretation;
using lionheart.Services.Embedding;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Interpretation;

/// <summary>
/// The canonical spelling chosen for every proposed value in one interpretation. Resolving
/// the whole run at once keeps it to a single embedding round-trip; the map is then applied
/// to the entry and to each set separately.
/// </summary>
public sealed class ResolvedVocabulary
{
    private readonly IReadOnlyDictionary<(FacetAxis, string), string> _canonicals;

    internal ResolvedVocabulary(IReadOnlyDictionary<(FacetAxis, string), string> canonicals) =>
        _canonicals = canonicals;

    /// <summary>
    /// Canonicalizes in the caller's order, dropping duplicates that only collide once distinct
    /// proposals snap to the same canonical.
    /// </summary>
    public IReadOnlyList<Facet> Canonicalize(IReadOnlyList<ProposedFacet> proposed) =>
        [.. proposed
            .Where(p => _canonicals.ContainsKey(FacetResolver.Key(p)))
            .Select(p => new Facet(p.Axis, _canonicals[FacetResolver.Key(p)]))
            .Distinct()];
}

public interface IFacetResolver
{
    Task<Result<ResolvedVocabulary>> ResolveAsync(IReadOnlyList<ProposedFacet> proposed);
}

/// <summary>
/// Snaps parser-proposed facet values onto the canonical <see cref="FacetValue"/> vocabulary,
/// so analytics can group on exact strings. Near-matches become aliases of an existing
/// canonical; only genuinely new meanings are admitted as canonicals. Vocabulary changes are
/// staged on the context and persisted by whoever saves — normally the commit stage.
/// </summary>
public sealed class FacetResolver : IFacetResolver
{
    /// <summary>
    /// Cosine similarity above which a proposed value is treated as a rewording of an existing
    /// canonical. Tuned to absorb spelling and phrasing drift ("quads" / "quadriceps") without
    /// merging distinct concepts ("squat" / "hinge"), which would silently destroy an axis.
    /// </summary>
    private const double SnapThreshold = 0.82;

    private readonly ModelContext _context;
    private readonly IEmbeddingGenerator _embeddings;

    public FacetResolver(ModelContext context, IEmbeddingGenerator embeddings)
    {
        _context = context;
        _embeddings = embeddings;
    }

    public async Task<Result<ResolvedVocabulary>> ResolveAsync(IReadOnlyList<ProposedFacet> proposed)
    {
        if (proposed.Count == 0) return Result<ResolvedVocabulary>.Success(
            new ResolvedVocabulary(new Dictionary<(FacetAxis, string), string>()));

        var vocabulary = await _context.FacetValues.ToListAsync();
        var distinct = DistinctProposals(proposed);

        var byLiteral = MatchLiterally(distinct, vocabulary);
        var unmatched = distinct.Where(p => !byLiteral.ContainsKey(Key(p))).ToList();

        var semantic = await MatchSemanticallyAsync(unmatched, vocabulary);
        if (!semantic.IsSuccess) return Result<ResolvedVocabulary>.Error(new ErrorList([.. semantic.Errors]));

        var canonicals = byLiteral
            .Concat(semantic.Value)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return Result<ResolvedVocabulary>.Success(new ResolvedVocabulary(canonicals));
    }

    /// <summary>
    /// Normalizes once at the boundary so every stage below — literal match, embedding input,
    /// and the stored canonical — sees the same spelling.
    /// </summary>
    private static List<ProposedFacet> DistinctProposals(IReadOnlyList<ProposedFacet> proposed) =>
        [.. proposed
            .Select(facet => facet with { Value = facet.Value.Trim() })
            .GroupBy(Key)
            .Select(group => group.First())];

    internal static (FacetAxis Axis, string Value) Key(ProposedFacet facet) =>
        (facet.Axis, facet.Value.Trim().ToLowerInvariant());

    private static Dictionary<(FacetAxis, string), string> MatchLiterally(
        List<ProposedFacet> proposed, List<FacetValue> vocabulary)
    {
        var matches = new Dictionary<(FacetAxis, string), string>();
        foreach (var facet in proposed)
        {
            var hit = vocabulary.FirstOrDefault(known =>
                known.Axis == facet.Axis &&
                (known.Canonical.Equals(facet.Value, StringComparison.OrdinalIgnoreCase) ||
                 known.Aliases.Any(alias => alias.Equals(facet.Value, StringComparison.OrdinalIgnoreCase))));

            if (hit is not null) matches[Key(facet)] = hit.Canonical;
        }

        return matches;
    }

    private async Task<Result<Dictionary<(FacetAxis, string), string>>> MatchSemanticallyAsync(
        List<ProposedFacet> proposed, List<FacetValue> vocabulary)
    {
        var matches = new Dictionary<(FacetAxis, string), string>();
        if (proposed.Count == 0) return Result<Dictionary<(FacetAxis, string), string>>.Success(matches);

        var vectors = await _embeddings.GenerateBatchAsync([.. proposed.Select(p => p.Value)]);
        if (!vectors.IsSuccess) return Result<Dictionary<(FacetAxis, string), string>>.Error(
            new ErrorList([.. vectors.Errors]));

        foreach (var (facet, vector) in proposed.Zip(vectors.Value))
        {
            var nearest = FindNearest(facet, vector, vocabulary);
            if (!nearest.IsSuccess) return Result<Dictionary<(FacetAxis, string), string>>.Error(
                new ErrorList([.. nearest.Errors]));

            var canonical = nearest.Value is { } match
                ? AbsorbAsAlias(match, facet.Value)
                : AdmitCanonical(facet, vector, vocabulary);

            matches[Key(facet)] = canonical;
        }

        return Result<Dictionary<(FacetAxis, string), string>>.Success(matches);
    }

    private static Result<FacetValue?> FindNearest(
        ProposedFacet facet, float[] vector, List<FacetValue> vocabulary)
    {
        FacetValue? nearest = null;
        var best = SnapThreshold;

        foreach (var candidate in vocabulary.Where(v => v.Axis == facet.Axis))
        {
            var similarity = Vectors.CosineSimilarity(vector, candidate.Embedding);
            if (!similarity.IsSuccess) return Result<FacetValue?>.Error(new ErrorList([.. similarity.Errors]));

            if (similarity.Value >= best)
            {
                best = similarity.Value;
                nearest = candidate;
            }
        }

        return Result<FacetValue?>.Success(nearest);
    }

    private string AbsorbAsAlias(FacetValue canonical, string alias)
    {
        var alreadyKnown = canonical.Aliases.Any(a => a.Equals(alias, StringComparison.OrdinalIgnoreCase))
            || canonical.Canonical.Equals(alias, StringComparison.OrdinalIgnoreCase);
        if (alreadyKnown) return canonical.Canonical;

        // Aliases is init-only on the record, so the update goes through EF's property entry
        // rather than a `with` expression — replacing the instance would break change tracking.
        _context.Entry(canonical).Property(v => v.Aliases).CurrentValue = [.. canonical.Aliases, alias];
        return canonical.Canonical;
    }

    private string AdmitCanonical(ProposedFacet facet, float[] vector, List<FacetValue> vocabulary)
    {
        var admitted = new FacetValue(facet.Axis, facet.Value, [], vector);
        _context.FacetValues.Add(admitted);

        // Visible to the rest of this pass, so two near-identical proposals in one log resolve
        // to a single new canonical instead of colliding on the composite key at save time.
        vocabulary.Add(admitted);
        return admitted.Canonical;
    }
}
