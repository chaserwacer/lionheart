using System.Globalization;
using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Health.Runs;

/// <summary>
/// A recorded run as the chat model reads it: one block of text per run, every figure with its unit.
/// </summary>
/// <remarks>
/// <b>No coordinate is ever written here.</b> The route is where the athlete was, so the model is
/// given what was derived from it — distance, time, pace, climb, splits — and never the path.
/// The heart rate says whose it is, since a strap's and the ring's are not the same instrument.
/// </remarks>
public static class RunFacts
{
    /// <summary>
    /// The context card's line: how many runs came from Health over the window, how far they went,
    /// and the latest — read off the sessions alone, so building the card reads no route.
    /// </summary>
    /// <param name="runs">The imported runs in the window, any order.</param>
    public static string Recent(IReadOnlyList<CompletedTrainingSession> runs, int days)
    {
        if (runs.Count is 0) return $"Runs from Apple Health: none in the last {days} days.";

        var latest = runs.MaxBy(run => run.Period.StartTimeUtc)!;
        var (meters, duration, climb) = Measured(latest);
        return $"Runs from Apple Health: {runs.Count} in the last {days} days, {Kilometres(runs.Sum(run => Measured(run).Meters))} in all; "
               + $"latest {latest.Period.StartTime:yyyy-MM-dd}, {Kilometres(meters)} in {Clock(duration)}"
               + (meters > 0 ? $", pace {Pace(duration * (1000 / meters))}/km" : string.Empty)
               + $", climb {Metres(climb)}.";
    }

    private static (double Meters, TimeSpan Duration, double Climb) Measured(CompletedTrainingSession run)
    {
        var sets = run.Sets.OfType<TrailRunSet>().ToList();
        return (sets.Sum(set => set.Distance * set.Unit.MetersEach()),
                sets.Aggregate(TimeSpan.Zero, (total, set) => total + set.Duration),
                sets.Sum(set => set.ElevationGainMeters));
    }

    public static string Summary(RecordedRunView run)
    {
        var lines = new List<string>
        {
            $"Run {run.Start:yyyy-MM-dd HH:mm} (recorded by {run.RecordedBy.Name}, imported from Apple Health): "
            + $"{Kilometres(run.DistanceMeters)} in {Clock(run.Duration)}"
            + (run.MovingTime is { } moving ? $", moving {Clock(moving)}" : string.Empty)
            + (run.Pace is { } pace ? $", pace {Pace(pace)}/km" : string.Empty)
            + (run.MovingPace is { } movingPace ? $" (moving {Pace(movingPace)}/km)" : string.Empty)
            + $", climb {Metres(run.ClimbMeters)}"
            + (run.DescentMeters is { } descent ? $", descent {Metres(descent)}" : string.Empty)
            + ".",
            HeartRate(run),
        };

        if (run.Kilometres.Count > 0)
            lines.Add("Splits per km: " + string.Join("; ", run.Kilometres.Select(Split)) + ".");

        return string.Join("\n", lines);
    }

    private static string HeartRate(RecordedRunView run) => run.HeartRate switch
    {
        SessionHeartRate.Recorded recorded =>
            $"Heart rate ({Whose(recorded.Source, run)}): average {recorded.AverageBpm} bpm, peak {recorded.PeakBpm} bpm, "
            + $"covering {recorded.Coverage:P0} of the run"
            + (recorded.Zones is { } zones
                ? $"; minutes in zones 1–5 {Minutes(zones.Zone1)}, {Minutes(zones.Zone2)}, {Minutes(zones.Zone3)}, "
                  + $"{Minutes(zones.Zone4)}, {Minutes(zones.Zone5)} (below zone 1 {Minutes(zones.BelowZones)}), "
                  + $"Edwards load {zones.Load.ToString("0", CultureInfo.InvariantCulture)}"
                : string.Empty)
            + ".",
        SessionHeartRate.NotRecorded => "Heart rate: none recorded by the app or the ring across the run.",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Whose(HeartRateSource source, RecordedRunView run) => source switch
    {
        HeartRateSource.AppleHealth => $"from {run.RecordedBy.Name} via Apple Health",
        HeartRateSource.Ring => "from the Oura ring's all-day series; the recording app wrote none",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Split(Split split) =>
        $"{split.Number}: {Kilometres(split.DistanceMeters)} at {Pace(split.Pace)}/km"
        + (split.ClimbMeters is { } climb && split.DescentMeters is { } descent ? $", +{Metres(climb)} −{Metres(descent)}" : string.Empty)
        + (split.AverageBpm is { } bpm ? $", {bpm} bpm" : string.Empty);

    private static string Kilometres(double meters) =>
        $"{(meters / 1000).ToString("0.00", CultureInfo.InvariantCulture)} km";

    private static string Metres(double meters) => $"{meters.ToString("0", CultureInfo.InvariantCulture)} m";

    private static string Minutes(double minutes) => minutes.ToString("0", CultureInfo.InvariantCulture);

    private static string Clock(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    private static string Pace(TimeSpan pace)
    {
        var seconds = (int)Math.Round(pace.TotalSeconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}
