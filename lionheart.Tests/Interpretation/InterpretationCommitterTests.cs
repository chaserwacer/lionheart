using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Interpretation;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Interpretation;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Interpretation;

public class InterpretationCommitterTests : IDisposable
{
    private readonly RemodelDatabase _database = new();
    private readonly FakeEmbeddingGenerator _embeddings = new();

    public void Dispose() => _database.Dispose();

    private async Task<Ardalis.Result.Result<Committed>> CommitAsync(ResolvedInterpretation interpretation)
    {
        using var context = _database.CreateContext();
        return await new InterpretationCommitter(context, _embeddings).CommitAsync(interpretation);
    }

    private static ResolvedInterpretation Only(CompletedTrainingSession session) =>
        new([session], [], []);

    private static ResolvedInterpretation Only(Context context) =>
        new([], [context], []);

    [Fact]
    public async Task SessionIsWrittenAlongsideItsAnalyzableProjection()
    {
        var session = Some.CompletedSession(_database.UserId);

        var committed = await CommitAsync(Only(session));

        Assert.True(committed.IsSuccess);
        Assert.Equal([session.TrainingSessionId], committed.Value.SessionIds);

        using var context = _database.CreateContext();
        Assert.NotNull(await context.Sessions.FindAsync(session.TrainingSessionId));

        var entry = await context.AnalyzableEntries.SingleAsync();
        Assert.Equal(session.TrainingSessionId, entry.EntryId);
        Assert.Equal(EntryKind.Session, entry.Kind);
        Assert.Equal(session.UserId, entry.UserId);
        Assert.Equal(Some.Decoration.SummaryText, entry.SummaryText);
        Assert.Equal(Some.Decoration.DecoratorVersion, entry.DecoratorVersion);
        Assert.NotEmpty(entry.Embedding);
    }

    /// <summary>
    /// The balance ledger and every other facet analytic filter on movement patterns, which only
    /// ever exist on individual sets — so the projection has to carry the union, not just the
    /// session-level decoration.
    /// </summary>
    [Fact]
    public async Task ProjectedFacetsUnionSessionAndSetFacets()
    {
        var session = Some.CompletedSession(_database.UserId);

        await CommitAsync(Only(session));

        using var context = _database.CreateContext();
        var entry = await context.AnalyzableEntries.SingleAsync();

        var setFacets = session.Sets.SelectMany(set => set.Facets).Distinct();
        Assert.All(setFacets, facet => Assert.Contains(facet, entry.Facets));
        Assert.All(Some.Decoration.Facets, facet => Assert.Contains(facet, entry.Facets));
        Assert.Equal(entry.Facets.Distinct().Count(), entry.Facets.Count);
    }

    [Fact]
    public async Task ContextIsProjectedUnderItsOwnKind()
    {
        var stateContext = Some.StateContext(_database.UserId) with
        {
            Decoration = new Decorated(Some.Decoration)
        };

        var committed = await CommitAsync(Only(stateContext));

        Assert.True(committed.IsSuccess);
        Assert.Equal([stateContext.ContextId], committed.Value.ContextIds);

        using var context = _database.CreateContext();
        var entry = await context.AnalyzableEntries.SingleAsync();
        Assert.Equal(EntryKind.Context, entry.Kind);
        Assert.Equal(stateContext.ContextId, entry.EntryId);
    }

    [Fact]
    public async Task UndecoratedEntriesAreStoredButNotProjected()
    {
        var stateContext = Some.StateContext(_database.UserId); // NotDecorated

        var committed = await CommitAsync(Only(stateContext));

        Assert.True(committed.IsSuccess);

        using var context = _database.CreateContext();
        Assert.NotNull(await context.Contexts.FindAsync(stateContext.ContextId));
        Assert.Empty(await context.AnalyzableEntries.ToListAsync());
    }

    [Fact]
    public async Task ProjectionIsKeyedByTheDomainIdSoRetrievalCanJoinBack()
    {
        var session = Some.CompletedSession(_database.UserId);
        var stateContext = Some.StateContext(_database.UserId) with
        {
            Decoration = new Decorated(Some.Decoration)
        };

        await CommitAsync(new ResolvedInterpretation([session], [stateContext], []));

        using var context = _database.CreateContext();
        var entryIds = await context.AnalyzableEntries.Select(e => e.EntryId).ToListAsync();
        Assert.Equal(
            new[] { session.TrainingSessionId, stateContext.ContextId }.Order(),
            entryIds.Order());
    }

    [Fact]
    public async Task WarningsFromEarlierStagesSurviveTheCommit()
    {
        var session = Some.CompletedSession(_database.UserId);

        var committed = await CommitAsync(new ResolvedInterpretation([session], [], ["ignored a rest-timer row"]));

        Assert.True(committed.IsSuccess);
        Assert.Equal(["ignored a rest-timer row"], committed.Value.Warnings);
    }

    [Fact]
    public async Task EntryWithoutASummaryStaysFacetQueryableWithNoVector()
    {
        var session = Some.CompletedSession(_database.UserId) with
        {
            Decoration = new Decorated(Some.Decoration with { SummaryText = string.Empty })
        };

        var committed = await CommitAsync(Only(session));

        Assert.True(committed.IsSuccess);

        using var context = _database.CreateContext();
        var entry = await context.AnalyzableEntries.SingleAsync();
        Assert.Empty(entry.Embedding);
        Assert.NotEmpty(entry.Facets);
    }

    [Fact]
    public async Task EmbeddingFailureAbortsTheWriteEntirely()
    {
        _embeddings.FailWith = "LM Studio unreachable";
        var session = Some.CompletedSession(_database.UserId);

        var committed = await CommitAsync(Only(session));

        Assert.False(committed.IsSuccess);

        using var context = _database.CreateContext();
        Assert.Empty(await context.Sessions.ToListAsync());
        Assert.Empty(await context.AnalyzableEntries.ToListAsync());
    }
}
