using System.Text.Json;
using lionheart.Model.Analysis;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Serialization;

/// <summary>
/// The resume path is stateless: the server hands the client a draft and the client hands it
/// straight back. That makes the draft's JSON a wire contract in both directions — a shape that
/// does not survive the round trip is a confirmation the athlete can never complete.
/// </summary>
public class InterpretationWireTests
{
    /// <summary>Web defaults are what ASP.NET serializes a response body with.</summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private static ProposedDecoration Decoration =>
        new([new ProposedFacet(FacetAxis.Modality, "lift")], ["felt-strong"], Valence.Positive, "A summary.");

    private static MovementReference Movement => new("Bench Press", "Barbell", "Incline");

    private static ParsedInterpretation EveryDraftShape => new(
        [
            new DraftSession(
                [
                    new DraftLiftSet(Movement, Reps: 5, Weight: 225, WeightUnit.Pounds, Rpe.Of(8.5))
                    {
                        Source = Some.TextedSource,
                        Facets = [new ProposedFacet(FacetAxis.MovementPattern, "push")]
                    },
                    new DraftDistanceTimeSet(
                        Movement, Distance: 1600, DistanceUnit.Meters, TimeSpan.FromMinutes(8), Rpe.Unrated)
                    {
                        Source = Some.TextedSource,
                        Facets = []
                    },
                    new DraftTrailRunSet(
                        Distance: 6.2, DistanceUnit.Miles, TimeSpan.FromMinutes(64),
                        ElevationGainMeters: 410, Rpe.Of(7))
                    {
                        Source = Some.TextedSource,
                        Facets = []
                    },
                    new DraftFreeformSet { Source = Some.TextedSource, Facets = [] }
                ],
                new EffortNotReported())
            {
                Period = new Interval(Some.Clock, Some.Clock.AddMinutes(75)),
                Notes = "push day",
                Decoration = Decoration
            },
            new DraftExertionContext(new EffortNotReported())
            {
                Period = new Instant(Some.Clock),
                Notes = "pressure-washed the driveway",
                Decoration = Decoration
            },
            new DraftStateContext
            {
                Period = new Instant(Some.Clock),
                Notes = "terrible sleep",
                Decoration = Decoration
            }
        ],
        Warnings: ["Could not read the second set."]);

    [Fact]
    public void A_draft_survives_the_round_trip_the_client_echoes_it_through()
    {
        var json = JsonSerializer.Serialize(EveryDraftShape, Wire);
        var echoed = JsonSerializer.Deserialize<ParsedInterpretation>(json, Wire);

        Assert.NotNull(echoed);
        Assert.Equal(json, JsonSerializer.Serialize(echoed, Wire));
    }

    [Fact]
    public void Every_draft_shape_keeps_its_own_type_across_the_wire()
    {
        var echoed = JsonSerializer.Deserialize<ParsedInterpretation>(
            JsonSerializer.Serialize(EveryDraftShape, Wire), Wire)!;

        Assert.Equal(
            [typeof(DraftSession), typeof(DraftExertionContext), typeof(DraftStateContext)],
            echoed.Entries.Select(entry => entry.GetType()));

        var session = Assert.IsType<DraftSession>(echoed.Entries[0]);
        Assert.Equal(
            [typeof(DraftLiftSet), typeof(DraftDistanceTimeSet), typeof(DraftTrailRunSet), typeof(DraftFreeformSet)],
            session.Sets.Select(set => set.GetType()));
    }

    /// <summary>
    /// The details a confirmation actually turns on: the movement names it has to ask about, and
    /// the raw utterance an entry must never lose.
    /// </summary>
    [Fact]
    public void The_names_and_the_utterance_come_back_intact()
    {
        var echoed = JsonSerializer.Deserialize<ParsedInterpretation>(
            JsonSerializer.Serialize(EveryDraftShape, Wire), Wire)!;

        var lift = Assert.IsType<DraftLiftSet>(Assert.IsType<DraftSession>(echoed.Entries[0]).Sets[0]);

        Assert.Equal(Movement, lift.Movement);
        Assert.Equal(8.5, lift.Rpe.Value);
        Assert.Equal(Some.TextedSource.RawText, Assert.IsType<Texted>(lift.Source).RawText);
    }

    public static TheoryData<InterpretationOutcome, string> BothOutcomes() => new()
    {
        { new Committed([Guid.NewGuid()], [], []), "committed" },
        {
            new AwaitingConfirmation(
                EveryDraftShape,
                [new UnresolvedReference("Squat", ReferenceKind.MovementBase, [])]),
            "awaitingConfirmation"
        },
    };

    /// <summary>
    /// Both outcomes come back on a 200, so the discriminator is the only thing telling a client
    /// whether it was written or whether it has to ask.
    /// </summary>
    [Theory]
    [MemberData(nameof(BothOutcomes))]
    public void An_outcome_is_discriminated_by_case(InterpretationOutcome outcome, string discriminator)
    {
        var json = JsonSerializer.Serialize(outcome, Wire);

        Assert.Equal(discriminator, JsonDocument.Parse(json).RootElement.GetProperty("$type").GetString());
        Assert.IsType(
            outcome.GetType(), JsonSerializer.Deserialize<InterpretationOutcome>(json, Wire));
    }

    /// <summary>
    /// The confirmation body is the draft plus one answer per name, and a null id is the answer
    /// meaning "none of those — it is something new".
    /// </summary>
    [Fact]
    public void A_confirmation_request_carries_the_draft_and_the_answers()
    {
        var chosen = Guid.NewGuid();
        var request = new ConfirmationRequest(
            EveryDraftShape,
            [
                new ReferenceChoice("Squat", ReferenceKind.MovementBase, chosen),
                new ReferenceChoice("Sled", ReferenceKind.Equipment, null)
            ]);

        var received = JsonSerializer.Deserialize<ConfirmationRequest>(
            JsonSerializer.Serialize(request, Wire), Wire)!;

        Assert.Equal(chosen, received.Choices[0].ChosenEntityId);
        Assert.Null(received.Choices[1].ChosenEntityId);
        Assert.Equal(ReferenceKind.Equipment, received.Choices[1].Kind);
        Assert.Equal(3, received.Draft.Entries.Count);
    }
}
