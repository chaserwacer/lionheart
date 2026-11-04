using lionheart.Model.Analysis;
using lionheart.Model.Health;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;

namespace lionheart.Services.Health;

/// <summary>What one Apple Health workout becomes: a session with its recording, or nothing, for a reason.</summary>
public abstract record HealthWorkoutDecision
{
    private HealthWorkoutDecision() { }

    public sealed record Import(CompletedTrainingSession Session, WorkoutRecording Recording) : HealthWorkoutDecision;

    /// <param name="MatchedSessionId">The logged session the workout already is, where it matched one.</param>
    public sealed record Excluded(HealthWorkoutExclusion Reason, Guid? MatchedSessionId) : HealthWorkoutDecision;
}

/// <summary>The identity a session imported now would take.</summary>
public sealed record NewSessionIdentity(Guid TrainingSessionId, Guid UserId, DateTime CreatedAt);

/// <summary>
/// Which Health workouts become Lionheart training, and as what.
/// </summary>
/// <remarks>
/// Pure, so the rule is tested without a database, and the one place it is written: the phone
/// sends what Health holds and this decides. In order —
/// <list type="number">
/// <item>An app whose workouts Lionheart reads by its own route is left out, so Oura's workouts
/// (which Oura also writes to Health) and Strong's are never counted twice.</item>
/// <item>Only an outdoor run with a distance becomes a session — see
/// <see cref="HealthWorkoutExclusion"/> for why each other kind is left out.</item>
/// <item>A run lying at least half inside a session already in the log is that session, by the same
/// rule the cardio report holds the ring's workouts to (<see cref="SessionMatching"/>). The log
/// includes runs imported earlier, so a run two apps both wrote to Health is taken once — whichever
/// the phone offered first.</item>
/// </list>
/// What is left is a completed session holding one trail-run set — trail running being the record
/// subject for a run along a route whatever its surface — with the route and heart rate beside it.
/// </remarks>
public static class HealthWorkoutDecisions
{
    /// <summary>
    /// The bundle identifiers, by prefix, of the apps whose workouts Lionheart already reads by its
    /// own route: Oura through its API, Strong through its export.
    /// </summary>
    public static readonly IReadOnlyList<string> ReadDirectly = ["com.ouraring.", "io.strongapp."];

    /// <summary>Health's spelling of a run.</summary>
    public const string Running = "running";

    public static HealthWorkoutDecision Decide(
        StatedHealthWorkout workout, IReadOnlyList<CompletedTrainingSession> logged, NewSessionIdentity identity)
    {
        if (ReadDirectly.Any(prefix => workout.Source.RecordedBy.BundleId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return Excluded(HealthWorkoutExclusion.ReadDirectly);
        if (!string.Equals(workout.Activity, Running, StringComparison.OrdinalIgnoreCase))
            return Excluded(HealthWorkoutExclusion.NotARun);
        if (workout.Indoor)
            return Excluded(HealthWorkoutExclusion.Indoor);
        if (DistanceMeters(workout) is not { } distance)
            return Excluded(HealthWorkoutExclusion.NoDistance);

        return SessionMatching.Find((workout.Period.Start, workout.Period.End), logged) switch
        {
            SessionMatch.LoggedRun run => Excluded(HealthWorkoutExclusion.SameAsLoggedRun, run.TrainingSessionId),
            SessionMatch.LoggedLifting lifting => Excluded(HealthWorkoutExclusion.InsideLoggedLifting, lifting.TrainingSessionId),
            SessionMatch.None => new HealthWorkoutDecision.Import(
                Session(workout, distance, identity),
                WorkoutRecording.Of(identity.TrainingSessionId, identity.UserId, workout.Route, workout.HeartRate)),
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }

    /// <summary>
    /// The climb a run is recorded with: the recording app's own figure where it stated one, since
    /// it may have read a barometer or a terrain model, and otherwise what the route's altitudes show.
    /// </summary>
    public static double Climb(StatedHealthWorkout workout) =>
        workout.ElevationAscendedMeters ?? RouteAnalysis.Figures(workout.Route).ClimbMeters ?? 0;

    /// <summary>The app's stated distance, else the route's length; nothing when neither has any.</summary>
    private static double? DistanceMeters(StatedHealthWorkout workout) =>
        workout.DistanceMeters is > 0 and var stated ? stated
        : RouteAnalysis.Figures(workout.Route).Meters is > 0 and var measured ? measured
        : null;

    private static CompletedTrainingSession Session(
        StatedHealthWorkout workout, double distanceMeters, NewSessionIdentity identity) =>
        new(
            [
                new TrailRunSet(
                    distanceMeters / DistanceUnit.Kilometers.MetersEach(),
                    DistanceUnit.Kilometers,
                    workout.Duration > TimeSpan.Zero ? workout.Duration : workout.Period.Duration,
                    Climb(workout),
                    Rpe.Unrated)
                {
                    Source = new Formed(),
                    Facets = [],
                },
            ],
            workout.Period,
            new EffortNotReported(),
            new NotDecorated())
        {
            TrainingSessionId = identity.TrainingSessionId,
            UserId = identity.UserId,
            Notes = string.Empty,
            Provenance = Provenance.User,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            Source = workout.Source,
            CreatedAt = identity.CreatedAt,
        };

    private static HealthWorkoutDecision Excluded(HealthWorkoutExclusion reason, Guid? matched = null) =>
        new HealthWorkoutDecision.Excluded(reason, matched);
}
