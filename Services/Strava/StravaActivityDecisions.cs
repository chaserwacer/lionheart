using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Cardio;
using lionheart.Services.Health.Recordings;
using lionheart.Services.Strava.Files;

namespace lionheart.Services.Strava;

/// <summary>What one archived activity becomes: a new session, a session already in the log, or nothing, for a reason.</summary>
public abstract record StravaActivityDecision
{
    private StravaActivityDecision() { }

    /// <summary>A new session, with the recording its file held beside it.</summary>
    public sealed record Added(CompletedTrainingSession Session, WorkoutRecording Recording) : StravaActivityDecision;

    /// <summary>A new gym session. Strava holds no route for one, and its heart rate goes into the notes.</summary>
    public sealed record AddedLifting(CompletedTrainingSession Session) : StravaActivityDecision;

    /// <summary>An earlier run of the import made this activity a session.</summary>
    public sealed record AlreadyImported(Guid TrainingSessionId) : StravaActivityDecision;

    /// <summary>The activity is a session the log already holds — imported from Health, or logged — by the same-session rule.</summary>
    public sealed record Matched(SessionMatch Match) : StravaActivityDecision;

    /// <summary>Not training Lionheart takes from the archive, for the reason given.</summary>
    public sealed record LeftOut(StravaExclusion Reason) : StravaActivityDecision;
}

/// <summary>Why an archived activity did not become a session and is in the log as none.</summary>
public enum StravaExclusion
{
    /// <summary>Its session was imported once and the athlete deleted it.</summary>
    Deleted,

    /// <summary>A type Lionheart does not take: a swim, yoga, a ski day.</summary>
    NotTaken,

    /// <summary>A run, ride or other distance activity with neither a stated distance nor a route to measure one from.</summary>
    NoDistance,
}

/// <summary>What the log held when an activity was decided, and how a new session is placed.</summary>
/// <param name="Logged">Every completed session, including those this run added before the activity.</param>
/// <param name="Deleted">The ids of the archived activities whose sessions the athlete deleted.</param>
public sealed record ArchiveLog(
    IReadOnlyList<CompletedTrainingSession> Logged,
    IReadOnlySet<Guid> HeldIds,
    IReadOnlySet<long> Deleted,
    ArchivePlacement Placement);

/// <summary>Which archived activities become Lionheart training, and as what.</summary>
/// <remarks>
/// Pure, so the rule is tested without a database. In order —
/// <list type="number">
/// <item>An activity this import already made a session is left as it is.</item>
/// <item>One whose session the athlete deleted stays deleted.</item>
/// <item>A type Lionheart does not take is left out (<see cref="StravaActivityKinds"/>).</item>
/// <item>A distance activity with no distance, stated or routed, is left out: nothing measures it.</item>
/// <item>One lying at least half inside a session the log holds is that session, by the rule
/// every source is held to (<see cref="SessionMatching"/>): Strava's own copy in Health, the same
/// run a watch recorded, a lifting session the athlete logged. The log includes what this run added
/// before it, so an activity Strava holds twice is taken once.</item>
/// </list>
/// What is left becomes a completed session of one set (<see cref="StravaSessions"/>).
/// </remarks>
public static class StravaActivityDecisions
{
    public static StravaActivityDecision Decide(StravaActivity activity, ActivityTrack track, ArchiveLog log)
    {
        var id = StravaSessions.SessionIdFor(activity.Id, log.Placement.UserId);
        if (log.HeldIds.Contains(id)) return new StravaActivityDecision.AlreadyImported(id);
        if (log.Deleted.Contains(activity.Id)) return LeftOut(StravaExclusion.Deleted);

        return StravaActivityKinds.Of(activity.Type) switch
        {
            StravaActivityKind.NotTaken => LeftOut(StravaExclusion.NotTaken),
            StravaActivityKind.Lifting => NewUnlessLogged(activity, log,
                () => new StravaActivityDecision.AddedLifting(StravaSessions.Lifting(activity, log.Placement))),
            var measured => RouteAnalysis.Distance(activity.DistanceMeters, track.Route) is { } distance
                ? NewUnlessLogged(activity, log, () => Added(
                    StravaSessions.Measured(
                        activity, measured, distance, RouteAnalysis.Climb(activity.ElevationGainMeters, track.Route), log.Placement),
                    track))
                : LeftOut(StravaExclusion.NoDistance),
        };
    }

    private static StravaActivityDecision NewUnlessLogged(StravaActivity activity, ArchiveLog log, Func<StravaActivityDecision> added) =>
        SessionMatching.Find(activity.Span, log.Logged) switch
        {
            SessionMatch.None => added(),
            var match => new StravaActivityDecision.Matched(match),
        };

    private static StravaActivityDecision Added(CompletedTrainingSession session, ActivityTrack track) =>
        new StravaActivityDecision.Added(session, track.RecordingFor(session.TrainingSessionId, session.UserId));

    private static StravaActivityDecision LeftOut(StravaExclusion reason) => new StravaActivityDecision.LeftOut(reason);
}
