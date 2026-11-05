using lionheart.Functional;
using lionheart.Model.Training.Sessions;

namespace lionheart.Model.Health;

/// <summary>
/// One Apple Health workout as the phone read it, offered to the server to import.
/// </summary>
/// <remarks>
/// The phone sends what Health holds and decides nothing: which workouts become training, and
/// which are already in the log under another name, is the server's rule
/// (<c>HealthWorkoutDecision</c>), so it is decided once and tested once. The phone does narrow
/// what it reads to the activities the rule takes, since a route is the expensive part to send.
/// <para>
/// Flat scalars rather than domain records, for the reason <see cref="Analysis.PeriodCommand"/>
/// gives: a domain constructor that throws while the body is being deserialized escapes as a 500,
/// and a malformed workout is a refusal. <see cref="Stated"/> is where it becomes one.
/// </para>
/// </remarks>
/// <param name="WorkoutId">The workout's UUID in Health — what makes a second import of it add nothing.</param>
/// <param name="Activity">Health's activity type in its own spelling: "running", "walking", "cycling".</param>
/// <param name="Indoor">Whether the recording app marked it indoor — a treadmill, for a run.</param>
/// <param name="AppName">The app that wrote the workout to Health, as Health names it.</param>
/// <param name="AppBundleId">That app's bundle identifier.</param>
/// <param name="Duration">The time the app counted, which excludes any pause it recorded.</param>
/// <param name="DistanceMeters">The distance the app stated, if it stated one.</param>
/// <param name="ElevationAscendedMeters">The climb the app stated, if it stated one.</param>
/// <param name="Route">Every fix of the route, oldest first; empty for a workout with none.</param>
/// <param name="HeartRate">The heart rate the same app wrote across the workout; empty for none.</param>
/// <param name="Metrics">
/// Everything else the same app measured, a series per metric: power, cadence, speed, running form,
/// and the heart rate's recovery after the workout. Absent from a phone that predates reading them.
/// </param>
public sealed record HealthWorkoutImport(
    Guid WorkoutId,
    string Activity,
    bool Indoor,
    string AppName,
    string AppBundleId,
    DateTimeOffset Start,
    DateTimeOffset End,
    TimeSpan Duration,
    double? DistanceMeters,
    double? ElevationAscendedMeters,
    IReadOnlyList<RoutePointCommand> Route,
    IReadOnlyList<HeartRateCommand> HeartRate,
    IReadOnlyList<MetricSeriesCommand>? Metrics = null)
{
    /// <summary>The workout as the domain holds it, or the first thing about it that cannot be read.</summary>
    public Result<StatedHealthWorkout, Problem> Stated() =>
        Identity()
            .Bind(_ => Times())
            .Bind(period => Measures().Map(_ => period))
            .Bind(period => Path().Map(route => new StatedHealthWorkout(
                new SessionSource.AppleHealth(WorkoutId, new RecordingApp(AppName.Trim(), AppBundleId.Trim())),
                Activity.Trim(),
                Indoor,
                period,
                Duration,
                DistanceMeters,
                ElevationAscendedMeters,
                route,
                Pulse(),
                Measured())));

    private Result<Unit, Problem> Identity() =>
        WorkoutId == Guid.Empty ? Refused(nameof(WorkoutId), "A workout carries its Health UUID.")
        : string.IsNullOrWhiteSpace(Activity) ? Refused(nameof(Activity), "A workout names its activity.")
        : string.IsNullOrWhiteSpace(AppName) || string.IsNullOrWhiteSpace(AppBundleId)
            ? Refused(nameof(AppBundleId), "A workout names the app that recorded it.")
        : Result<Unit, Problem>.Success(Unit.Value);

    private Result<Analysis.Interval, Problem> Times() =>
        End < Start
            ? Result<Analysis.Interval, Problem>.Failure(new Problem.Invalid(nameof(End), "The workout's end precedes its start."))
            : Result<Analysis.Interval, Problem>.Success(new Analysis.Interval(Start, End));

    private Result<Unit, Problem> Measures() =>
        Duration < TimeSpan.Zero ? Refused(nameof(Duration), "A duration is not negative.")
        : DistanceMeters is { } distance && !(double.IsFinite(distance) && distance >= 0)
            ? Refused(nameof(DistanceMeters), "A distance is a finite number of metres, not negative.")
        : ElevationAscendedMeters is { } climb && !(double.IsFinite(climb) && climb >= 0)
            ? Refused(nameof(ElevationAscendedMeters), "A climb is a finite number of metres, not negative.")
        : Result<Unit, Problem>.Success(Unit.Value);

    private Result<WorkoutRoute, Problem> Path() =>
        (Route ?? []).Traverse(point => point.ToPoint())
            .Bind(points => points.Zip(points.Skip(1)).Any(pair => pair.Second.At < pair.First.At)
                ? Result<WorkoutRoute, Problem>.Failure(new Problem.Invalid(nameof(Route), "A route's fixes run oldest first."))
                : Result<WorkoutRoute, Problem>.Success(new WorkoutRoute(points)));

    /// <summary>
    /// The heart rate, without any reading that cannot be one. A sensor's zero or a unit mistake is
    /// left out rather than averaged — and rather than refusing the run, which one glitch in an
    /// hour of samples would otherwise cost.
    /// </summary>
    private RecordedHeartRate Pulse() =>
        RecordedHeartRate.Of((HeartRate ?? []).Where(sample => sample.IsHeartRate).Select(sample => (sample.At, sample.Bpm)));

    /// <summary>
    /// The metrics this server knows, without a reading that cannot be one. A metric a newer phone
    /// names is passed over rather than refusing the workout, as an impossible reading is.
    /// </summary>
    private RecordedMetrics Measured() =>
        RecordedMetrics.Of(
            from series in Metrics ?? []
            let metric = WorkoutMetrics.Named(series.Metric)
            where metric is not null
            from sample in series.Samples ?? []
            select (metric.Value, sample.At, sample.Value));

    private static Result<Unit, Problem> Refused(string field, string message) =>
        Result<Unit, Problem>.Failure(new Problem.Invalid(field, message));
}

/// <param name="Altitude">Metres above sea level, or absent where the fix had no valid altitude.</param>
public sealed record RoutePointCommand(DateTimeOffset At, double Latitude, double Longitude, double? Altitude)
{
    public Result<RoutePoint, Problem> ToPoint() =>
        RoutePoint.Fix(At, Latitude, Longitude, Altitude)
            .MapError(Problem (reason) => new Problem.Invalid(nameof(HealthWorkoutImport.Route), reason));
}

public sealed record HeartRateCommand(DateTimeOffset At, int Bpm)
{
    public bool IsHeartRate => RecordedHeartRate.Admits(Bpm);
}

/// <param name="Metric">The metric's wire name: <c>power</c>, <c>cadence</c>, <c>speed</c>, <c>groundContactTime</c>…</param>
public sealed record MetricSeriesCommand(string Metric, IReadOnlyList<MetricSampleCommand> Samples);

public sealed record MetricSampleCommand(DateTimeOffset At, double Value);

/// <summary>A Health workout that has passed the wire: every field read, nothing yet decided.</summary>
public sealed record StatedHealthWorkout(
    SessionSource.AppleHealth Source,
    string Activity,
    bool Indoor,
    Analysis.Interval Period,
    TimeSpan Duration,
    double? DistanceMeters,
    double? ElevationAscendedMeters,
    WorkoutRoute Route,
    RecordedHeartRate HeartRate,
    RecordedMetrics Metrics);
