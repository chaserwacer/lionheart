using System.Text.Json;
using lionheart.Data;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Tests.Serialization;

/// <summary>
/// Hand-written expectations for the bytes that actually land in the JSON TEXT columns.
/// The round-trip tests use the serializer as its own oracle, so a serializer-level change
/// (a renamed member, a derived value leaking in, an enum switching to strings) round-trips
/// happily while orphaning every stored row. These literals are the independent oracle:
/// they fail on any change to the on-disk shape, which is exactly when a migration is owed.
/// Member order is part of what they pin: redeclaring only some positional members of a
/// record reorders its stored payload, which no round-trip test notices.
/// </summary>
public class StorageFormatTests
{
    private static readonly Guid MovementDataId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTime Start = new(2026, 7, 18, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// Serializes through the persistence layer's own options, not the ambient default. Using
    /// the default here would mean these literals described bytes no column ever holds.
    /// </summary>
    private static string Store<T>(T value) => JsonSerializer.Serialize(value, ValueConversions.StorageJson);

    [Fact]
    public void Lift_set_stores_exactly()
    {
        var lift = new LiftSet(MovementDataId, Reps: 5, Weight: 225, WeightUnit.Pounds, Rpe.Of(8.5))
        {
            Source = new Formed(),
            Facets = [new(FacetAxis.Modality, "lift")],
        };

        Assert.Equal(
            """
            {"$type":"lift","MovementDataId":"11111111-2222-3333-4444-555555555555","Reps":5,"Weight":225,"Unit":1,"Rpe":8.5,"Source":{"$type":"formed"},"Facets":[{"Axis":0,"Value":"lift"}]}
            """,
            Store<SetEntry>(lift));
    }

    [Fact]
    public void Unrated_rpe_stores_as_null_inside_its_set()
    {
        var lift = new LiftSet(MovementDataId, Reps: 3, Weight: 235, WeightUnit.Pounds, Rpe.Unrated)
        {
            Source = new Texted("worked up to 235, 2 or 3 sets", 0.6),
            Facets = [],
        };

        Assert.Equal(
            """
            {"$type":"lift","MovementDataId":"11111111-2222-3333-4444-555555555555","Reps":3,"Weight":235,"Unit":1,"Rpe":null,"Source":{"$type":"texted","RawText":"worked up to 235, 2 or 3 sets","StructureConfidence":0.6},"Facets":[]}
            """,
            Store<SetEntry>(lift));
    }

    [Fact]
    public void Interval_stores_its_endpoints_and_nothing_derived()
    {
        Assert.Equal(
            """
            {"$type":"interval","Start":"2026-07-18T09:30:00+00:00","End":"2026-07-18T10:45:00+00:00"}
            """,
            Store<Period>(new Interval(Start, Start.AddMinutes(75))));
    }

    [Fact]
    public void Instant_stores_only_its_moment()
    {
        Assert.Equal(
            """
            {"$type":"instant","At":"2026-07-18T09:30:00+00:00"}
            """,
            Store<Period>(new Instant(Start)));
    }

    /// <summary>
    /// The captured offset is part of the payload — it is what later answers which calendar day
    /// the athlete trained on, and nothing else in storage can reconstruct it.
    /// </summary>
    [Fact]
    public void Instant_stores_the_offset_it_was_logged_at()
    {
        Assert.Equal(
            """
            {"$type":"instant","At":"2026-07-18T23:30:00-04:00"}
            """,
            Store<Period>(new Instant(new DateTimeOffset(2026, 7, 18, 23, 30, 0, TimeSpan.FromHours(-4)))));
    }

    /// <summary>
    /// Rows written before Period carried an offset stored a bare "Z". They must still read, as
    /// UTC — this is the compatibility direction that matters, since no rewrite is planned.
    /// </summary>
    [Fact]
    public void A_legacy_z_suffixed_instant_still_reads_as_utc()
    {
        var period = JsonSerializer.Deserialize<Period>(
            """{"$type":"instant","At":"2026-07-18T09:30:00Z"}""", ValueConversions.StorageJson);

        var instant = Assert.IsType<Instant>(period);
        Assert.Equal(TimeSpan.Zero, instant.At.Offset);
        Assert.Equal(new DateTime(2026, 7, 18, 9, 30, 0, DateTimeKind.Utc), instant.StartTimeUtc);
    }

    [Fact]
    public void Decorated_stores_its_decoration_and_no_fill_state_flag()
    {
        var decorated = new Decorated(new Decoration(
            [new(FacetAxis.Modality, "lift"), new(FacetAxis.MovementPattern, "push")],
            FreeTags: ["pr-attempt"],
            Valence.Positive,
            SummaryText: "Heavy push session.",
            DecoratorVersion: 1));

        Assert.Equal(
            """
            {"$type":"decorated","Decoration":{"Facets":[{"Axis":0,"Value":"lift"},{"Axis":1,"Value":"push"}],"FreeTags":["pr-attempt"],"Valence":0,"SummaryText":"Heavy push session.","DecoratorVersion":1}}
            """,
            Store<DecorationState>(decorated));
    }

    [Fact]
    public void Undecorated_stores_as_a_bare_discriminator()
    {
        Assert.Equal("""{"$type":"none"}""", Store<DecorationState>(new NotDecorated()));
    }

    [Fact]
    public void Completed_session_sets_column_stores_every_shape_in_order()
    {
        IReadOnlyList<SetEntry> sets =
        [
            new LiftSet(MovementDataId, Reps: 5, Weight: 225, WeightUnit.Pounds, Rpe.Of(8.5))
            {
                Source = new Formed(),
                Facets = [new(FacetAxis.Modality, "lift")],
            },
            new DistanceTimeSet(MovementDataId, Distance: 1600, DistanceUnit.Meters, TimeSpan.FromMinutes(8), Rpe.Unrated)
            {
                Source = new Formed(),
                Facets = [],
            },
            new TrailRunSet(Distance: 6.2, DistanceUnit.Miles, TimeSpan.FromMinutes(64), ElevationGainMeters: 410, Rpe.Of(7))
            {
                Source = new Formed(),
                Facets = [],
            },
            new FreeformSet
            {
                Source = new Texted("felt flat, called it early", 0.2),
                Facets = [new(FacetAxis.BodyState, "fatigued")],
            },
        ];

        Assert.Equal(
            """
            [{"$type":"lift","MovementDataId":"11111111-2222-3333-4444-555555555555","Reps":5,"Weight":225,"Unit":1,"Rpe":8.5,"Source":{"$type":"formed"},"Facets":[{"Axis":0,"Value":"lift"}]},{"$type":"distanceTime","MovementDataId":"11111111-2222-3333-4444-555555555555","Distance":1600,"Unit":0,"Duration":"00:08:00","Rpe":null,"Source":{"$type":"formed"},"Facets":[]},{"$type":"trailRun","Distance":6.2,"Unit":2,"Duration":"01:04:00","ElevationGainMeters":410,"Rpe":7,"Source":{"$type":"formed"},"Facets":[]},{"$type":"freeform","Source":{"$type":"texted","RawText":"felt flat, called it early","StructureConfidence":0.2},"Facets":[{"Axis":5,"Value":"fatigued"}]}]
            """,
            Store(sets));
    }

    [Fact]
    public void Reported_effort_stores_its_ratings()
    {
        EffortState effort = new EffortReported(new PerceivedEffortRatings
        {
            RecordedAt = Start,
            AccumulatedFatigue = 6,
            DifficultyRating = 8,
            EngagementRating = 7,
            ExternalVariablesRating = 3,
        });

        Assert.Equal(
            """
            {"$type":"reported","Ratings":{"RecordedAt":"2026-07-18T09:30:00Z","AccumulatedFatigue":6,"DifficultyRating":8,"EngagementRating":7,"ExternalVariablesRating":3}}
            """,
            Store(effort));
    }

    [Fact]
    public void Prescriptions_column_stores_every_shape()
    {
        IReadOnlyList<SetPrescription> prescriptions =
        [
            new LiftPrescription(MovementDataId, TargetReps: 5, TargetWeight: 225, WeightUnit.Pounds, Rpe.Of(8)),
            new DistanceTimePrescription(MovementDataId, TargetDistance: 1600, DistanceUnit.Meters, TimeSpan.FromMinutes(8)),
            new FreeformPrescription("hard intervals, coaches choice"),
        ];

        Assert.Equal(
            """
            [{"$type":"lift","MovementDataId":"11111111-2222-3333-4444-555555555555","TargetReps":5,"TargetWeight":225,"Unit":1,"TargetRpe":8},{"$type":"distanceTime","MovementDataId":"11111111-2222-3333-4444-555555555555","TargetDistance":1600,"Unit":0,"TargetDuration":"00:08:00"},{"$type":"freeform","Text":"hard intervals, coaches choice"}]
            """,
            Store(prescriptions));
    }

    /// <summary>
    /// The storage options use the relaxed encoder, so ordinary training punctuation is stored
    /// as itself. Under the default encoder "sled pull+push" would land as "sled pull\\u002Bpush" —
    /// lossless, but it bloats every row and makes the SQLite file unreadable by hand.
    /// </summary>
    [Theory]
    [InlineData("sled pull+push worked up to 235")]
    [InlineData("coach's choice")]
    [InlineData("felt <bad> & heavy")]
    [InlineData("emoji 💪 and \u4e2d\u6587")]
    public void Punctuation_is_stored_verbatim_and_round_trips(string text)
    {
        var stored = Store<Utterance>(new Texted(text, 1));

        Assert.Contains($"\"RawText\":{JsonSerializer.Serialize(text, ValueConversions.StorageJson)}", stored);
        Assert.DoesNotContain("\\u00", stored);
        Assert.Equal(text, Assert.IsType<Texted>(
            JsonSerializer.Deserialize<Utterance>(stored, ValueConversions.StorageJson)).RawText);
    }

    /// <summary>
    /// Enum members persist as ordinals, so member order is a storage contract. Reordering
    /// FacetAxis silently re-labels every stored facet; this is the tripwire.
    /// </summary>
    [Fact]
    public void Facet_axis_ordinals_are_frozen()
    {
        Assert.Equal(0, (int)FacetAxis.Modality);
        Assert.Equal(1, (int)FacetAxis.MovementPattern);
        Assert.Equal(2, (int)FacetAxis.MuscleRegion);
        Assert.Equal(3, (int)FacetAxis.Intent);
        Assert.Equal(4, (int)FacetAxis.LifeDomain);
        Assert.Equal(5, (int)FacetAxis.BodyState);
        Assert.Equal(6, Enum.GetValues<FacetAxis>().Length);
    }

    [Fact]
    public void Valence_and_unit_ordinals_are_frozen()
    {
        Assert.Equal(0, (int)Valence.Positive);
        Assert.Equal(1, (int)Valence.Negative);
        Assert.Equal(2, (int)Valence.Neutral);
        Assert.Equal(3, (int)Valence.Mixed);

        Assert.Equal(0, (int)WeightUnit.Kilograms);
        Assert.Equal(1, (int)WeightUnit.Pounds);

        Assert.Equal(0, (int)DistanceUnit.Meters);
        Assert.Equal(1, (int)DistanceUnit.Yards);
        Assert.Equal(2, (int)DistanceUnit.Miles);
        Assert.Equal(3, (int)DistanceUnit.Kilometers);
    }

    /// <summary>
    /// Every discriminator string in one place: renaming one orphans stored rows, and the
    /// round-trip tests would not notice because they rename both sides at once.
    /// </summary>
    [Theory]
    [InlineData(typeof(Instant), "instant")]
    [InlineData(typeof(Interval), "interval")]
    [InlineData(typeof(NotDecorated), "none")]
    [InlineData(typeof(Decorated), "decorated")]
    [InlineData(typeof(EffortNotReported), "none")]
    [InlineData(typeof(EffortReported), "reported")]
    [InlineData(typeof(Texted), "texted")]
    [InlineData(typeof(Formed), "formed")]
    [InlineData(typeof(LiftSet), "lift")]
    [InlineData(typeof(DistanceTimeSet), "distanceTime")]
    [InlineData(typeof(TrailRunSet), "trailRun")]
    [InlineData(typeof(FreeformSet), "freeform")]
    [InlineData(typeof(LiftPrescription), "lift")]
    [InlineData(typeof(DistanceTimePrescription), "distanceTime")]
    [InlineData(typeof(FreeformPrescription), "freeform")]
    public void Discriminator_strings_are_frozen(Type derived, string discriminator)
    {
        var declared = derived.BaseType!
            .GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonDerivedTypeAttribute), inherit: false)
            .Cast<System.Text.Json.Serialization.JsonDerivedTypeAttribute>()
            .Single(a => a.DerivedType == derived);

        Assert.Equal(discriminator, declared.TypeDiscriminator);
    }
}
