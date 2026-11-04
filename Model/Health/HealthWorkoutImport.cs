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
/// what it reads to running workouts, since a route is the expensive part to send.
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
    IReadOnlyList<HeartRateCommand> HeartRate)
{
    /// <summary>The workout as the domain holds it, or the first thing about it that cannot be read.</summary>
    public Result<StatedHealthWorkout, Problem> Stated() =>
        Identity()
            .Bind(_ => Times())
            .Bind(period => Measures().Map(_ => period))
            .Bind(period => Path().Bind(route => Pulse().Map(heartRate => new StatedHealthWorkout(
                new SessionSource.AppleHealth(WorkoutId, new RecordingApp(AppName.Trim(), AppBundleId.Trim())),
                Activity.Trim(),
                Indoor,
                period,
                Duration,
                DistanceMeters,
                ElevationAscendedMeters,
                route,
                heartRate))));

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

    private Result<RecordedHeartRate, Problem> Pulse() =>
        (HeartRate ?? []).Traverse(sample => sample.ToReading())
            .Map(readings => RecordedHeartRate.Of(readings));

    private static Result<Unit, Problem> Refused(string field, string message) =>
        Result<Unit, Problem>.Failure(new Problem.Invalid(field, message));
}

/// <param name="Altitude">Metres above sea level, or absent where the fix had no valid altitude.</param>
public sealed record RoutePointCommand(DateTimeOffset At, double Latitude, double Longitude, double? Altitude)
{
    /// <summary>
    /// A millionth of a degree, about 11 cm — finer than any phone's fix, and what keeps a stored
    /// coordinate to ten characters rather than seventeen.
    /// </summary>
    private const int CoordinateDecimals = 6;

    /// <summary>A tenth of a metre, finer than the altitude a phone reports.</summary>
    private const int AltitudeDecimals = 1;

    public Result<RoutePoint, Problem> ToPoint() =>
        Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180
        && (Altitude is null || double.IsFinite(Altitude.Value))
            ? Result<RoutePoint, Problem>.Success(new RoutePoint(
                At,
                Math.Round(Latitude, CoordinateDecimals),
                Math.Round(Longitude, CoordinateDecimals),
                Altitude is { } altitude ? Math.Round(altitude, AltitudeDecimals) : null))
            : Result<RoutePoint, Problem>.Failure(new Problem.Invalid(
                nameof(HealthWorkoutImport.Route), $"The fix at {At:O} is not a place on Earth."));
}

public sealed record HeartRateCommand(DateTimeOffset At, int Bpm)
{
    /// <summary>The widest a human heart rate is, so a sensor's zero or a unit mistake is refused rather than averaged.</summary>
    private const int Highest = 260;

    public Result<(DateTimeOffset At, int Bpm), Problem> ToReading() =>
        Bpm is > 0 and <= Highest
            ? Result<(DateTimeOffset At, int Bpm), Problem>.Success((At, Bpm))
            : Result<(DateTimeOffset At, int Bpm), Problem>.Failure(new Problem.Invalid(
                nameof(HealthWorkoutImport.HeartRate), $"{Bpm} bpm at {At:O} is not a heart rate."));
}

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
    RecordedHeartRate HeartRate);
