using System.Text.Json;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Serialization;

/// <summary>
/// The sealed hierarchies persist as JSON TEXT discriminated by "$type". Every concrete
/// shape must survive a round trip through its abstract base, and each discriminator string
/// is a storage contract — renaming one silently orphans existing rows.
/// </summary>
public class PolymorphicJsonTests
{
    public static TheoryData<object, Type, string> EveryConcreteShape() => new()
    {
        { new Instant(Some.Clock), typeof(Period), "instant" },
        { new Interval(Some.Clock, Some.Clock.AddMinutes(75)), typeof(Period), "interval" },
        { new NotDecorated(), typeof(DecorationState), "none" },
        { new Decorated(Some.Decoration), typeof(DecorationState), "decorated" },
        { new EffortNotReported(), typeof(EffortState), "none" },
        { Some.ReportedEffort, typeof(EffortState), "reported" },
        { Some.TextedSource, typeof(Utterance), "texted" },
        { new Formed(), typeof(Utterance), "formed" },
        { Some.LiftSet, typeof(SetEntry), "lift" },
        { Some.DistanceTimeSet, typeof(SetEntry), "distanceTime" },
        { Some.TrailRunSet, typeof(SetEntry), "trailRun" },
        { Some.FreeformSet, typeof(SetEntry), "freeform" },
        { Some.AllPrescriptionShapes[0], typeof(SetPrescription), "lift" },
        { Some.AllPrescriptionShapes[1], typeof(SetPrescription), "distanceTime" },
        { Some.AllPrescriptionShapes[2], typeof(SetPrescription), "freeform" },
    };

    [Theory]
    [MemberData(nameof(EveryConcreteShape))]
    public void Every_shape_round_trips_through_its_base_type(object value, Type baseType, string discriminator)
    {
        var json = JsonSerializer.Serialize(value, baseType);
        var rehydrated = JsonSerializer.Deserialize(json, baseType);

        Assert.NotNull(rehydrated);
        Assert.IsType(value.GetType(), rehydrated);
        Assert.Equal(json, JsonSerializer.Serialize(rehydrated, baseType));

        using var document = JsonDocument.Parse(json);
        Assert.Equal(discriminator, document.RootElement.GetProperty("$type").GetString());
    }

    [Fact]
    public void Heterogeneous_set_list_round_trips_shape_by_shape()
    {
        var sets = Some.AllSetShapes;

        var json = JsonSerializer.Serialize(sets);
        var rehydrated = JsonSerializer.Deserialize<IReadOnlyList<SetEntry>>(json);

        Assert.NotNull(rehydrated);
        Assert.Equal(
            sets.Select(s => s.GetType()),
            rehydrated.Select(s => s.GetType()));
        JsonAssert.Equal(sets, rehydrated);
    }

    [Fact]
    public void Freeform_set_keeps_its_raw_words_through_the_round_trip()
    {
        var json = JsonSerializer.Serialize<SetEntry>(Some.FreeformSet);
        var rehydrated = Assert.IsType<FreeformSet>(JsonSerializer.Deserialize<SetEntry>(json));

        var source = Assert.IsType<Texted>(rehydrated.Source);
        Assert.Equal(Some.TextedSource.RawText, source.RawText);
        Assert.Equal(Some.TextedSource.StructureConfidence, source.StructureConfidence);
    }

    [Fact]
    public void Corrupted_interval_fails_loudly_instead_of_rehydrating_invalid_state()
    {
        const string endBeforeStart =
            """{"$type":"interval","Start":"2026-07-18T10:00:00Z","End":"2026-07-18T09:00:00Z"}""";

        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<Period>(endBeforeStart));
    }
}
