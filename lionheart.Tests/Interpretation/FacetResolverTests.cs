using lionheart.Model.Analysis;
using lionheart.Model.Interpretation;
using lionheart.Services.Interpretation;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Interpretation;

public class FacetResolverTests : IDisposable
{
    private readonly RemodelDatabase _database = new();
    private readonly FakeEmbeddingGenerator _embeddings = new();

    public void Dispose() => _database.Dispose();

    private async Task SeedVocabularyAsync(params FacetValue[] values)
    {
        using var context = _database.CreateContext();
        context.FacetValues.AddRange(values);
        await context.SaveChangesAsync();
    }

    private async Task<(IReadOnlyList<Facet> Facets, Ardalis.Result.Result<ResolvedVocabulary> Result)>
        ResolveAsync(params ProposedFacet[] proposed)
    {
        using var context = _database.CreateContext();
        var resolver = new FacetResolver(context, _embeddings);
        var result = await resolver.ResolveAsync(proposed);
        if (!result.IsSuccess) return ([], result);

        var facets = result.Value.Canonicalize(proposed);
        await context.SaveChangesAsync();
        return (facets, result);
    }

    [Fact]
    public async Task ExactCanonicalMatchesWithoutEmbedding()
    {
        await SeedVocabularyAsync(new FacetValue(FacetAxis.Modality, "lift", [], FakeEmbeddingGenerator.OneHot(1)));

        var (facets, _) = await ResolveAsync(new ProposedFacet(FacetAxis.Modality, "Lift"));

        Assert.Equal([new Facet(FacetAxis.Modality, "lift")], facets);
        Assert.Empty(_embeddings.Requested);
    }

    [Fact]
    public async Task KnownAliasResolvesToItsCanonicalWithoutEmbedding()
    {
        await SeedVocabularyAsync(
            new FacetValue(FacetAxis.Modality, "lift", ["weights"], FakeEmbeddingGenerator.OneHot(1)));

        var (facets, _) = await ResolveAsync(new ProposedFacet(FacetAxis.Modality, "weights"));

        Assert.Equal([new Facet(FacetAxis.Modality, "lift")], facets);
        Assert.Empty(_embeddings.Requested);
    }

    [Fact]
    public async Task NearMatchSnapsToCanonicalAndAccruesTheAlias()
    {
        await SeedVocabularyAsync(
            new FacetValue(FacetAxis.MuscleRegion, "quads", [], FakeEmbeddingGenerator.OneHot(3)));
        _embeddings.Returns("quadriceps", FakeEmbeddingGenerator.Between(3, 4, tilt: 0.1f));

        var (facets, _) = await ResolveAsync(new ProposedFacet(FacetAxis.MuscleRegion, "quadriceps"));

        Assert.Equal([new Facet(FacetAxis.MuscleRegion, "quads")], facets);

        using var context = _database.CreateContext();
        var stored = await context.FacetValues.SingleAsync();
        Assert.Equal("quads", stored.Canonical);
        Assert.Contains("quadriceps", stored.Aliases);
    }

    [Fact]
    public async Task UnrelatedValueIsAdmittedAsANewCanonical()
    {
        await SeedVocabularyAsync(
            new FacetValue(FacetAxis.MovementPattern, "squat", [], FakeEmbeddingGenerator.OneHot(3)));

        var (facets, _) = await ResolveAsync(new ProposedFacet(FacetAxis.MovementPattern, "hinge"));

        Assert.Equal([new Facet(FacetAxis.MovementPattern, "hinge")], facets);

        using var context = _database.CreateContext();
        var canonicals = await context.FacetValues.Select(v => v.Canonical).ToListAsync();
        Assert.Equal(["hinge", "squat"], canonicals.Order());
    }

    /// <summary>
    /// Two near-identical new values in one pass must converge on a single canonical — otherwise
    /// they collide on the (Axis, Canonical) primary key at save time.
    /// </summary>
    [Fact]
    public async Task TwoNearIdenticalNewValuesInOnePassBecomeOneCanonical()
    {
        _embeddings
            .Returns("sled push", FakeEmbeddingGenerator.OneHot(7))
            .Returns("sled pushing", FakeEmbeddingGenerator.Between(7, 8, tilt: 0.1f));

        var (facets, _) = await ResolveAsync(
            new ProposedFacet(FacetAxis.MovementPattern, "sled push"),
            new ProposedFacet(FacetAxis.MovementPattern, "sled pushing"));

        Assert.Equal([new Facet(FacetAxis.MovementPattern, "sled push")], facets);

        using var context = _database.CreateContext();
        var stored = await context.FacetValues.SingleAsync();
        Assert.Equal("sled push", stored.Canonical);
        Assert.Contains("sled pushing", stored.Aliases);
    }

    [Fact]
    public async Task SameValueOnDifferentAxesStaysDistinct()
    {
        var (facets, _) = await ResolveAsync(
            new ProposedFacet(FacetAxis.MovementPattern, "push"),
            new ProposedFacet(FacetAxis.Intent, "push"));

        Assert.Equal(
            [new Facet(FacetAxis.MovementPattern, "push"), new Facet(FacetAxis.Intent, "push")],
            facets);
    }

    [Fact]
    public async Task RepeatedProposalsCollapseToOneFacet()
    {
        var (facets, _) = await ResolveAsync(
            new ProposedFacet(FacetAxis.Modality, "run"),
            new ProposedFacet(FacetAxis.Modality, "Run"));

        Assert.Equal([new Facet(FacetAxis.Modality, "run")], facets);
    }

    /// <summary>
    /// Padding must not fork the vocabulary: the canonical spelling is the primary key and the
    /// string every analytic groups on.
    /// </summary>
    [Fact]
    public async Task SurroundingWhitespaceNeitherBypassesMatchingNorReachesStorage()
    {
        await SeedVocabularyAsync(new FacetValue(FacetAxis.Modality, "lift", [], FakeEmbeddingGenerator.OneHot(1)));

        var (facets, _) = await ResolveAsync(new ProposedFacet(FacetAxis.Modality, "  lift  "));

        Assert.Equal([new Facet(FacetAxis.Modality, "lift")], facets);
        Assert.Empty(_embeddings.Requested);

        using var context = _database.CreateContext();
        Assert.Equal("lift", (await context.FacetValues.SingleAsync()).Canonical);
    }

    [Fact]
    public async Task NewCanonicalIsStoredTrimmed()
    {
        var (facets, _) = await ResolveAsync(new ProposedFacet(FacetAxis.Intent, "  recovery "));

        Assert.Equal([new Facet(FacetAxis.Intent, "recovery")], facets);

        using var context = _database.CreateContext();
        Assert.Equal("recovery", (await context.FacetValues.SingleAsync()).Canonical);
    }

    [Fact]
    public async Task StaleVocabularyDimensionsFailLoudlyInsteadOfMisResolving()
    {
        await SeedVocabularyAsync(
            new FacetValue(FacetAxis.Modality, "lift", [], [0.1f, 0.2f]));

        var (_, result) = await ResolveAsync(new ProposedFacet(FacetAxis.Modality, "sled work"));

        Assert.False(result.IsSuccess);
        Assert.Contains("recomputed", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task EmbeddingFailurePropagatesRatherThanInventingVocabulary()
    {
        _embeddings.FailWith = "LM Studio unreachable";

        var (_, result) = await ResolveAsync(new ProposedFacet(FacetAxis.Modality, "sled work"));

        Assert.False(result.IsSuccess);

        using var context = _database.CreateContext();
        Assert.Empty(await context.FacetValues.ToListAsync());
    }
}
