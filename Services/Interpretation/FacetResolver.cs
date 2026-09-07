using lionheart.Functional;
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
    Task<Result<ResolvedVocabulary, Problem>> ResolveAsync(IReadOnlyList<ProposedFacet> proposed);
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

    public async Task<Result<ResolvedVocabulary, Problem>> ResolveAsync(IReadOnlyList<ProposedFacet> proposed)
    {
        if (proposed.Count == 0) return Result<ResolvedVocabulary, Problem>.Success(
            new ResolvedVocabulary(new Dictionary<(FacetAxis, string), string>()));

        var vocabulary = await _context.FacetValues.ToListAsync();
        var distinct = DistinctProposals(proposed);

        var byLiteral = MatchLiterally(distinct, vocabulary);
        var unmatched = distinct.Where(p => !byLiteral.ContainsKey(Key(p))).ToList();

        return (await MatchSemanticallyAsync(unmatched, vocabulary))
            .Map(semantic => new ResolvedVocabulary(
                byLiteral.Concat(semantic).ToDictionary(pair => pair.Key, pair => pair.Value)));
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

    private Task<Result<Dictionary<(FacetAxis, string), string>, Problem>> MatchSemanticallyAsync(
        List<ProposedFacet> proposed, List<FacetValue> vocabulary) =>
        proposed.Count == 0
            ? Task.FromResult(Result<Dictionary<(FacetAxis, string), string>, Problem>.Success([]))
            : _embeddings.GenerateBatchAsync([.. proposed.Select(p => p.Value)])
                .BindAsync(vectors => SnapToVocabulary(proposed, vectors, vocabulary));

    /// <summary>
    /// Walks the proposals in order, because admitting a canonical makes it visible to the ones
    /// that follow. Fail-fast: a vocabulary the embedder cannot score must not be half-updated.
    /// </summary>
    private Result<Dictionary<(FacetAxis, string), string>, Problem> SnapToVocabulary(
        List<ProposedFacet> proposed, IReadOnlyList<float[]> vectors, List<FacetValue> vocabulary) =>
        proposed.Zip(vectors)
            .Traverse(pair => FindNearest(pair.First, pair.Second, vocabulary)
                .Map(nearest => KeyValuePair.Create(
                    Key(pair.First),
                    nearest is { } match
                        ? AbsorbAsAlias(match, pair.First.Value)
                        : AdmitCanonical(pair.First, pair.Second, vocabulary))))
            .Map(canonicals => canonicals.ToDictionary());

    private static Result<FacetValue?, Problem> FindNearest(
        ProposedFacet facet, float[] vector, List<FacetValue> vocabulary) =>
        vocabulary
            .Where(candidate => candidate.Axis == facet.Axis)
            .Traverse(candidate => Vectors.CosineSimilarity(vector, candidate.Embedding)
                .Map(similarity => (Candidate: candidate, Similarity: similarity)))
            .Map(Closest);

    /// <summary>
    /// The last candidate at least as close as every candidate before it, or none if nothing
    /// reaches <see cref="SnapThreshold"/>.
    /// </summary>
    private static FacetValue? Closest(IReadOnlyList<(FacetValue Candidate, double Similarity)> scored)
    {
        FacetValue? nearest = null;
        var best = SnapThreshold;

        foreach (var (candidate, similarity) in scored)
        {
            if (similarity < best) continue;
            best = similarity;
            nearest = candidate;
        }

        return nearest;
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
