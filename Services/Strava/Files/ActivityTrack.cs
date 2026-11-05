using lionheart.Functional;
using lionheart.Model.Health;

namespace lionheart.Services.Strava.Files;

/// <summary>
/// What an activity's file recorded beyond the archive's summary row: the route, the heart rate and
/// the other metrics, in the shapes a <see cref="WorkoutRecording"/> holds them.
/// </summary>
/// <remarks>
/// <see cref="None"/> for an activity with no file — one entered by hand on Strava, or a file the
/// archive lacks — which is a fact about the activity rather than a fault.
/// </remarks>
public sealed record ActivityTrack(WorkoutRoute Route, RecordedHeartRate HeartRate, RecordedMetrics Metrics)
{
    public static ActivityTrack None { get; } = new(WorkoutRoute.None, RecordedHeartRate.None, RecordedMetrics.None);

    /// <summary>
    /// The track from samples as a file stated them, without a fix that is no place on Earth or a
    /// reading that cannot be one — the rules a Health workout's samples are held to.
    /// </summary>
    public static ActivityTrack Of(IReadOnlyList<TrackSample> samples) =>
        new(
            new WorkoutRoute(samples
                .Where(sample => sample.Latitude is not null && sample.Longitude is not null)
                .Partition(sample => RoutePoint.Fix(sample.At, sample.Latitude!.Value, sample.Longitude!.Value, sample.Altitude))
                .Delivered),
            RecordedHeartRate.Of(
                from sample in samples
                where sample.HeartRate is { } bpm && RecordedHeartRate.Admits(bpm)
                select (sample.At, sample.HeartRate!.Value)),
            RecordedMetrics.Of(
                from sample in samples
                from reading in sample.Readings()
                select (reading.Metric, sample.At, reading.Value)));

    public WorkoutRecording RecordingFor(Guid trainingSessionId, Guid userId) =>
        WorkoutRecording.Of(trainingSessionId, userId, Route, HeartRate, Metrics);
}

/// <summary>One sample of an activity file — a GPX track point or a FIT record — with what it carried.</summary>
public sealed record TrackSample(
    DateTimeOffset At,
    double? Latitude,
    double? Longitude,
    double? Altitude,
    int? HeartRate,
    double? Cadence,
    double? Power,
    double? Speed)
{
    public IEnumerable<(WorkoutMetric Metric, double Value)> Readings()
    {
        if (Power is { } power) yield return (WorkoutMetric.Power, power);
        if (Cadence is { } cadence) yield return (WorkoutMetric.Cadence, cadence);
        if (Speed is { } speed) yield return (WorkoutMetric.Speed, speed);
    }
}
