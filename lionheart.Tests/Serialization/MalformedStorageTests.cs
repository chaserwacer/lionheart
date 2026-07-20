using System.Text.Json;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.Sets;

namespace lionheart.Tests.Serialization;

/// <summary>
/// Storage is schema-flexible TEXT, so anything can end up in a JSON column: a row written by
/// an older discriminator set, a half-flushed write, a hand-edit. The contract is fail-loud —
/// a malformed payload must throw, never rehydrate a half-built domain object that then flows
/// into analytics as if it were real.
/// </summary>
public class MalformedStorageTests
{
    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json)!;

    [Fact]
    public void Unknown_set_discriminator_fails_loudly()
    {
        Assert.ThrowsAny<JsonException>(() =>
            Read<SetEntry>("""{"$type":"swimSet","Distance":100,"Source":{"$type":"formed"},"Facets":[]}"""));
    }

    [Fact]
    public void Unknown_period_discriminator_fails_loudly()
    {
        Assert.ThrowsAny<JsonException>(() => Read<Period>("""{"$type":"recurring","At":"2026-07-18T09:30:00Z"}"""));
    }

    [Fact]
    public void Missing_discriminator_fails_loudly()
    {
        Assert.Throws<NotSupportedException>(() => Read<Period>("""{"At":"2026-07-18T09:30:00Z"}"""));
    }

    [Fact]
    public void Set_missing_its_required_source_fails_loudly()
    {
        Assert.ThrowsAny<JsonException>(() =>
            Read<SetEntry>("""{"$type":"freeform","Facets":[]}"""));
    }

    [Fact]
    public void Set_missing_its_required_facets_fails_loudly()
    {
        Assert.ThrowsAny<JsonException>(() =>
            Read<SetEntry>("""{"$type":"freeform","Source":{"$type":"formed"}}"""));
    }

    [Fact]
    public void Truncated_payload_fails_loudly()
    {
        Assert.ThrowsAny<JsonException>(() =>
            Read<SetEntry>("""{"$type":"lift","Reps":5,"Weight":225,"Source":{"$type":"for"""));
    }

    [Fact]
    public void Interval_with_end_before_start_fails_loudly()
    {
        Assert.ThrowsAny<Exception>(() =>
            Read<Period>("""{"$type":"interval","Start":"2026-07-18T10:00:00Z","End":"2026-07-18T09:00:00Z"}"""));
    }

    [Fact]
    public void Texted_confidence_outside_zero_to_one_fails_loudly()
    {
        Assert.ThrowsAny<Exception>(() =>
            Read<Utterance>("""{"$type":"texted","RawText":"anything","StructureConfidence":4.2}"""));
    }

    [Fact]
    public void Rpe_outside_zero_to_ten_fails_loudly()
    {
        Assert.ThrowsAny<Exception>(() =>
            Read<SetEntry>("""{"$type":"lift","MovementDataId":"11111111-2222-3333-4444-555555555555","Reps":5,"Weight":225,"Unit":1,"Rpe":42,"Source":{"$type":"formed"},"Facets":[]}"""));
    }

    /// <summary>
    /// FacetAxis is the closed query surface the design promises analytics and the MCP tool
    /// enum params can rely on. An ordinal outside the enum must not become a live facet.
    /// </summary>
    [Fact]
    public void Facet_axis_outside_the_closed_enum_fails_loudly()
    {
        Assert.ThrowsAny<Exception>(() => Read<Facet>("""{"Axis":99,"Value":"nutrition"}"""));
    }

    [Fact]
    public void Facet_axis_outside_the_closed_enum_fails_loudly_inside_a_set()
    {
        Assert.ThrowsAny<Exception>(() =>
            Read<SetEntry>("""{"$type":"freeform","Source":{"$type":"formed"},"Facets":[{"Axis":-1,"Value":"nutrition"}]}"""));
    }

    [Fact]
    public void Decoration_valence_outside_the_closed_enum_fails_loudly()
    {
        Assert.ThrowsAny<Exception>(() =>
            Read<DecorationState>("""{"$type":"decorated","Decoration":{"Facets":[],"FreeTags":[],"Valence":7,"SummaryText":"x","DecoratorVersion":1}}"""));
    }

    [Fact]
    public void Effort_state_with_an_unknown_discriminator_fails_loudly()
    {
        Assert.ThrowsAny<JsonException>(() => Read<EffortState>("""{"$type":"partial","Ratings":{}}"""));
    }

    /// <summary>
    /// A null in a non-nullable member is the quietest corruption available: nothing throws,
    /// and the null travels into the domain behind a non-nullable reference type.
    /// </summary>
    [Fact]
    public void Null_raw_text_does_not_smuggle_a_null_into_a_non_nullable_member()
    {
        Assert.ThrowsAny<Exception>(() =>
            Read<Utterance>("""{"$type":"texted","RawText":null,"StructureConfidence":0.5}"""));
    }

    [Fact]
    public void Null_facet_value_does_not_smuggle_a_null_into_a_non_nullable_member()
    {
        Assert.ThrowsAny<Exception>(() => Read<Facet>("""{"Axis":0,"Value":null}"""));
    }

    [Theory]
    [InlineData("""{"Facets":null,"FreeTags":[],"Valence":0,"SummaryText":"x","DecoratorVersion":1}""")]
    [InlineData("""{"Facets":[],"FreeTags":null,"Valence":0,"SummaryText":"x","DecoratorVersion":1}""")]
    [InlineData("""{"Facets":[],"FreeTags":[],"Valence":0,"SummaryText":null,"DecoratorVersion":1}""")]
    public void A_null_list_or_text_in_a_decoration_fails_loudly(string json)
    {
        Assert.ThrowsAny<Exception>(() => Read<Decoration>(json));
    }

    [Fact]
    public void A_missing_list_in_a_decoration_fails_loudly()
    {
        Assert.ThrowsAny<Exception>(() =>
            Read<Decoration>("""{"FreeTags":[],"Valence":0,"SummaryText":"x","DecoratorVersion":1}"""));
    }

    /// <summary>
    /// FLAGGED DEFECT — characterizes current behavior, not desired behavior. A null element in
    /// a stored sets list rehydrates as a null hole in <c>CompletedTrainingSession.Sets</c>, so
    /// the first analytics pass that touches <c>set.Source</c> throws far from the corrupt row.
    /// Fixing it means deciding which of the domain's ~8 list members get element guards and
    /// whether a bad element rejects the row or is dropped — a call for the owner, not this audit.
    /// Flip this test to an assertion of failure once that decision lands.
    /// </summary>
    [Fact]
    public void Null_element_inside_a_set_list_currently_survives_as_a_hole()
    {
        var sets = Read<IReadOnlyList<SetEntry>>(
            """[{"$type":"freeform","Source":{"$type":"formed"},"Facets":[]},null]""");

        Assert.Equal(2, sets.Count);
        Assert.Null(sets[1]);
    }
}
