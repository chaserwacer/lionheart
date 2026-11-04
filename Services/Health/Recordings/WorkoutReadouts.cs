using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;

namespace lionheart.Services.Health.Recordings;

/// <summary>The heart-rate samples a run is read against, and whose they are.</summary>
public sealed record WorkoutHeartRate(IReadOnlyList<HeartRateSample> Samples, HeartRateSource Source);

/// <summary>
/// A recorded run read whole for its page and for the chat model: the session's figures, the
/// route's, the splits and the heart rate.
/// </summary>
/// <remarks>
/// Pure. The heart rate is summarised by <see cref="CardioAnalysis.HeartRateOf"/> over the same
/// samples and the same maximum the cardio report uses, so the run's page and the cardio screen
/// never give one run two averages.
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
                    new CardioSessionSource.LoggedSession(session.TrainingSessionId, IsTrailRun: true, IsRecorded: true),
                    start, end, stated.Meters),
                samples,
                heartRate.Source,
                maximum),
            maximum,
            new HeartRateTrace(heartRate.Source, [.. samples.Select(sample => new HeartRateReading(sample.At, sample.Bpm))]),
            RouteAnalysis.Splits(recording.Route, stated.Meters, DistanceUnit.Kilometers.MetersEach(), samples),
            RouteAnalysis.Splits(recording.Route, stated.Meters, DistanceUnit.Miles.MetersEach(), samples),
            [.. recording.Route.Points.Select(point => new Coordinate(point.Latitude, point.Longitude))]);
    }

    /// <summary>
    /// The run's distance, time and climb as the session states them — its trail-run sets, which a
    /// correction may have restated — and the route's own where a correction left none.
    /// </summary>
    private static (double Meters, TimeSpan Duration, double Climb) Stated(
        CompletedTrainingSession session, RouteFigures route, TimeSpan elapsed)
    {
        var runs = session.Sets.OfType<TrailRunSet>().ToList();
        return runs.Count is 0
            ? (route.Meters, elapsed, route.ClimbMeters ?? 0)
            : (runs.Sum(run => run.Distance * run.Unit.MetersEach()),
               runs.Aggregate(TimeSpan.Zero, (total, run) => total + run.Duration),
               runs.Sum(run => run.ElevationGainMeters));
    }

    private static TimeSpan? PerKilometre(TimeSpan time, double meters) =>
        meters > 0 ? time * (DistanceUnit.Kilometers.MetersEach() / meters) : null;
}
