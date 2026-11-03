using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Interpretation;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.Interpretation;

public class InterpretationResolverTests : IDisposable
{
    private readonly RemodelDatabase _database = new();
    private readonly FakeEmbeddingGenerator _embeddings = new();
    private readonly IdentityUser _user;

    public InterpretationResolverTests()
    {
        _user = new IdentityUser("resolver@lionheart.local") { Id = _database.UserId.ToString() };
        using var context = _database.CreateContext();
        context.MovementBases.Add(new MovementBase
        {
            MovementBaseID = Guid.NewGuid(),
            Name = "Bench Press",
            UserID = _database.UserId,
            Description = string.Empty
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

    private async Task<ResolutionOutcome> ResolveAsync(ParsedInterpretation parsed)
    {
        using var context = _database.CreateContext();
        var resolver = new InterpretationResolver(
            new MovementResolver(context, new MovementDataService(context, new MovementModifierService(context))),
            new FacetResolver(context, _embeddings));

        return ResultAssert.Succeeded(await resolver.ResolveAsync(_user, parsed));
    }

    private static ProposedDecoration Decoration(params ProposedFacet[] facets) =>
        new(facets, FreeTags: ["felt-strong"], Valence.Positive, SummaryText: "A summary to embed.");

    private static DraftSession Session(IReadOnlyList<DraftSet> sets) =>
        new(sets, new EffortNotReported())
        {
            Period = new Interval(Some.Clock, Some.Clock.AddMinutes(60)),
            Notes = "push day",
            Decoration = Decoration(new ProposedFacet(FacetAxis.Modality, "lifting"))
        };

    private static DraftLiftSet LiftSet(string baseName = "Bench Press", string equipment = "Barbell") =>
        new(new MovementReference(baseName, equipment, string.Empty), Reps: 5, Weight: 225, WeightUnit.Pounds, Rpe.Of(8))
        {
            Source = new Texted("bench 225x5", 0.9),
            Facets = [new ProposedFacet(FacetAxis.MovementPattern, "push")]
        };

    [Fact]
    public async Task DraftSessionBecomesACompletedSessionStampedAsAiAssisted()
    {
        var outcome = await ResolveAsync(new ParsedInterpretation([Session([LiftSet()])], []));

        var resolved = Assert.IsType<Resolved>(outcome);
        var session = Assert.Single(resolved.Interpretation.Sessions);

        Assert.Equal(_database.UserId, session.UserId);
        Assert.Equal("push day", session.Notes);
        Assert.Equal(Provenance.AiAssisted, session.Provenance);
        Assert.IsType<Standalone>(session.Membership);
        Assert.NotEqual(Guid.Empty, session.TrainingSessionId);
    }

    [Fact]
    public async Task DecorationIsStampedWithTheCurrentDecoratorVersion()
    {
        var outcome = await ResolveAsync(new ParsedInterpretation([Session([LiftSet()])], []));

        var session = Assert.Single(Assert.IsType<Resolved>(outcome).Interpretation.Sessions);
        var decorated = Assert.IsType<Decorated>(session.Decoration);
        Assert.Equal(DecoratorVersion.Current, decorated.Decoration.DecoratorVersion);
        Assert.Equal(Valence.Positive, decorated.Decoration.Valence);
        Assert.Equal(["felt-strong"], decorated.Decoration.FreeTags);
    }

    [Fact]
    public async Task LiftSetKeepsItsUtteranceAndGainsAResolvedMovementId()
    {
        var outcome = await ResolveAsync(new ParsedInterpretation([Session([LiftSet()])], []));

        var session = Assert.Single(Assert.IsType<Resolved>(outcome).Interpretation.Sessions);
        var lift = Assert.IsType<LiftSet>(Assert.Single(session.Sets));

        Assert.NotEqual(Guid.Empty, lift.MovementDataId);
        Assert.Equal(5, lift.Reps);
        Assert.Equal(225, lift.Weight);
        Assert.True(lift.Rpe.IsRated);
        var source = Assert.IsType<Texted>(lift.Source);
        Assert.Equal("bench 225x5", source.RawText);
    }

    [Fact]
    public async Task FreeformSetSurvivesWithoutNeedingAMovement()
    {
        var freeform = new DraftFreeformSet
        {
            Source = new Texted("sled pull+push worked up to 235, 2 or 3 sets", 0.4),
            Facets = [new ProposedFacet(FacetAxis.Modality, "sled")]
        };

        var outcome = await ResolveAsync(new ParsedInterpretation([Session([freeform])], []));

        var session = Assert.Single(Assert.IsType<Resolved>(outcome).Interpretation.Sessions);
        var set = Assert.IsType<FreeformSet>(Assert.Single(session.Sets));
        Assert.Equal(0.4, Assert.IsType<Texted>(set.Source).StructureConfidence);
        Assert.Equal([new Facet(FacetAxis.Modality, "sled")], set.Facets);
    }

    [Fact]
    public async Task UnknownMovementStopsTheRunBeforeAnythingIsBuilt()
    {
        var outcome = await ResolveAsync(
            new ParsedInterpretation([Session([LiftSet(baseName: "Sled Pull")])], []));

        var ambiguous = Assert.IsType<Ambiguous>(outcome);
        var reference = Assert.Single(ambiguous.References);
        Assert.Equal("Sled Pull", reference.RawName);
        Assert.Equal(ReferenceKind.MovementBase, reference.Kind);
    }

    [Fact]
    public async Task ExertionContextCarriesEffortWhileStateContextDoesNot()
    {
        var exertion = new DraftExertionContext(new EffortNotReported())
        {
            Period = new Interval(Some.Clock, Some.Clock.AddHours(3)),
            Notes = "pressure-washed the driveway",
            Decoration = Decoration(new ProposedFacet(FacetAxis.LifeDomain, "chores"))
        };
        var state = new DraftStateContext
        {
            Period = new Instant(Some.Clock),
            Notes = "terrible sleep",
            Decoration = Decoration(new ProposedFacet(FacetAxis.BodyState, "under-slept"))
        };

        var outcome = await ResolveAsync(new ParsedInterpretation([exertion, state], []));

        var contexts = Assert.IsType<Resolved>(outcome).Interpretation.Contexts;
        Assert.Equal(2, contexts.Count);
        Assert.Contains(contexts, c => c is ExertionContext);
        Assert.Contains(contexts, c => c is StateContext);
        Assert.All(contexts, c => Assert.Equal(_database.UserId, c.UserId));
        Assert.All(contexts, c => Assert.IsType<Decorated>(c.Decoration));
    }

    /// <summary>
    /// Entry-level and set-level facets resolve against one shared vocabulary pass, so the whole
    /// interpretation costs a single embedding round-trip rather than one per set.
    /// </summary>
    [Fact]
    public async Task WholeInterpretationResolvesFacetsInOneEmbeddingPass()
    {
        await ResolveAsync(new ParsedInterpretation([Session([LiftSet(), LiftSet()])], []));

        Assert.Equal(_embeddings.Requested.Distinct().Count(), _embeddings.Requested.Count);
    }

    [Fact]
    public async Task WarningsFromParseArePreserved()
    {
        var outcome = await ResolveAsync(
            new ParsedInterpretation([Session([LiftSet()])], ["ignored a rest-timer row"]));

        Assert.Equal(
            ["ignored a rest-timer row"],
            Assert.IsType<Resolved>(outcome).Interpretation.Warnings);
    }
}
