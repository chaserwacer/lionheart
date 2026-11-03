using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Interpretation;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Embedding;
using lionheart.Services.Training.PersonalRecords;

namespace lionheart.Services.Interpretation;

public interface IInterpretationCommitter
{
    Task<Result<Committed, Problem>> CommitAsync(ResolvedInterpretation interpretation);
}

/// <summary>
/// Stage three: resolved records are written together with the <see cref="AnalyzableEntry"/>
/// projection that retrieval reads. Both halves land in one save — an entry without its
/// projection would be invisible to every analytic.
/// <para>
/// This writes new entries only. Re-decorating an entry that already exists needs an update
/// path on both halves and does not live here yet.
/// </para>
/// </summary>
public sealed class InterpretationCommitter : IInterpretationCommitter
{
    private readonly ModelContext _context;
    private readonly IEmbeddingGenerator _embeddings;
    private readonly IPersonalRecordDetector _records;

    public InterpretationCommitter(
        ModelContext context, IEmbeddingGenerator embeddings, IPersonalRecordDetector records)
    {
        _context = context;
        _embeddings = embeddings;
        _records = records;
    }

    public Task<Result<Committed, Problem>> CommitAsync(ResolvedInterpretation interpretation) =>
        EmbedSummariesAsync(CollectProjections(interpretation))
            .BindAsync(entries => WriteAsync(interpretation, entries));

    /// <summary>
    /// A session logged as text sets the same records one logged through the form does, so the
    /// third write path that produces a completed session stages them too — the records belong to
    /// the training, not to the route it arrived on.
    /// </summary>
    private async Task<Result<Committed, Problem>> WriteAsync(
        ResolvedInterpretation interpretation, IReadOnlyList<AnalyzableEntry> entries)
    {
        foreach (var session in interpretation.Sessions)
        {
            _context.Sessions.Add(session);
            await _records.StageAsync(session, CancellationToken.None);
        }

        foreach (var context in interpretation.Contexts) _context.Contexts.Add(context);
        _context.AnalyzableEntries.AddRange(entries);

        await _context.SaveChangesAsync();

        return Result<Committed, Problem>.Success(new Committed(
            SessionIds: [.. interpretation.Sessions.Select(s => s.TrainingSessionId)],
            ContextIds: [.. interpretation.Contexts.Select(c => c.ContextId)],
            Warnings: interpretation.Warnings));
    }

    /// <summary>
    /// Session facets are the union of the session's own and every set's: the balance ledger and
    /// friends filter on movement patterns, which only ever exist at set level.
    /// </summary>
    private static IReadOnlyList<Projection> CollectProjections(ResolvedInterpretation interpretation)
    {
        var sessions =
            from session in interpretation.Sessions
            let decorated = session.Decoration as Decorated
            where decorated is not null
            select new Projection(
                session.TrainingSessionId,
                EntryKind.Session,
                session.UserId,
                session.Period,
                [.. decorated.Decoration.Facets.Concat(session.Sets.SelectMany(set => set.Facets)).Distinct()],
                decorated.Decoration);

        var contexts =
            from context in interpretation.Contexts
            let decorated = context.Decoration as Decorated
            where decorated is not null
            select new Projection(
                context.ContextId,
                EntryKind.Context,
                context.UserId,
                context.Period,
                decorated.Decoration.Facets,
                decorated.Decoration);

        return [.. sessions.Concat(contexts)];
    }

    private Task<Result<IReadOnlyList<AnalyzableEntry>, Problem>> EmbedSummariesAsync(
        IReadOnlyList<Projection> projections)
    {
        if (projections.Count == 0)
        {
            return Task.FromResult(Result<IReadOnlyList<AnalyzableEntry>, Problem>.Success([]));
        }

        // An entry with no summary still belongs in the projection — it stays facet- and
        // window-queryable, just not semantically retrievable.
        var summarized = projections.Where(p => !string.IsNullOrWhiteSpace(p.Decoration.SummaryText)).ToList();

        return _embeddings.GenerateBatchAsync([.. summarized.Select(p => p.Decoration.SummaryText)])
            .MapAsync(vectors => Project(projections, summarized, vectors));
    }

    private static IReadOnlyList<AnalyzableEntry> Project(
        IReadOnlyList<Projection> projections,
        IReadOnlyList<Projection> summarized,
        IReadOnlyList<float[]> vectors)
    {
        var byId = summarized
            .Zip(vectors)
            .ToDictionary(pair => pair.First.EntryId, pair => pair.Second);

        return
        [
            .. projections.Select(p => new AnalyzableEntry(
                EntryId: p.EntryId,
                Kind: p.Kind,
                UserId: p.UserId,
                Period: p.Period,
                Facets: p.Facets,
                Valence: p.Decoration.Valence,
                Embedding: byId.TryGetValue(p.EntryId, out var vector) ? vector : [],
                SummaryText: p.Decoration.SummaryText,
                DecoratorVersion: p.Decoration.DecoratorVersion))
        ];
    }

    private sealed record Projection(
        Guid EntryId,
        EntryKind Kind,
        Guid UserId,
        Period Period,
        IReadOnlyList<Facet> Facets,
        Decoration Decoration);
}
