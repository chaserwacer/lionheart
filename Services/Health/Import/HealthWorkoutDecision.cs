using lionheart.Model.Analysis;
using lionheart.Model.Health;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;
using lionheart.Services.Health.Recordings;

namespace lionheart.Services.Health.Import;

/// <summary>What one Apple Health workout becomes: a session with its recording, or nothing, for a reason.</summary>
public abstract record HealthWorkoutDecision
{
    private HealthWorkoutDecision() { }

    /// <param name="Source">The workout the session was imported from — the session's own source, held typed.</param>
    public sealed record Import(CompletedTrainingSession Session, WorkoutRecording Recording, SessionSource.AppleHealth Source)
        : HealthWorkoutDecision;

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
/// <item>Runs, rides, hikes and walks are taken, as <see cref="Taken"/> says; anything else is
/// left out.</item>
/// <item>One with no distance is left out: nothing measures it.</item>
/// <item>One lying at least half inside a session already in the log is that session, by the same
/// rule the cardio report holds the ring's workouts to (<see cref="SessionMatching"/>). The log
/// includes workouts imported earlier, so a ride two apps both wrote to Health is taken once —
/// whichever the phone offered first.</item>
/// </list>
/// What is left is a completed session holding one set — a trail run for an outdoor run, an
/// endurance set for everything else — with the route, heart rate and metrics beside it.
/// </remarks>
public static class HealthWorkoutDecisions
{
    /// <summary>
    /// The bundle identifiers, by prefix, of the apps whose workouts Lionheart already reads by its
    /// own route: Oura through its API, Strong through its export.
    /// </summary>
    public static readonly IReadOnlyList<string> ReadDirectly = ["com.ouraring.", "io.strongapp."];

    public static HealthWorkoutDecision Decide(
        StatedHealthWorkout workout, IReadOnlyList<CompletedTrainingSession> logged, NewSessionIdentity identity)
    {
        if (ReadDirectly.Any(prefix => workout.Source.RecordedBy.BundleId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return Excluded(HealthWorkoutExclusion.ReadDirectly);
        if (Taken(workout.Activity, workout.Indoor) is not { } taken)
            return Excluded(HealthWorkoutExclusion.OtherActivity);
        if (DistanceMeters(workout) is not { } distance)
            return Excluded(HealthWorkoutExclusion.NoDistance);

        return SessionMatching.Find((workout.Period.Start, workout.Period.End), logged) switch
        {
            SessionMatch.LoggedRun run => Excluded(HealthWorkoutExclusion.SameAsLoggedRun, run.TrainingSessionId),
            SessionMatch.LoggedLifting lifting => Excluded(HealthWorkoutExclusion.InsideLoggedLifting, lifting.TrainingSessionId),
            SessionMatch.None => new HealthWorkoutDecision.Import(
                Session(workout, Set(taken, workout, distance), identity),
                WorkoutRecording.Of(identity.TrainingSessionId, identity.UserId, workout.Route, workout.HeartRate, workout.Metrics),
                workout.Source),
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }

    /// <summary>
    /// What a workout of Health's activity becomes, or nothing for an activity Lionheart does not take.
    /// </summary>
    /// <remarks>
    /// An outdoor run is a trail run, competing on trail running. A ride outdoors and a hike are
    /// endurance sets that compete on riding and hiking. Indoors — a trainer, a treadmill — a ride or
    /// a run is training without a record, since its distance is the machine's estimate rather than
    /// ground covered. A walk is training and never a record, wherever it was. Elliptical, stairs and
    /// the rest are left out: a handful each, with no distance that means anything, and the ring's
    /// own workout still carries any of them into the cardio report.
    /// </remarks>
    public static TakenAs? Taken(string activity, bool indoor) => (activity.ToLowerInvariant(), indoor) switch
    {
        ("running", false) => new TakenAs.TrailRun(),
        ("running", true) => new TakenAs.Endurance(EnduranceActivity.IndoorRun),
        ("cycling", false) => new TakenAs.Endurance(EnduranceActivity.Ride),
        ("cycling", true) => new TakenAs.Endurance(EnduranceActivity.IndoorRide),
        ("hiking", _) => new TakenAs.Endurance(EnduranceActivity.Hike),
        ("walking", _) => new TakenAs.Endurance(EnduranceActivity.Walk),
        _ => null,
    };

    /// <summary>
    /// The climb a workout is recorded with: the recording app's own figure where it stated one,
    /// since it may have read a barometer or a terrain model, and otherwise what the route's
    /// altitudes show.
    /// </summary>
    /// <remarks>To a tenth of a metre, the precision a climb is stated to anywhere.</remarks>
    public static double Climb(StatedHealthWorkout workout) =>
        Math.Round(workout.ElevationAscendedMeters ?? RouteAnalysis.Figures(workout.Route).ClimbMeters ?? 0, 1);

    /// <summary>The app's stated distance, else the route's length; nothing when neither has any.</summary>
    private static double? DistanceMeters(StatedHealthWorkout workout) =>
        workout.DistanceMeters is > 0 and var stated ? stated
        : RouteAnalysis.Figures(workout.Route).Meters is > 0 and var measured ? measured
        : null;

    private static SetEntry Set(TakenAs taken, StatedHealthWorkout workout, double distanceMeters)
    {
        var kilometres = distanceMeters / DistanceUnit.Kilometers.MetersEach();
        var duration = workout.Duration > TimeSpan.Zero ? workout.Duration : workout.Period.Duration;
        return taken switch
        {
            TakenAs.TrailRun => new TrailRunSet(kilometres, DistanceUnit.Kilometers, duration, Climb(workout), Rpe.Unrated)
            {
                Source = new Formed(),
                Facets = [],
            },
            TakenAs.Endurance endurance => new EnduranceSet(
                endurance.Activity, kilometres, DistanceUnit.Kilometers, duration, Climb(workout), Rpe.Unrated)
            {
                Source = new Formed(),
                Facets = [],
            },
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }

    private static CompletedTrainingSession Session(StatedHealthWorkout workout, SetEntry set, NewSessionIdentity identity) =>
        new([set], workout.Period, new EffortNotReported(), new NotDecorated())
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

/// <summary>The set a Health workout Lionheart takes becomes.</summary>
public abstract record TakenAs
{
    private TakenAs() { }

    /// <summary>An outdoor run: a <see cref="TrailRunSet"/>.</summary>
    public sealed record TrailRun : TakenAs;

    /// <summary>Anything else taken: an <see cref="EnduranceSet"/> of this activity.</summary>
    public sealed record Endurance(EnduranceActivity Activity) : TakenAs;
}
