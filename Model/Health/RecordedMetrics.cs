using System.Text.Json;
using System.Text.Json.Serialization;

namespace lionheart.Model.Health;

/// <summary>What one series of a workout's samples measures, each in the one unit it is held in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkoutMetric>))]
public enum WorkoutMetric
{
    /// <summary>Watts: a ride's from a power meter or a trainer, a run's from the watch.</summary>
    [JsonStringEnumMemberName("power")] Power,

    /// <summary>Pedal revolutions per minute.</summary>
    [JsonStringEnumMemberName("cadence")] Cadence,

    /// <summary>Metres per second, as the device measured it rather than as the route implies.</summary>
    [JsonStringEnumMemberName("speed")] Speed,

    /// <summary>Milliseconds each foot is on the ground.</summary>
    [JsonStringEnumMemberName("groundContactTime")] GroundContactTime,

    /// <summary>Centimetres the body rises and falls each stride.</summary>
    [JsonStringEnumMemberName("verticalOscillation")] VerticalOscillation,

    /// <summary>Metres each stride covers.</summary>
    [JsonStringEnumMemberName("strideLength")] StrideLength,

    /// <summary>Beats per minute the heart rate fell in the minute after the workout ended.</summary>
    [JsonStringEnumMemberName("heartRateRecovery")] HeartRateRecovery,
}

public static class WorkoutMetrics
{
    private static readonly IReadOnlyDictionary<string, WorkoutMetric> ByWireName =
        Enum.GetValues<WorkoutMetric>().ToDictionary(metric => JsonNamingPolicy.CamelCase.ConvertName(metric.ToString()), StringComparer.Ordinal);

    /// <summary>The metric a wire name names, or none for a name this server does not know.</summary>
    public static WorkoutMetric? Named(string? wire) =>
        wire is not null && ByWireName.TryGetValue(wire, out var metric) ? metric : null;

    extension(WorkoutMetric metric)
    {
        /// <summary>
        /// Whether a reading can be one: inside what any rider or runner produces. A reading past it
        /// is a sensor's mistake or a unit slip, left out as an impossible heart rate is.
        /// </summary>
        public bool Admits(double value) => double.IsFinite(value) && value >= 0 && value <= metric switch
        {
            WorkoutMetric.Power => 3000,
            WorkoutMetric.Cadence => 250,
            WorkoutMetric.Speed => 40,
            WorkoutMetric.GroundContactTime => 2000,
            WorkoutMetric.VerticalOscillation => 40,
            WorkoutMetric.StrideLength => 4,
            WorkoutMetric.HeartRateRecovery => 150,
            _ => throw new System.Diagnostics.UnreachableException(),
        };

        /// <summary>A reading to the precision its instrument has: a watt, a tenth of a centimetre.</summary>
        public double Rounded(double value) => Math.Round(value, metric switch
        {
            WorkoutMetric.Power or WorkoutMetric.Cadence or WorkoutMetric.GroundContactTime
                or WorkoutMetric.HeartRateRecovery => 0,
            WorkoutMetric.VerticalOscillation => 1,
            WorkoutMetric.Speed or WorkoutMetric.StrideLength => 2,
            _ => throw new System.Diagnostics.UnreachableException(),
        });
    }
}

public sealed record MetricSample(DateTimeOffset At, double Value);

/// <summary>One metric's samples across a workout, oldest first.</summary>
public sealed record MetricSeries(WorkoutMetric Metric, IReadOnlyList<MetricSample> Samples)
{
    public IReadOnlyList<MetricSample> Samples { get; init; } = Require.Present(Samples, nameof(Samples));

    /// <summary>Compared by the samples it holds, not by the list holding them.</summary>
    public bool Equals(MetricSeries? other) =>
        other is not null && Metric == other.Metric && Samples.SequenceEqual(other.Samples);

    public override int GetHashCode() => HashCode.Combine(Metric, Samples.Count);
}

/// <summary>
/// What the app that recorded a workout measured across it beyond heart rate: a ride's power,
/// cadence and speed; a run's power, speed and form; the heart rate's recovery after it.
/// </summary>
/// <remarks>
/// At most one series per metric, each oldest first and none empty, so a reader asks for a metric
/// and gets its samples or nothing. Empty is <see cref="None"/>: the app wrote none of these, which
/// is most apps and every workout before a watch measured them.
/// </remarks>
public sealed record RecordedMetrics
{
    private RecordedMetrics(IReadOnlyList<MetricSeries> series) => Series = series;

    public IReadOnlyList<MetricSeries> Series { get; }

    public static RecordedMetrics None { get; } = new([]);

    public bool IsRecorded => Series.Count > 0;

    /// <summary>
    /// The samples, grouped one series to a metric in the order the metrics are declared, each
    /// oldest first and rounded to its instrument's precision, without a reading that cannot be one.
    /// </summary>
    public static RecordedMetrics Of(IEnumerable<(WorkoutMetric Metric, DateTimeOffset At, double Value)> readings) =>
        new([
            .. readings
                .Where(reading => reading.Metric.Admits(reading.Value))
                .GroupBy(reading => reading.Metric)
                .OrderBy(group => group.Key)
                .Select(group => new MetricSeries(
                    group.Key,
                    [.. group.OrderBy(reading => reading.At).Select(reading => new MetricSample(reading.At, group.Key.Rounded(reading.Value)))])),
        ]);

    /// <summary>One metric's samples, or none where the app did not write it.</summary>
    public IReadOnlyList<MetricSample> Samples(WorkoutMetric metric) =>
        Series.FirstOrDefault(series => series.Metric == metric)?.Samples ?? [];

    /// <summary>Compared by the series it holds, not by the list holding them.</summary>
    public bool Equals(RecordedMetrics? other) => other is not null && Series.SequenceEqual(other.Series);

    public override int GetHashCode() => Series.Count;
}
