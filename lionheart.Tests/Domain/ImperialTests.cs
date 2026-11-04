using lionheart.Model;
using lionheart.Model.Training.Sets;

namespace lionheart.Tests.Domain;

/// <summary>The units figures are stated in for the chat model, which repeats them to the athlete.</summary>
public class ImperialTests
{
    [Fact]
    public void A_weight_logged_in_kilograms_reads_in_pounds_to_a_tenth()
    {
        Assert.Equal("220.5 lb", Imperial.Weight(100, WeightUnit.Kilograms));
        Assert.Equal("225 lb", Imperial.Weight(225, WeightUnit.Pounds));
    }

    [Fact]
    public void Kilometres_read_as_miles_and_track_distances_as_logged()
    {
        // A 400 m repeat is a 400 to a US runner, not a quarter of a mile; a 5 km run is 3.11 miles.
        Assert.Equal("3.11 mi", Imperial.Distance(5, DistanceUnit.Kilometers));
        Assert.Equal("400 m", Imperial.Distance(400, DistanceUnit.Meters));
        Assert.Equal("100 yd", Imperial.Distance(100, DistanceUnit.Yards));
        Assert.Equal("2.5 mi", Imperial.Distance(2.5, DistanceUnit.Miles));
    }

    [Fact]
    public void A_temperature_deviation_converts_by_the_scale_alone()
    {
        // A deviation of nothing is nothing in either scale; the 32-degree offset belongs to
        // absolute temperatures only.
        Assert.Equal(0, Imperial.FahrenheitChange(0));
        Assert.Equal(0.9, Imperial.FahrenheitChange(0.5), precision: 10);
    }

    [Fact]
    public void A_pace_per_kilometre_reads_per_mile_and_a_ride_in_mph()
    {
        Assert.Equal("8:03/mi", Imperial.Pace(Imperial.PerMile(TimeSpan.FromMinutes(5))));
        Assert.Equal("18.6 mph", Imperial.Speed(Imperial.PerMile(TimeSpan.FromMinutes(2))));
        Assert.Equal("459 ft", Imperial.Feet(140));
    }
}
