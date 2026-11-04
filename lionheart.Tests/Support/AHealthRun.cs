using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;

namespace lionheart.Tests.Support;

/// <summary>
/// Apple Health runs as the phone sends them, invented in Health's shape: a straight route due north
/// at an even pace, one fix every few seconds, and a strap's heart rate.
/// </summary>
/// <remarks>
/// Every value here is made up — the place is an open meadow nobody in this repository lives near,
/// the ids are fresh, the dates arbitrary. Nothing in a fixture may come from the athlete's own
/// Health data, since a route says where someone lives.
/// </remarks>
public static class AHealthRun
{
    public static readonly DateTimeOffset Start = new(2026, 9, 20, 7, 0, 0, TimeSpan.FromHours(2));

    public const string StravaName = "Strava";
    public const string StravaBundle = "com.strava.stravaride";

    /// <summary>An invented starting point, in open country.</summary>
    public const double Latitude = 46.5;
    public const double Longitude = 8.25;

    /// <summary>Metres in one degree of latitude on the sphere the analysis measures over.</summary>
    public const double MetresPerDegree = 6_371_008.8 * Math.PI / 180;

    /// <summary>
    /// An outdoor run of <paramref name="meters"/> at <paramref name="metersPerSecond"/>, recorded
    /// by Strava with a route and heart rate across it.
    /// </summary>
    public static HealthWorkoutImport Outdoor(
        double meters = 5000,
        double metersPerSecond = 3,
        DateTimeOffset? start = null,
        Guid? workoutId = null,
        double? statedMeters = null,
        int bpm = 150)
    {
        var from = start ?? Start;
        var route = North(from, meters, metersPerSecond);
        var end = route[^1].At;
        return new HealthWorkoutImport(
            workoutId ?? Guid.NewGuid(),
            "running",
            Indoor: false,
            StravaName,
            StravaBundle,
            from,
            end,
            end - from,
            statedMeters ?? meters,
            ElevationAscendedMeters: null,
            route,
            [.. Enumerable.Range(0, (int)((end - from).TotalSeconds / 5)).Select(index => new HeartRateCommand(from.AddSeconds(5 * index), bpm))]);
    }

    /// <summary>
    /// Fixes due north from the invented start, every <paramref name="secondsPerFix"/> seconds, at an
    /// even pace and an altitude of <paramref name="altitude"/> (by fix index) where given.
    /// </summary>
    public static IReadOnlyList<RoutePointCommand> North(
        DateTimeOffset start, double meters, double metersPerSecond, int secondsPerFix = 5, Func<int, double?>? altitude = null)
    {
        var fixes = (int)Math.Ceiling(meters / (metersPerSecond * secondsPerFix));
        return
        [
            .. Enumerable.Range(0, fixes + 1).Select(index =>
            {
                var covered = Math.Min(meters, index * metersPerSecond * secondsPerFix);
                return new RoutePointCommand(
                    start.AddSeconds(covered / metersPerSecond),
                    Latitude + covered / MetresPerDegree,
                    Longitude,
                    altitude is null ? 400 : altitude(index));
            }),
        ];
    }

    /// <summary>The same fixes as domain points.</summary>
    public static WorkoutRoute Route(IReadOnlyList<RoutePointCommand> fixes) =>
        new([.. fixes.Select(fix => new RoutePoint(fix.At, fix.Latitude, fix.Longitude, fix.Altitude))]);

    public static SessionSource.AppleHealth Source(Guid? workoutId = null, string bundle = StravaBundle) =>
        new(workoutId ?? Guid.NewGuid(), new RecordingApp(StravaName, bundle));
}
