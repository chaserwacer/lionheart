using lionheart.Model.Cardio;
using lionheart.Model.Training.Sessions;

namespace lionheart.Model.Health;

/// <summary>
/// A run imported from Apple Health, read for its own page: the session's figures, the route, the
/// splits and the heart rate across it.
/// </summary>
/// <remarks>
/// The distance, time and climb are the session's own trail-run set, so the page, the records and
/// the cardio report all read one figure, and a correction to the session moves all three. What
/// only the route can say — moving time, descent, the splits — is derived from it on every read
/// rather than stored, so a better derivation needs no migration.
/// <para>
/// Paces are per kilometre; a client showing miles converts, which is a change of unit and not a
/// second derivation. Each split's pace is per its own unit.
/// </para>
/// </remarks>
/// <param name="Duration">The time the recording app counted, which excludes a pause it recorded.</param>
/// <param name="MovingTime">The route's time in motion, or absent with no route to read it from.</param>
/// <param name="Pace">The duration over the distance, per kilometre; absent with no distance.</param>
/// <param name="MovingPace">The moving time over the distance, per kilometre.</param>
/// <param name="ClimbMeters">The session's climb, as the recording app stated it or the route showed it.</param>
/// <param name="DescentMeters">The route's descent, or absent where it carried no altitude.</param>
/// <param name="HeartRate">What the trace says across the run, the cardio report's own reading.</param>
/// <param name="Trace">The samples the heart rate was read from, for drawing.</param>
/// <param name="Route">The path, for the map. Coordinates only: the times stay on the server.</param>
public sealed record RecordedRunView(
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
    IReadOnlyList<Coordinate> Route);

/// <summary>A place on the route.</summary>
public sealed record Coordinate(double Latitude, double Longitude);

/// <summary>The heart-rate samples across a run, and whose they are.</summary>
public sealed record HeartRateTrace(HeartRateSource Source, IReadOnlyList<HeartRateReading> Samples);

public sealed record HeartRateReading(DateTimeOffset At, int Bpm);

/// <summary>
/// One stretch of a run a unit long — a kilometre or a mile — or what is left after the last
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
