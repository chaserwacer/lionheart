using lionheart.Model.Training.Sets;

namespace lionheart.Services.Strava;

/// <summary>One activity as the archive's <c>activities.csv</c> sums it up.</summary>
/// <param name="Id">Strava's activity id, which the import is idempotent on.</param>
/// <param name="Start">When it began, as the instant Strava wrote (in UTC).</param>
/// <param name="Type">Strava's activity type in its own spelling: "Run", "Ride", "Weight Training".</param>
/// <param name="Elapsed">From start to finish, pauses included: what the session's period spans.</param>
/// <param name="Moving">The time Strava counted as moving; zero where it wrote none.</param>
/// <param name="DistanceMeters">The distance Strava stated; zero for an activity with none, as a gym session.</param>
/// <param name="ElevationGainMeters">The climb Strava stated, or absent where it wrote none.</param>
/// <param name="PerceivedExertion">The athlete's own 1–10 rating, where he gave one.</param>
/// <param name="AverageHeartRate">Strava's average over the activity, where a sensor gave one.</param>
/// <param name="MaxHeartRate">Its peak.</param>
/// <param name="File">The activity's file, relative to the archive; empty for an activity with none.</param>
public sealed record StravaActivity(
    long Id,
    DateTimeOffset Start,
    string Name,
    string Type,
    TimeSpan Elapsed,
    TimeSpan Moving,
    double DistanceMeters,
    double? ElevationGainMeters,
    string Description,
    string PrivateNote,
    Rpe PerceivedExertion,
    int? AverageHeartRate,
    int? MaxHeartRate,
    string File)
{
    /// <summary>The clock time it occupied, which is what the same-session rule compares.</summary>
    public (DateTimeOffset Start, DateTimeOffset End) Span => (Start, Start + Elapsed);
}
