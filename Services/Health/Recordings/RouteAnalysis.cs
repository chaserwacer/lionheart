using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Oura.Readings;

namespace lionheart.Services.Health.Recordings;

/// <summary>What a route says on its own: its length, its time in motion, and its climb and descent.</summary>
/// <param name="ClimbMeters">Absent where no fix carried an altitude.</param>
public sealed record RouteFigures(double Meters, TimeSpan Moving, double? ClimbMeters, double? DescentMeters);

/// <summary>
/// What a recorded route says: its length, its time in motion, its climb and descent, and its
/// splits.
/// </summary>
/// <remarks>
/// Pure, so every judgement in it — when a runner is stopped, how much altitude change is a climb,
/// how a split is cut — is tested without a database. The route is first read as progress along
/// it: at each fix, the distance covered, the clock time, the moving time, the climb and the descent
/// so far. Every figure is then a difference between two points of progress, and a split boundary
/// that falls between two fixes is interpolated.
/// </remarks>
public static class RouteAnalysis
{
    /// <summary>
    /// The slowest a fix-to-fix pace can be and still count as moving, in metres a second.
    /// </summary>
    /// <remarks>
    /// About a third of a slow walk (1.4 m/s), so a steep power-hike on a trail run counts as
    /// moving, while a phone standing still — whose fixes wander a metre or so either way — does not.
    /// A recording app that pauses leaves a gap with little distance across it, which this reads as
    /// stopped without needing to know the app paused.
    /// </remarks>
    public const double MovingSpeed = 0.5;

    /// <summary>
    /// How far the altitude must move from the last altitude counted before it counts as climb or
    /// descent, in metres.
    /// </summary>
    /// <remarks>
    /// A dead band, because a phone's altitude jitters by a metre or two between fixes, and summing
    /// every wobble on a flat road would report a hill. Three metres is under the step any real
    /// rise makes and over most of the noise; a barometric altimeter's trace passes it untouched.
    /// </remarks>
    public const double ClimbBand = 3;

    /// <summary>
    /// The shortest remainder after the last whole unit that is a split of its own. Less is a run
    /// stopped a few strides past a marker, and its pace would be noise.
    /// </summary>
    public const double ShortestLastSplit = 10;

    /// <summary>The mean radius of the Earth, in metres (IUGG).</summary>
    private const double EarthRadius = 6_371_008.8;

    public static RouteFigures Figures(WorkoutRoute route)
    {
        if (!route.HasPath) return new RouteFigures(0, TimeSpan.Zero, null, null);

        var track = Track.Along(route, scale: 1);
        var end = track.Last;
        return new RouteFigures(
            end.Meters,
            TimeSpan.FromSeconds(end.Moving),
            track.HasAltitude ? end.Climb : null,
            track.HasAltitude ? end.Descent : null);
    }

    /// <summary>
    /// The route cut into lengths of <paramref name="unitMeters"/>, the last one what is left.
    /// </summary>
    /// <param name="distanceMeters">
    /// The run's stated distance. The route's own length differs from it by the recording app's
    /// smoothing, so the route is scaled to it and the splits add up to the distance the run is
    /// recorded as. Zero leaves the route's own length.
    /// </param>
    /// <param name="heartRate">The samples each split's average is read from, oldest first.</param>
    public static IReadOnlyList<Split> Splits(
        WorkoutRoute route, double distanceMeters, double unitMeters, IReadOnlyList<HeartRateSample> heartRate)
    {
        if (!route.HasPath) return [];

        var length = Figures(route).Meters;
        var track = Track.Along(route, scale: distanceMeters > 0 && length > 0 ? distanceMeters / length : 1);
        var bounds = Bounds(track.Last.Meters, unitMeters);

        return
        [
            .. bounds.Zip(bounds.Skip(1)).Select((stretch, index) => Cut(
                index + 1, track, track.At(stretch.First), track.At(stretch.Second), unitMeters, route.Points[0].At, heartRate))
        ];
    }

    /// <summary>The distance between two fixes over the Earth's surface (haversine), in metres.</summary>
    public static double MetersBetween(RoutePoint from, RoutePoint to)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;

        var latitude = Radians(to.Latitude - from.Latitude);
        var longitude = Radians(to.Longitude - from.Longitude);
        var a = Math.Pow(Math.Sin(latitude / 2), 2)
                + Math.Cos(Radians(from.Latitude)) * Math.Cos(Radians(to.Latitude)) * Math.Pow(Math.Sin(longitude / 2), 2);
        return 2 * EarthRadius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>0, every whole unit, and the end where enough is left past the last whole one.</summary>
    private static IReadOnlyList<double> Bounds(double total, double unit)
    {
        var whole = (int)Math.Floor(total / unit);
        IEnumerable<double> marks = Enumerable.Range(0, whole + 1).Select(count => count * unit);
        return [.. total - whole * unit >= ShortestLastSplit ? marks.Append(total) : marks];
    }

    private static Split Cut(
        int number,
        Track track,
        Progress from,
        Progress to,
        double unitMeters,
        DateTimeOffset start,
        IReadOnlyList<HeartRateSample> heartRate)
    {
        var meters = to.Meters - from.Meters;
        var elapsed = to.Seconds - from.Seconds;
        var moving = to.Moving - from.Moving;

        return new Split(
            number,
            meters,
            TimeSpan.FromSeconds(elapsed),
            TimeSpan.FromSeconds(moving),
            TimeSpan.FromSeconds((moving > 0 ? moving : elapsed) * unitMeters / meters),
            track.HasAltitude ? to.Climb - from.Climb : null,
            track.HasAltitude ? to.Descent - from.Descent : null,
            AverageBpm(heartRate, start.AddSeconds(from.Seconds), start.AddSeconds(to.Seconds)));
    }

    private static int? AverageBpm(IReadOnlyList<HeartRateSample> samples, DateTimeOffset from, DateTimeOffset to)
    {
        var inside = samples.Where(sample => sample.At >= from && sample.At < to).ToList();
        return inside.Count is 0
            ? null
            : (int)Math.Round(SampleTime.AverageBpm([.. SampleTime.Attributed(inside, to)]));
    }

    /// <summary>How far along the route, and everything accumulated getting there.</summary>
    private readonly record struct Progress(double Meters, double Seconds, double Moving, double Climb, double Descent);

    /// <summary>The route as progress at each fix.</summary>
    private sealed class Track
    {
        private readonly IReadOnlyList<Progress> _fixes;

        private Track(IReadOnlyList<Progress> fixes, bool hasAltitude)
        {
            _fixes = fixes;
            HasAltitude = hasAltitude;
        }

        public bool HasAltitude { get; }

        public Progress Last => _fixes[^1];

        /// <param name="scale">What each fix-to-fix distance is multiplied by, so the track ends at a stated length.</param>
        public static Track Along(WorkoutRoute route, double scale)
        {
            var points = route.Points;
            var steps = Elevation(points);

            var fixes = points.Skip(1).Select((point, index) => (From: points[index], To: point, Step: steps[index + 1]))
                .Aggregate(
                    new List<Progress> { new(0, 0, 0, 0, 0) },
                    (progress, leg) =>
                    {
                        var before = progress[^1];
                        var meters = MetersBetween(leg.From, leg.To);
                        var seconds = (leg.To.At - leg.From.At).TotalSeconds;
                        var moved = seconds > 0 && meters / seconds >= MovingSpeed;
                        progress.Add(new Progress(
                            before.Meters + meters * scale,
                            before.Seconds + seconds,
                            before.Moving + (moved ? seconds : 0),
                            before.Climb + leg.Step.Up,
                            before.Descent + leg.Step.Down));
                        return progress;
                    });

            return new Track(fixes, points.Any(point => point.Altitude is not null));
        }

        /// <summary>
        /// The progress at a distance along the track, interpolated between the fixes either side.
        /// Climb and descent are counted at the fix where the dead band confirms them, so a split
        /// takes the climb its own fixes confirmed and the splits add up to the whole.
        /// </summary>
        public Progress At(double meters)
        {
            var after = FirstReaching(meters);
            if (after == 0) return _fixes[0];

            var (before, next) = (_fixes[after - 1], _fixes[after]);
            var span = next.Meters - before.Meters;
            var share = span > 0 ? Math.Clamp((meters - before.Meters) / span, 0, 1) : 1;
            var reached = share >= 1 ? next : before;

            return new Progress(
                meters,
                before.Seconds + share * (next.Seconds - before.Seconds),
                before.Moving + share * (next.Moving - before.Moving),
                reached.Climb,
                reached.Descent);
        }

        /// <summary>The first fix at or past a distance, found by bisection; the last fix when none is.</summary>
        private int FirstReaching(double meters)
        {
            var (low, high) = (0, _fixes.Count - 1);
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (_fixes[middle].Meters < meters) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        /// <summary>
        /// The climb and descent each fix confirms, through the dead band: the altitude must move
        /// <see cref="ClimbBand"/> from the last altitude counted before the change counts, and it is
        /// then counted whole. A fix with no altitude confirms nothing and moves nothing.
        /// </summary>
        private static IReadOnlyList<(double Up, double Down)> Elevation(IReadOnlyList<RoutePoint> points) =>
            points
                .Aggregate(
                    (Reference: (double?)null, Steps: new List<(double Up, double Down)>()),
                    (walk, point) =>
                    {
                        var (reference, steps) = walk;
                        if (point.Altitude is not { } altitude)
                        {
                            steps.Add((0, 0));
                            return (reference, steps);
                        }
                        if (reference is not { } turning)
                        {
                            steps.Add((0, 0));
                            return (altitude, steps);
                        }

                        var change = altitude - turning;
                        if (Math.Abs(change) < ClimbBand)
                        {
                            steps.Add((0, 0));
                            return (turning, steps);
                        }

                        steps.Add(change > 0 ? (change, 0) : (0, -change));
                        return (altitude, steps);
                    })
                .Steps;
    }
}
