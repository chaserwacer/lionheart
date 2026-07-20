using System.Text.Json;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Llm;

/// <summary>
/// Where model output meets the domain: whatever shape the engine has, a proposal must become
/// domain types, and these pin what the domain will and will not accept from a model. They sit
/// beside — not on top of — lionheart.Tests/Interpretation/, which covers the engine's stages
/// themselves; these cover the boundary the engine has to write through.
/// </summary>
public class InterpretationContractTests
{
    private const string SkipUntilReRunnable =
        "Engine re-runnability (re-decoration / freeform upgrade) not implemented — see docs/data-remodel.md step 3.";

    /// <summary>The canonical terse log from the design doc's motivation section.</summary>
    private const string TerseLog = "sled pull+push worked up to 235, 2 or 3 sets";

    // ---- Runnable today: what the domain accepts from a model's proposal ----

    [Fact]
    public void A_well_formed_model_proposal_becomes_a_set_with_a_texted_source()
    {
        const string modelOutput = """
            {"$type":"lift","MovementDataId":"11111111-2222-3333-4444-555555555555","Reps":3,"Weight":235,
             "Unit":1,"Rpe":8,"Source":{"$type":"texted","RawText":"worked up to 235","StructureConfidence":0.55},
             "Facets":[{"Axis":0,"Value":"lift"},{"Axis":1,"Value":"push"}]}
            """;

        var set = Assert.IsType<LiftSet>(JsonSerializer.Deserialize<SetEntry>(modelOutput));

        var source = Assert.IsType<Texted>(set.Source);
        Assert.Equal("worked up to 235", source.RawText);
        Assert.InRange(source.StructureConfidence, 0, 1);
        Assert.All(set.Facets, facet => Assert.True(Enum.IsDefined(facet.Axis)));
    }

    /// <summary>
    /// "2 or 3 sets" is the doc's example of ambiguity that must log rather than block. Low
    /// confidence is a legal value, not an error.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.15)]
    [InlineData(1)]
    public void Low_confidence_output_is_accepted_rather_than_rejected(double confidence)
    {
        var utterance = new Texted(TerseLog, confidence);

        Assert.Equal(confidence, utterance.StructureConfidence);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(-0.2)]
    public void Confidence_outside_zero_to_one_is_refused(double confidence) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Texted(TerseLog, confidence));

    [Fact]
    public void A_model_that_cannot_lift_structure_still_produces_a_valid_freeform_set()
    {
        const string modelOutput = """
            {"$type":"freeform","Source":{"$type":"texted","RawText":"sled stuff, felt heavy","StructureConfidence":0.1},
             "Facets":[{"Axis":0,"Value":"sled"}]}
            """;

        var set = Assert.IsType<FreeformSet>(JsonSerializer.Deserialize<SetEntry>(modelOutput));

        Assert.Equal("sled stuff, felt heavy", Assert.IsType<Texted>(set.Source).RawText);
    }

    [Fact]
    public void Invalid_json_from_the_model_fails_loudly_rather_than_yielding_a_partial_set()
    {
        const string modelOutput = """{"$type":"lift","Reps":3,"Weight":  I think about 235?}""";

        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<SetEntry>(modelOutput));
    }

    [Fact]
    public void Prose_wrapped_around_json_is_not_silently_accepted()
    {
        const string modelOutput = """
            Sure! Here is the structured version:
            {"$type":"freeform","Source":{"$type":"formed"},"Facets":[]}
            """;

        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<SetEntry>(modelOutput));
    }

    /// <summary>
    /// The axes are closed precisely so they can be the MCP tool's enum params. A model
    /// inventing "Nutrition" must not be able to widen the query surface by writing a row.
    /// </summary>
    [Fact]
    public void A_hallucinated_facet_axis_ordinal_is_refused()
    {
        Assert.ThrowsAny<Exception>(() =>
            JsonSerializer.Deserialize<Facet>("""{"Axis":42,"Value":"protein-intake"}"""));
    }

    [Fact]
    public void A_hallucinated_facet_axis_name_is_refused()
    {
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<Facet>("""{"Axis":"Nutrition","Value":"protein-intake"}"""));
    }

    [Fact]
    public void Every_facet_axis_the_model_may_be_offered_is_one_the_domain_accepts()
    {
        foreach (var axis in Enum.GetValues<FacetAxis>())
        {
            var facet = new Facet(axis, "some-value");

            Assert.Equal(axis, JsonSerializer.Deserialize<Facet>(JsonSerializer.Serialize(facet))!.Axis);
        }
    }

    [Fact]
    public void A_hallucinated_valence_is_refused()
    {
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<Decoration>(
            """{"Facets":[],"FreeTags":[],"Valence":9,"SummaryText":"x","DecoratorVersion":1}"""));
    }

    [Fact]
    public void A_proposed_decoration_carries_the_decorator_version_that_produced_it()
    {
        var decoration = Some.Decoration with { DecoratorVersion = 7 };

        Assert.Equal(7, decoration.DecoratorVersion);
    }

    // ---- Specification for engine work that has not landed ----
    //
    // The Parse, Resolve and Commit stages themselves are built and covered by
    // lionheart.Tests/Interpretation/ (fakes) and the [LmStudioFact] tests there (live). What
    // remains below is the re-runnability half of the doc's "versioned and re-runnable" promise,
    // which has no code yet.

    [Fact(Skip = SkipUntilReRunnable)]
    public void Re_running_the_engine_at_a_newer_decorator_version_re_decorates_in_place()
    {
        // Expects backfill semantics: WHERE DecoratorVersion < current re-decorates the entity
        // and its AnalyzableEntry projection, leaving the raw utterance untouched. Note that
        // Commit is deliberately create-only today, so this needs an update path on both sides
        // before it can pass.
        Assert.Fail("Specification only.");
    }

    [Fact(Skip = SkipUntilReRunnable)]
    public void Upgrading_a_freeform_set_to_a_lift_set_keeps_the_original_utterance()
    {
        // The doc's central promise: structuring is additive enrichment, the words are never lost.
        Assert.Fail("Specification only.");
    }

    [Fact(Skip = SkipUntilReRunnable)]
    public void Resolve_reports_an_out_of_enum_facet_axis_rather_than_throwing()
    {
        // The runnable tests above prove the domain refuses the value. This pins the surrounding
        // behaviour: a model proposing an axis outside FacetAxis should surface as a warning on
        // the interpretation outcome, not an exception escaping the ingest pipeline.
        Assert.Fail("Specification only.");
    }
}
