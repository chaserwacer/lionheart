using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using lionheart.Model.Training.Sets;
using lionheart.Services;
using lionheart.Services.Interpretation;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Interpretation;

/// <summary>
/// The resume path. A run that could not settle a name hands the draft back and the client
/// returns it with an answer per name; the server re-resolves from there. The whole point is
/// that the parse — and the model call it costs — happens once, so the parser here refuses to
/// be called twice and the tests assert on that as much as on the outcome.
/// </summary>
public class InterpretationConfirmationTests : IDisposable
{
    private readonly RemodelDatabase _database = new();
    private readonly FakeEmbeddingGenerator _embeddings = new();
    private readonly IdentityUser _user;

    private Guid _frontSquatId;
    private Guid _barbellId;

    public InterpretationConfirmationTests()
    {
        _user = new IdentityUser("athlete@lionheart.local") { Id = _database.UserId.ToString() };

        using var context = _database.CreateContext();
        _frontSquatId = Guid.NewGuid();
        _barbellId = Guid.NewGuid();
        context.MovementBases.Add(new MovementBase
        {
            MovementBaseID = _frontSquatId,
            Name = "Front Squat",
            UserID = _database.UserId,
            Description = string.Empty
        });
        context.Equipments.Add(new Equipment
        {
            EquipmentID = _barbellId,
            Name = "Barbell",
            UserID = _database.UserId
        });
        context.SaveChanges();
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task AnUnknownNameComesBackWithTheDraftAndItsCandidates()
    {
        var draft = Draft("Squat");
        var parser = new StubParser(draft);

        var outcome = await InterpretAsync(parser, draft);

        var awaiting = Assert.IsType<AwaitingConfirmation>(outcome);
        var unresolved = Assert.Single(awaiting.References);
        Assert.Equal("Squat", unresolved.RawName);
        Assert.Equal(ReferenceKind.MovementBase, unresolved.Kind);
        Assert.Equal(_frontSquatId, Assert.Single(unresolved.Candidates).EntityId);
        Assert.Same(draft, awaiting.Draft);
    }

    [Fact]
    public async Task ChoosingACandidateCommitsWithoutParsingAgain()
    {
        var parser = new StubParser(Draft("Squat"));

        var outcome = await ConfirmAsync(
            parser,
            Draft("Squat"),
            new ReferenceChoice("Squat", ReferenceKind.MovementBase, _frontSquatId));

        var committed = Assert.IsType<Committed>(outcome);
        Assert.Single(committed.SessionIds);
        Assert.Equal(0, parser.Calls);
    }

    /// <summary>The chosen name is not the entity's name, so the set must land on what was picked.</summary>
    [Fact]
    public async Task TheChosenEntityIsWhatTheSetIsLoggedAgainst()
    {
        await ConfirmAsync(
            new StubParser(Draft("Squat")),
            Draft("Squat"),
            new ReferenceChoice("Squat", ReferenceKind.MovementBase, _frontSquatId));

        using var context = _database.CreateContext();
        var movementData = Assert.Single(await context.MovementDatas.ToListAsync());
        Assert.Equal(_frontSquatId, movementData.MovementBaseID);
        Assert.Equal(_barbellId, movementData.EquipmentID);
    }

    [Fact]
    public async Task AChoiceWithNoIdCreatesTheEntityUnderTheNameAsWritten()
    {
        var outcome = await ConfirmAsync(
            new StubParser(Draft("Sled Pull")),
            Draft("Sled Pull"),
            new ReferenceChoice("Sled Pull", ReferenceKind.MovementBase, null));

        Assert.IsType<Committed>(outcome);

        using var context = _database.CreateContext();
        var created = Assert.Single(await context.MovementBases.Where(mb => mb.Name == "Sled Pull").ToListAsync());
        Assert.Equal(_database.UserId, created.UserID);
    }

    /// <summary>One answer settles the name however many sets used it.</summary>
    [Fact]
    public async Task OneAnswerCoversEveryRepeatOfTheName()
    {
        var draft = Draft("sled pull", "SLED PULL", "  Sled Pull ");

        var outcome = await ConfirmAsync(
            new StubParser(draft),
            draft,
            new ReferenceChoice("Sled Pull", ReferenceKind.MovementBase, null));

        Assert.IsType<Committed>(outcome);

        using var context = _database.CreateContext();
        Assert.Single(await context.MovementBases.Where(mb => mb.UserID == _database.UserId && mb.Name != "Front Squat").ToListAsync());
    }

    /// <summary>
    /// A partly answered confirmation is not a refusal — it is another round. Nothing may be
    /// created for the names that were answered, because the athlete can still abandon the run.
    /// </summary>
    [Fact]
    public async Task AnUnansweredNameAsksAgainAndCreatesNothing()
    {
        var draft = Draft("Sled Pull", "Sled Push");

        var outcome = await ConfirmAsync(
            new StubParser(draft),
            draft,
            new ReferenceChoice("Sled Pull", ReferenceKind.MovementBase, null));

        var awaiting = Assert.IsType<AwaitingConfirmation>(outcome);
        Assert.Equal("Sled Push", Assert.Single(awaiting.References).RawName);

        using var context = _database.CreateContext();
        Assert.Empty(await context.MovementDatas.ToListAsync());
        Assert.Empty(await context.Sessions.ToListAsync());
        Assert.DoesNotContain(await context.MovementBases.ToListAsync(), mb => mb.Name == "Sled Pull");
    }

    /// <summary>
    /// An id belonging to somebody else is answered as absent: saying it is forbidden would
    /// disclose that it exists.
    /// </summary>
    [Fact]
    public async Task ChoosingAnEntityTheAthleteDoesNotOwnIsNotFound()
    {
        var draft = Draft("Squat");

        var refused = await ConfirmRefusalAsync(
            new StubParser(draft),
            draft,
            new ReferenceChoice("Squat", ReferenceKind.MovementBase, Guid.NewGuid()));

        Assert.IsType<Problem.NotFound>(refused);

        using var context = _database.CreateContext();
        Assert.Empty(await context.MovementDatas.ToListAsync());
    }

    [Fact]
    public async Task ConfirmingADraftWithNothingInItIsRefusedAsInvalid()
    {
        var refused = await ConfirmRefusalAsync(
            new StubParser(Draft("Squat")),
            new ParsedInterpretation([], []));

        Assert.IsType<Problem.Invalid>(refused);
    }

    [Fact]
    public async Task AFullyMatchedLogIsParsedOnceAndCommitted()
    {
        var draft = Draft("Front Squat");
        var parser = new StubParser(draft);

        Assert.IsType<Committed>(await InterpretAsync(parser, draft));
        Assert.Equal(1, parser.Calls);
    }

    private async Task<InterpretationOutcome> InterpretAsync(StubParser parser, ParsedInterpretation draft)
    {
        using var context = _database.CreateContext();
        return ResultAssert.Succeeded(await BuildEngine(context, parser).InterpretAsync(
            _user, new InterpretationRequest("front squat 225x5", Some.Clock)));
    }

    private async Task<InterpretationOutcome> ConfirmAsync(
        StubParser parser, ParsedInterpretation draft, params ReferenceChoice[] choices)
    {
        using var context = _database.CreateContext();
        return ResultAssert.Succeeded(await BuildEngine(context, parser)
            .ConfirmAsync(_user, new ConfirmationRequest(draft, choices)));
    }

    private async Task<Problem> ConfirmRefusalAsync(
        StubParser parser, ParsedInterpretation draft, params ReferenceChoice[] choices)
    {
        using var context = _database.CreateContext();
        return ResultAssert.Failed(await BuildEngine(context, parser)
            .ConfirmAsync(_user, new ConfirmationRequest(draft, choices)));
    }

    private InterpretationEngine BuildEngine(ModelContext context, IInterpretationParser parser) =>
        new(
            context,
            parser,
            new InterpretationResolver(
                new MovementResolver(context, new MovementDataService(context, new MovementModifierService(context))),
                new FacetResolver(context, _embeddings)),
            new InterpretationCommitter(context, _embeddings, new PersonalRecordDetector(context, TimeProvider.System)));

    private static ParsedInterpretation Draft(params string[] movementNames) => new(
        [
            new DraftSession(
                [.. movementNames.Select(name => new DraftLiftSet(
                    new MovementReference(name, "Barbell", string.Empty),
                    Reps: 5, Weight: 225, WeightUnit.Pounds, Rpe.Of(8))
                {
                    Source = new Texted($"{name} 225x5", 0.9),
                    Facets = []
                })],
                new EffortNotReported())
            {
                Period = new Interval(Some.Clock, Some.Clock.AddMinutes(60)),
                Notes = "leg day",
                Decoration = new ProposedDecoration(
                    [new ProposedFacet(FacetAxis.Modality, "lifting")],
                    FreeTags: [],
                    Valence.Neutral,
                    SummaryText: "A leg session.")
            }
        ],
        Warnings: []);

    /// <summary>
    /// Stands in for the model call. It counts its invocations because "the parse is not paid
    /// for twice" is the property the resume path exists for.
    /// </summary>
    private sealed class StubParser : IInterpretationParser
    {
        private readonly ParsedInterpretation _draft;

        public StubParser(ParsedInterpretation draft) => _draft = draft;

        public int Calls { get; private set; }

        public Task<Result<ParsedInterpretation, Problem>> ParseAsync(
            InterpretationRequest request, string knownMovementContext)
        {
            Calls++;
            return Task.FromResult(Result<ParsedInterpretation, Problem>.Success(_draft));
        }
    }
}
