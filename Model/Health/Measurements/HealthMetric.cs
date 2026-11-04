using System.Text.Json.Serialization;

namespace lionheart.Model.Health.Measurements;

/// <summary>A figure Apple's own devices measure and write to Health that Lionheart keeps day by day.</summary>
/// <remarks>
/// Each is held in one fixed unit, the one named on its member, so a stored number never needs
/// its unit beside it; the phone reads Health in exactly these units.
/// <para>
/// <b>HRV here is SDNN</b>, the spread of beat-to-beat intervals over a short reading. Oura's HRV
/// is RMSSD over the night. They are different statistics over different windows and run at
/// different levels for the same heart, so the two are never merged, averaged or drawn on one
/// axis: Oura's stays in Oura's tables and this one here.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<HealthMetric>))]
public enum HealthMetric
{
    /// <summary>The watch's estimate from outdoor walks, runs and hikes, ml/kg/min.</summary>
    [JsonStringEnumMemberName("vo2Max")] Vo2Max,

    /// <summary>The watch's resting heart rate for the day, bpm.</summary>
    [JsonStringEnumMemberName("restingHeartRate")] RestingHeartRate,

    /// <summary>Heart-rate variability as SDNN, ms. Not comparable with Oura's RMSSD.</summary>
    [JsonStringEnumMemberName("heartRateVariabilitySdnn")] HeartRateVariabilitySdnn,

    /// <summary>Breaths per minute, measured by the watch during sleep.</summary>
    [JsonStringEnumMemberName("respiratoryRate")] RespiratoryRate,

    /// <summary>The day's mean heart rate while walking, bpm.</summary>
    [JsonStringEnumMemberName("walkingHeartRateAverage")] WalkingHeartRateAverage,

    /// <summary>Walking speed on level ground, m/s.</summary>
    [JsonStringEnumMemberName("walkingSpeed")] WalkingSpeed,

    /// <summary>Step length while walking, m.</summary>
    [JsonStringEnumMemberName("walkingStepLength")] WalkingStepLength,

    /// <summary>The share of the gait cycle with both feet on the ground, 0 to 1.</summary>
    [JsonStringEnumMemberName("walkingDoubleSupport")] WalkingDoubleSupport,

    /// <summary>The share of steps whose timing differs between the two legs, 0 to 1.</summary>
    [JsonStringEnumMemberName("walkingAsymmetry")] WalkingAsymmetry,

    /// <summary>Apple Walking Steadiness, 0 to 1.</summary>
    [JsonStringEnumMemberName("walkingSteadiness")] WalkingSteadiness,

    /// <summary>The phone's estimate of how far a six-minute walk test would go, m.</summary>
    [JsonStringEnumMemberName("sixMinuteWalkDistance")] SixMinuteWalkDistance,
}

/// <summary>Which of Apple's devices took a reading.</summary>
/// <remarks>
/// Kept per row rather than implied by the metric: the watch and the phone both measure some
/// walking figures, and the two are carried at different places on the body, so a day measured by
/// both keeps both rather than a blend of them.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<MeasuringDevice>))]
public enum MeasuringDevice
{
    [JsonStringEnumMemberName("appleWatch")] AppleWatch,
    [JsonStringEnumMemberName("iPhone")] IPhone,
}

/// <summary>What a figure can physically be, which a day's reading has to fall inside.</summary>
public static class HealthMetricBounds
{
    /// <summary>The lowest and highest values a reading of the metric can take, both included.</summary>
    /// <remarks>
    /// Wide enough for any person and narrow enough that a unit slip — milliseconds sent as
    /// seconds, a percentage sent as 0 to 100 — lands outside.
    /// </remarks>
    public static (double Lowest, double Highest) Of(HealthMetric metric) => metric switch
    {
        HealthMetric.Vo2Max => (10, 100),
        HealthMetric.RestingHeartRate => (20, 200),
        HealthMetric.HeartRateVariabilitySdnn => (1, 400),
        HealthMetric.RespiratoryRate => (3, 60),
        HealthMetric.WalkingHeartRateAverage => (30, 230),
        HealthMetric.WalkingSpeed => (0.05, 5),
        HealthMetric.WalkingStepLength => (0.05, 2.5),
        HealthMetric.WalkingDoubleSupport => (0, 1),
        HealthMetric.WalkingAsymmetry => (0, 1),
        HealthMetric.WalkingSteadiness => (0, 1),
        HealthMetric.SixMinuteWalkDistance => (10, 2000),
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}
