using System.Globalization;
using lionheart.Model;
using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Health.Recordings;

/// <summary>
/// A recorded workout as the chat model reads it: one block of text per workout, every figure with
/// its unit.
/// </summary>
/// <remarks>
/// <b>No coordinate is ever written here.</b> The route is where the athlete was, so the model is
/// given what was derived from it — distance, time, pace, climb, splits — and never the path.
/// The heart rate says whose it is, since a strap's and the ring's are not the same instrument.
/// Every figure is in the units the athlete reads (<see cref="Imperial"/>), because the model
/// repeats them to him: a ride in mph, everything else in minutes per mile, climb in feet, splits per
/// mile.
/// </remarks>
public static class WorkoutFacts
{
    /// <summary>
    /// The context card's line: how many of each recorded workout — from Health or the Strava
    /// archive — the window holds, how far they went, and the latest of each, read off the sessions
    /// alone, so building the card reads no route.
    /// </summary>
    /// <param name="workouts">The imported workouts in the window, any order.</param>
    public static string Recent(IReadOnlyList<CompletedTrainingSession> workouts, int days)
    {
        if (workouts.Count is 0) return $"Recorded workouts: none in the last {days} days.";

        var byActivity = workouts
            .GroupBy(WorkoutReadouts.ActivityOf)
            .OrderBy(group => group.Key)
            .ToList();

        var counts = byActivity.Select(group =>
            $"{Counted(group.Count(), group.Key)} {Imperial.Miles(group.Sum(workout => Measured(workout).Meters))}");
        var latest = byActivity.Select(group => Latest(group.Key, group.MaxBy(workout => workout.Period.StartTimeUtc)!));

        return $"Recorded workouts in the last {days} days: {string.Join(", ", counts)}; {string.Join("; ", latest)}.";
    }

    private static string Latest(RecordedActivity activity, CompletedTrainingSession workout)
    {
        var (meters, duration, climb) = Measured(workout);
        return $"latest {Noun(activity)} {workout.Period.StartTime:yyyy-MM-dd}, {Imperial.Miles(meters)} in {Clock(duration)}"
               + (meters > 0 ? $", {Rate(activity, duration * (Imperial.MetersPerMile / meters))}" : string.Empty)
               + $", climb {Imperial.Feet(climb)}";
    }

    private static (double Meters, TimeSpan Duration, double Climb) Measured(CompletedTrainingSession workout)
    {
        var covered = WorkoutReadouts.Covered(workout);
        return (covered.Sum(set => set.Meters),
                covered.Aggregate(TimeSpan.Zero, (total, set) => total + set.Duration),
                covered.Sum(set => set.Climb));
    }

    public static string Summary(RecordedWorkoutView workout)
    {
        var lines = new List<string>
        {
            $"{Capitalised(Noun(workout.Activity))} {workout.Start:yyyy-MM-dd HH:mm} ({Provenance(workout.Source)}): "
            + $"{Imperial.Miles(workout.DistanceMeters)} in {Clock(workout.Duration)}"
            + (workout.MovingTime is { } moving ? $", moving {Clock(moving)}" : string.Empty)
            + (workout.Pace is { } pace ? $", {Rate(workout.Activity, Imperial.PerMile(pace))}" : string.Empty)
            + (workout.MovingPace is { } movingPace ? $" (moving {Rate(workout.Activity, Imperial.PerMile(movingPace))})" : string.Empty)
            + $", climb {Imperial.Feet(workout.ClimbMeters)}"
            + (workout.DescentMeters is { } descent ? $", descent {Imperial.Feet(descent)}" : string.Empty)
            + ".",
            HeartRate(workout),
        };

        lines.AddRange(Measures(workout));
        if (workout.Miles.Count > 0)
            lines.Add("Splits per mile: " + string.Join("; ", workout.Miles.Select(split => Split(workout.Activity, split))) + ".");

        return string.Join("\n", lines);
    }

    /// <summary>The app's own averages and the heart rate's recovery, as one line; none where it wrote neither.</summary>
    private static IEnumerable<string> Measures(RecordedWorkoutView workout)
    {
        var parts = new List<string>();
        var averages = workout.Metrics.Where(metric => metric.Metric is not WorkoutMetric.HeartRateRecovery).ToList();
        if (averages.Count > 0)
            parts.Add("averages " + string.Join(", ", averages.Select(metric =>
                $"{MetricName(metric.Metric)} {Figure(metric.Metric, metric.Average)} (peak {Figure(metric.Metric, metric.Peak)})")));
        if (workout.Metrics.FirstOrDefault(metric => metric.Metric is WorkoutMetric.HeartRateRecovery) is { } recovery)
            parts.Add($"heart rate fell {Figure(recovery.Metric, recovery.Average)} in the minute after");

        return parts.Count is 0 ? [] : ["Measured by the app: " + string.Join("; ", parts) + "."];
    }

    private static string MetricName(WorkoutMetric metric) => metric switch
    {
        WorkoutMetric.Power => "power",
        WorkoutMetric.Cadence => "cadence",
        WorkoutMetric.Speed => "speed",
        WorkoutMetric.GroundContactTime => "ground contact time",
        WorkoutMetric.VerticalOscillation => "vertical oscillation",
        WorkoutMetric.StrideLength => "stride length",
        WorkoutMetric.HeartRateRecovery => "heart-rate recovery",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Figure(WorkoutMetric metric, double value) => metric switch
    {
        WorkoutMetric.Power => $"{value.ToString("0", CultureInfo.InvariantCulture)} W",
        WorkoutMetric.Cadence => $"{value.ToString("0", CultureInfo.InvariantCulture)} rpm",
        WorkoutMetric.Speed => Imperial.Speed(metersPerSecond: value),
        WorkoutMetric.GroundContactTime => $"{value.ToString("0", CultureInfo.InvariantCulture)} ms",
        WorkoutMetric.VerticalOscillation => $"{(value / 100 / Imperial.MetersPerInch).ToString("0.0", CultureInfo.InvariantCulture)} in",
        WorkoutMetric.StrideLength => $"{(value / Imperial.MetersPerFoot).ToString("0.0", CultureInfo.InvariantCulture)} ft",
        WorkoutMetric.HeartRateRecovery => $"{value.ToString("0", CultureInfo.InvariantCulture)} bpm",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Noun(RecordedActivity activity) => activity switch
    {
        RecordedActivity.Run => "run",
        RecordedActivity.Ride => "ride",
        RecordedActivity.IndoorRide => "indoor ride",
        RecordedActivity.Hike => "hike",
        RecordedActivity.Walk => "walk",
        RecordedActivity.IndoorRun => "indoor run",
        RecordedActivity.EBikeRide => "e-bike ride",
        RecordedActivity.Paddle => "paddle",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Counted(int count, RecordedActivity activity) => $"{count} {Noun(activity)}{(count == 1 ? string.Empty : "s")}";

    private static string Capitalised(string noun) => char.ToUpperInvariant(noun[0]) + noun[1..];

    /// <summary>A ride's speed in mph, anything else's pace per mile, from the time per mile.</summary>
    private static string Rate(RecordedActivity activity, TimeSpan perMile) =>
        activity is RecordedActivity.Ride or RecordedActivity.IndoorRide or RecordedActivity.EBikeRide or RecordedActivity.Paddle
            ? $"speed {Imperial.Speed(perMile)}"
            : $"pace {Imperial.Pace(perMile)}";

    private static string HeartRate(RecordedWorkoutView workout) => workout.HeartRate switch
    {
        SessionHeartRate.Recorded recorded =>
            $"Heart rate ({Whose(recorded.Source, workout)}): average {recorded.AverageBpm} bpm, peak {recorded.PeakBpm} bpm, "
            + $"covering {recorded.Coverage:P0} of it"
            + (recorded.Zones is { } zones
                ? $"; minutes in zones 1–5 {Minutes(zones.Zone1)}, {Minutes(zones.Zone2)}, {Minutes(zones.Zone3)}, "
                  + $"{Minutes(zones.Zone4)}, {Minutes(zones.Zone5)} (below zone 1 {Minutes(zones.BelowZones)}), "
                  + $"Edwards load {zones.Load.ToString("0", CultureInfo.InvariantCulture)}"
                : string.Empty)
            + ".",
        SessionHeartRate.NotRecorded => "Heart rate: none recorded by the app or the ring across it.",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Whose(HeartRateSource source, RecordedWorkoutView workout) => source switch
    {
        HeartRateSource.RecordingApp => $"from {Recorder(workout.Source)}{Route(workout.Source)}",
        HeartRateSource.Ring => "from the Oura ring's all-day series; the recording app wrote none",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Provenance(SessionSource source) => source switch
    {
        SessionSource.AppleHealth health => $"recorded by {health.RecordedBy.Name}, imported from Apple Health",
        SessionSource.StravaArchive => "recorded in Strava, imported from the Strava archive",
        _ => throw new System.Diagnostics.UnreachableException("Only a recorded workout has a readout."),
    };

    private static string Recorder(SessionSource source) => source switch
    {
        SessionSource.AppleHealth health => health.RecordedBy.Name,
        SessionSource.StravaArchive => "Strava",
        _ => throw new System.Diagnostics.UnreachableException("Only a recorded workout has a readout."),
    };

    private static string Route(SessionSource source) => source switch
    {
        SessionSource.AppleHealth => " via Apple Health",
        SessionSource.StravaArchive => " in the Strava archive",
        _ => throw new System.Diagnostics.UnreachableException("Only a recorded workout has a readout."),
    };

    private static string Split(RecordedActivity activity, Split split) =>
        $"{split.Number}: {Imperial.Miles(split.DistanceMeters)} at {Rate(activity, split.Pace)}"
        + (split.ClimbMeters is { } climb && split.DescentMeters is { } descent ? $", +{Imperial.Feet(climb)} −{Imperial.Feet(descent)}" : string.Empty)
        + (split.AverageBpm is { } bpm ? $", {bpm} bpm" : string.Empty);

    private static string Minutes(double minutes) => minutes.ToString("0", CultureInfo.InvariantCulture);

    private static string Clock(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
