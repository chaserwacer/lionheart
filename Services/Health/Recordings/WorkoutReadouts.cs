using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;

namespace lionheart.Services.Health.Recordings;

/// <summary>The heart-rate samples a workout is read against, and whose they are.</summary>
public sealed record WorkoutHeartRate(IReadOnlyList<HeartRateSample> Samples, HeartRateSource Source);

/// <summary>
/// A recorded workout read whole for its page and for the chat model: the session's figures, the
/// route's, the splits, the heart rate and the app's other metrics.
/// </summary>
/// <remarks>
/// Pure. The heart rate is summarised by <see cref="CardioAnalysis.HeartRateOf"/> over the same
/// samples and the same maximum the cardio report uses, so the workout's page and the cardio screen
/// never give one workout two averages.
/// </remarks>
public static class WorkoutReadouts
{
    /// <param name="heartRate">The recording app's samples where it wrote any, the ring's otherwise.</param>
    /// <param name="maximum">The maximum the cardio report draws zones from, or nothing without one.</param>
    public static RecordedWorkoutView Of(
        CompletedTrainingSession session,
        SessionSource.AppleHealth source,
        WorkoutRecording recording,
        WorkoutHeartRate heartRate,
        MaxHeartRate? maximum)
    {
        var (start, end) = SessionMatching.Span(session.Period) ?? (session.Period.StartTime, session.Period.StartTime);
        var route = RouteAnalysis.Figures(recording.Route);
        var stated = Stated(session, route, end - start);
        var moving = recording.Route.HasPath ? route.Moving : (TimeSpan?)null;
        var samples = heartRate.Samples.Where(sample => sample.At >= start && sample.At < end).ToList();

        return new RecordedWorkoutView(
            session.TrainingSessionId,
            source.RecordedBy,
            start,
            end,
            stated.Meters,
            stated.Duration,
            moving,
            PerKilometre(stated.Duration, stated.Meters),
            moving is { } inMotion ? PerKilometre(inMotion, stated.Meters) : null,
            stated.Climb,
            route.DescentMeters,
            CardioAnalysis.HeartRateOf(
                new GatheredSession(
                    new CardioSessionSource.LoggedSession(
                        session.TrainingSessionId,
                        IsTrailRun: session.Sets.OfType<TrailRunSet>().Any(),
                        IsRecorded: true,
                        session.Sets.EnduranceActivity),
                    start, end, stated.Meters),
                samples,
                heartRate.Source,
                maximum),
            maximum,
            new HeartRateTrace(heartRate.Source, [.. samples.Select(sample => new HeartRateReading(sample.At, sample.Bpm))]),
            RouteAnalysis.Splits(recording.Route, stated.Meters, DistanceUnit.Kilometers.MetersEach(), samples),
            RouteAnalysis.Splits(recording.Route, stated.Meters, DistanceUnit.Miles.MetersEach(), samples),
            [.. recording.Route.Points.Select(point => new Coordinate(point.Latitude, point.Longitude))],
            ActivityOf(session),
            [.. recording.Metrics.Series.Select(Readout)]);
    }

    /// <summary>
    /// What the session is, from its set: a trail run is a run, an endurance set its own activity. A
    /// correction can leave no set at all, and a session imported as a run reads as one still.
    /// </summary>
    public static RecordedActivity ActivityOf(CompletedTrainingSession session) =>
        session.Sets.EnduranceActivity switch
        {
            null => RecordedActivity.Run,
            EnduranceActivity.Ride => RecordedActivity.Ride,
            EnduranceActivity.IndoorRide => RecordedActivity.IndoorRide,
            EnduranceActivity.Hike => RecordedActivity.Hike,
            EnduranceActivity.Walk => RecordedActivity.Walk,
            EnduranceActivity.IndoorRun => RecordedActivity.IndoorRun,
            _ => throw new System.Diagnostics.UnreachableException(),
        };

    private static MetricReadout Readout(MetricSeries series) =>
        new(
            series.Metric,
            series.Metric.Rounded(series.Samples.Average(sample => sample.Value)),
            series.Samples.Max(sample => sample.Value),
            [.. series.Samples.Select(sample => new MetricReading(sample.At, sample.Value))]);

    /// <summary>
    /// The workout's distance, time and climb as the session states them — its sets, which a
    /// correction may have restated — and the route's own where a correction left none.
    /// </summary>
    private static (double Meters, TimeSpan Duration, double Climb) Stated(
        CompletedTrainingSession session, RouteFigures route, TimeSpan elapsed) =>
        Covered(session) switch
        {
            [] => (route.Meters, elapsed, route.ClimbMeters ?? 0),
            var covered => (covered.Sum(set => set.Meters),
                            covered.Aggregate(TimeSpan.Zero, (total, set) => total + set.Duration),
                            covered.Sum(set => set.Climb)),
        };

    /// <summary>The session's measured stretches: its trail runs and endurance sets.</summary>
    public static IReadOnlyList<(double Meters, TimeSpan Duration, double Climb)> Covered(CompletedTrainingSession session) =>
    [
        .. session.Sets.Select(set => set switch
            {
                TrailRunSet run => (run.Distance * run.Unit.MetersEach(), run.Duration, run.ElevationGainMeters),
                EnduranceSet endurance => (endurance.Distance * endurance.Unit.MetersEach(), endurance.Duration, endurance.ElevationGainMeters),
                _ => ((double, TimeSpan, double)?)null,
            })
            .OfType<(double, TimeSpan, double)>(),
    ];

    private static TimeSpan? PerKilometre(TimeSpan time, double meters) =>
        meters > 0 ? time * (DistanceUnit.Kilometers.MetersEach() / meters) : null;
}
