using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Sessions;

/// <summary>
/// Where a session's record came from: the athlete's own log, or a workout another app recorded
/// and Apple Health holds.
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
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AthleteLog), "athleteLog")]
[JsonDerivedType(typeof(AppleHealth), "appleHealth")]
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
}

/// <summary>An app that wrote a workout to Apple Health, as Health names it.</summary>
/// <param name="Name">What the app calls itself — "Strava".</param>
/// <param name="BundleId">Its bundle identifier, which is what eligibility is decided on, since a name is localised.</param>
public sealed record RecordingApp(string Name, string BundleId)
{
    public string Name { get; init; } = Require.Present(Name, nameof(Name));
    public string BundleId { get; init; } = Require.Present(BundleId, nameof(BundleId));
}
