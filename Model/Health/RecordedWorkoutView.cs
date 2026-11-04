using System.Text.Json.Serialization;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Sessions;

namespace lionheart.Model.Health;

/// <summary>
/// A workout imported from Apple Health, read for its own page: the session's figures, the route,
/// the splits, the heart rate across it, and whatever else the recording app measured.
/// </summary>
/// <remarks>
/// The distance, time and climb are the session's own set, so the page, the records and the cardio
/// report all read one figure, and a correction to the session moves all three. What only the route
/// can say — moving time, descent, the splits — is derived from it on every read rather than
/// stored, so a better derivation needs no migration.
/// <para>
/// Paces are per kilometre whatever the activity; a client showing a ride's speed, or miles,
/// converts, which is a change of unit and not a second derivation. Each split's pace is per its
/// own unit.
/// </para>
/// </remarks>
/// <param name="Activity">What the session is: a run, a ride, a hike, a walk, indoors or out.</param>
/// <param name="Duration">The time the recording app counted, which excludes a pause it recorded.</param>
/// <param name="MovingTime">The route's time in motion, or absent with no route whose clock can be read.</param>
/// <param name="Pace">The duration over the distance, per kilometre; absent with no distance.</param>
/// <param name="MovingPace">The moving time over the distance, per kilometre.</param>
/// <param name="ClimbMeters">The session's climb, as the recording app stated it or the route showed it.</param>
/// <param name="DescentMeters">The route's descent, or absent where it carried no altitude.</param>
/// <param name="HeartRate">What the trace says across the workout, the cardio report's own reading.</param>
/// <param name="Trace">The samples the heart rate was read from, for drawing.</param>
/// <param name="Route">The path, for the map. Coordinates only: the times stay on the server.</param>
/// <param name="Metrics">Each other metric the app wrote, in the order they are declared; empty for none.</param>
public sealed record RecordedWorkoutView(
    Guid TrainingSessionId,
    RecordingApp RecordedBy,
    DateTimeOffset Start,
    DateTimeOffset End,
    double DistanceMeters,
    TimeSpan Duration,
    TimeSpan? MovingTime,
    TimeSpan? Pace,
    TimeSpan? MovingPace,
    double ClimbMeters,
    double? DescentMeters,
    SessionHeartRate HeartRate,
    MaxHeartRate? MaxHeartRate,
    HeartRateTrace Trace,
    IReadOnlyList<Split> Kilometres,
    IReadOnlyList<Split> Miles,
    IReadOnlyList<Coordinate> Route,
    RecordedActivity Activity,
    IReadOnlyList<MetricReadout> Metrics);

/// <summary>What a recorded session is, as its page names it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecordedActivity>))]
public enum RecordedActivity
{
    [JsonStringEnumMemberName("run")] Run,
    [JsonStringEnumMemberName("ride")] Ride,
    [JsonStringEnumMemberName("indoorRide")] IndoorRide,
    [JsonStringEnumMemberName("hike")] Hike,
    [JsonStringEnumMemberName("walk")] Walk,
    [JsonStringEnumMemberName("indoorRun")] IndoorRun,
}

/// <summary>One metric across the workout: its mean, its highest, and the samples, for drawing.</summary>
/// <remarks>
/// The mean is over the samples as written, so a ride's average power counts the coasting, as a
/// power meter's own average does. Heart-rate recovery is one sample, which is its own mean.
/// </remarks>
public sealed record MetricReadout(WorkoutMetric Metric, double Average, double Peak, IReadOnlyList<MetricReading> Samples);

public sealed record MetricReading(DateTimeOffset At, double Value);

/// <summary>A place on the route.</summary>
public sealed record Coordinate(double Latitude, double Longitude);

/// <summary>The heart-rate samples across a workout, and whose they are.</summary>
public sealed record HeartRateTrace(HeartRateSource Source, IReadOnlyList<HeartRateReading> Samples);

public sealed record HeartRateReading(DateTimeOffset At, int Bpm);

/// <summary>
/// One stretch of a workout a unit long — a kilometre or a mile — or what is left after the last
/// whole one.
/// </summary>
/// <param name="Number">Counted from 1.</param>
/// <param name="Elapsed">Clock time across the split, a stop included.</param>
/// <param name="Moving">Time in motion across the split.</param>
/// <param name="Pace">Moving time per whole unit, so a short last split reads at the pace it was run.</param>
/// <param name="ClimbMeters">Absent where the route carried no altitude.</param>
/// <param name="AverageBpm">Absent where no heart rate falls inside the split.</param>
public sealed record Split(
    int Number,
    double DistanceMeters,
    TimeSpan Elapsed,
    TimeSpan Moving,
    TimeSpan Pace,
    double? ClimbMeters,
    double? DescentMeters,
    int? AverageBpm);
