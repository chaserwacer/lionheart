using lionheart.Model.Training.Sets;

namespace lionheart.Model.BodyMeasurements;

/// <summary>Logs what the athlete weighed; the day comes from the route.</summary>
public sealed record BodyweightRequest(double Weight, WeightUnit Unit);

/// <summary>One day's bodyweight on the wire, flat: the day, the number, its unit.</summary>
public sealed record BodyweightView(DateOnly Day, double Weight, WeightUnit Unit)
{
    public static BodyweightView Of(BodyweightMeasurement measurement) =>
        new(measurement.Day, measurement.Bodyweight.Value, measurement.Bodyweight.Unit);
}
