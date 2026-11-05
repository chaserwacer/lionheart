using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Sessions;

/// <summary>
/// Where a session's record came from: the athlete's own log, a workout another app recorded and
/// Apple Health holds, or an activity in the athlete's Strava archive.
/// </summary>
/// <remarks>
/// Orthogonal to <see cref="Provenance"/>, which says who authored the content. An imported run is
/// the athlete's own training, written by the app he recorded it with, so it is
/// <see cref="Provenance.User"/> and <see cref="AppleHealth"/> at once.
/// <para>
/// <see cref="AthleteLog"/> covers everything entered through Lionheart and everything imported
/// from his own logs (Strong, the coached sheets), because those are his log restated; a Health
/// workout is a device's recording, which is a different kind of record — it has a route and a
/// heart-rate trace beside it, and an identity in Health that a later import is matched on.
/// </para>
/// <para>
/// <see cref="StravaArchive"/> is a Strava activity read from the archive Strava exports, for the
/// activities that never reached Health. It is its own case rather than an
/// <see cref="AppleHealth"/> workout with a Strava id, because it has no Health identity and the
/// archive, not Health, is what a later run of its import is matched on.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AthleteLog), "athleteLog")]
[JsonDerivedType(typeof(AppleHealth), "appleHealth")]
[JsonDerivedType(typeof(StravaArchive), "stravaArchive")]
public abstract record SessionSource
{
    private protected SessionSource() { }

    public sealed record AthleteLog : SessionSource;

    /// <param name="WorkoutId">The workout's UUID in Apple Health, which an import is idempotent on.</param>
    /// <param name="RecordedBy">The app that wrote the workout to Health.</param>
    public sealed record AppleHealth : SessionSource
    {
        public AppleHealth(Guid workoutId, RecordingApp recordedBy)
        {
            if (workoutId == Guid.Empty)
                throw new ArgumentException("An Apple Health workout has a real UUID.", nameof(workoutId));

            WorkoutId = workoutId;
            RecordedBy = Require.Present(recordedBy, nameof(recordedBy));
        }

        public Guid WorkoutId { get; }
        public RecordingApp RecordedBy { get; }
    }

    /// <param name="ActivityId">The activity's id in Strava, which its import is idempotent on.</param>
    public sealed record StravaArchive : SessionSource
    {
        public StravaArchive(long activityId)
        {
            if (activityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(activityId), activityId, "A Strava activity has a positive id.");

            ActivityId = activityId;
        }

        public long ActivityId { get; }
    }
}

public static class SessionSources
{
    extension(SessionSource source)
    {
        /// <summary>
        /// Whether the session is a device's recording — imported from Apple Health or the Strava
        /// archive — whose route and heart rate may sit beside it in a <c>WorkoutRecording</c>.
        /// </summary>
        public bool IsRecording => source is SessionSource.AppleHealth or SessionSource.StravaArchive;
    }
}

/// <summary>An app that wrote a workout to Apple Health, as Health names it.</summary>
/// <param name="Name">What the app calls itself — "Strava".</param>
/// <param name="BundleId">Its bundle identifier, which is what eligibility is decided on, since a name is localised.</param>
public sealed record RecordingApp(string Name, string BundleId)
{
    public string Name { get; init; } = Require.Present(Name, nameof(Name));
    public string BundleId { get; init; } = Require.Present(BundleId, nameof(BundleId));
}
