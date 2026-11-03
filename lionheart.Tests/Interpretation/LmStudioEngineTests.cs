using lionheart.Data;
using lionheart.Model.Analysis;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using lionheart.Services;
using lionheart.Services.Embedding;
using lionheart.Services.Interpretation;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Interpretation;

/// <summary>
/// The whole engine against a live LM Studio and a real SQLite schema — the only check that
/// parse, resolve and commit compose correctly with real model output rather than staged fakes.
/// </summary>
[Trait("Category", "LocalModel")]
[Collection(LiveModelCollection.Name)]
public class LmStudioEngineTests : IDisposable
{
    private readonly RemodelDatabase _database = new();
    private readonly IdentityUser _user;

    public LmStudioEngineTests()
    {
        _user = new IdentityUser("engine@lionheart.local") { Id = _database.UserId.ToString() };

        using var context = _database.CreateContext();
        context.MovementBases.Add(new MovementBase
        {
            MovementBaseID = Guid.NewGuid(),
            Name = "Bench Press",
            UserID = _database.UserId,
            Description = string.Empty,
            MuscleGroups = []
        });
        context.Equipments.Add(new Equipment
        {
            EquipmentID = Guid.NewGuid(),
            Name = "Barbell",
            UserID = _database.UserId
        });
        context.SaveChanges();
    }

    public void Dispose() => _database.Dispose();

    private static InterpretationEngine BuildEngine(ModelContext context)
    {
        DotNetEnv.Env.TraversePath().Load();
        LmStudioFactAttribute.SkipUnlessAvailable();
        var embeddings = new OpenAiCompatibleEmbeddingGenerator(LmStudio.CreateEmbeddingClient());

        return new InterpretationEngine(
            context,
            new InterpretationParser(LmStudio.CreateChatModel()),
            new InterpretationResolver(
                new MovementResolver(context, new MovementDataService(context, new MovementModifierService(context))),
                new FacetResolver(context, embeddings)),
            new InterpretationCommitter(context, embeddings));
    }

    [LmStudioFact]
    public async Task KnownMovementLogRunsAllTheWayToStoredSessionAndProjection()
    {
        InterpretationOutcome outcome;
        using (var context = _database.CreateContext())
        {
            var result = await BuildEngine(context).InterpretAsync(
                _user,
                new InterpretationRequest("Bench press 225x5 and 235x5 today, barbell. Felt strong.", DateTime.UtcNow));

            outcome = ResultAssert.Succeeded(result);
        }

        var committed = Assert.IsType<Committed>(outcome);
        Assert.NotEmpty(committed.SessionIds);

        using var verify = _database.CreateContext();
        var session = await verify.Sessions.FindAsync(committed.SessionIds[0]);
        Assert.NotNull(session);

        var entry = await verify.AnalyzableEntries.SingleAsync();
        Assert.Equal(committed.SessionIds[0], entry.EntryId);
        Assert.Equal(EntryKind.Session, entry.Kind);
        Assert.NotEmpty(entry.SummaryText);
        Assert.NotEmpty(entry.Embedding);
        Assert.NotEmpty(entry.Facets);

        // The vocabulary is grown by the run itself, and every projected facet must be canonical.
        var canonicals = await verify.FacetValues.ToListAsync();
        Assert.NotEmpty(canonicals);
        Assert.All(entry.Facets, facet =>
            Assert.Contains(canonicals, v => v.Axis == facet.Axis && v.Canonical == facet.Value));
    }

    [LmStudioFact]
    public async Task UnknownMovementComesBackForConfirmationWithNothingWritten()
    {
        using (var context = _database.CreateContext())
        {
            var result = await BuildEngine(context).InterpretAsync(
                _user,
                new InterpretationRequest("Did 4 sets of kettlebell windmills at 35lb.", DateTime.UtcNow));

            var outcome = ResultAssert.Succeeded(result);
            var awaiting = Assert.IsType<AwaitingConfirmation>(outcome);
            Assert.NotEmpty(awaiting.References);
            Assert.NotEmpty(awaiting.Draft.Entries);
        }

        using var verify = _database.CreateContext();
        Assert.Empty(await verify.Sessions.ToListAsync());
        Assert.Empty(await verify.AnalyzableEntries.ToListAsync());
        Assert.Empty(await verify.MovementDatas.ToListAsync());
        Assert.Empty(await verify.FacetValues.ToListAsync());
    }
}
