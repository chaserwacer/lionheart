using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Cardio;

/// <summary>Which logged session, if any, a workout another device recorded already is.</summary>
public abstract record SessionMatch
{
    private SessionMatch() { }

    /// <summary>It lies outside every logged session: a session of its own.</summary>
    public sealed record None : SessionMatch;

    /// <summary>
    /// It lies inside a logged cardio session — a run, a ride, a hike, a walk — which is the same
    /// session already counted.
    /// </summary>
    public sealed record LoggedRun(Guid TrainingSessionId) : SessionMatch;

    /// <summary>It lies inside a logged lifting session: the gym as the other device saw it.</summary>
    public sealed record LoggedLifting(Guid TrainingSessionId) : SessionMatch;
}

/// <summary>
/// The one rule for when a workout recorded somewhere else — the ring, Apple Health, the Strava
/// archive — is a session the athlete already has in his log.
/// </summary>
/// <remarks>
/// One rule for every source, so a run is never counted once by the cardio report and twice by the
/// log: the cardio report applies it to the ring's workouts on every read, and the Health and
/// Strava archive imports apply it once, before a workout becomes a session at all.
/// </remarks>
public static class SessionMatching
{
    /// <summary>
    /// How much of a workout must lie inside a logged session for the two to be one session.
    /// </summary>
    /// <remarks>
    /// The ring detects most of what the athlete logs, a few minutes late and a few minutes short.
    /// Half its own length inside the logged session is a match; anything less is two things that
    /// happened to be near each other.
    /// </remarks>
    public const double SameSessionOverlap = 0.5;

    /// <summary>
    /// The logged session a workout spanning <paramref name="workout"/> already is. A run is
    /// matched ahead of a lifting session, so a workout inside both is the run.
    /// </summary>
    public static SessionMatch Find(
        (DateTimeOffset Start, DateTimeOffset End) workout, IEnumerable<CompletedTrainingSession> logged)
    {
        var inside = logged.Where(session => Overlaps(workout, session.Period)).ToList();

        return inside.FirstOrDefault(IsCardio) is { } run ? new SessionMatch.LoggedRun(run.TrainingSessionId)
            : inside.FirstOrDefault() is { } lifting ? new SessionMatch.LoggedLifting(lifting.TrainingSessionId)
            : new SessionMatch.None();
    }

    /// <summary>A logged session that carries a trail run, an endurance set or a distance-and-time set.</summary>
    public static bool IsCardio(CompletedTrainingSession session) =>
        session.Sets.Any(set => set is TrailRunSet or EnduranceSet or DistanceTimeSet);

    /// <summary>A period as a span of clock time, or nothing for a moment, which has no length to overlap.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End)? Span(Period period) => period switch
    {
        Interval interval => (interval.Start, interval.End),
        Instant => null,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>
    /// How much of a workout lies inside a logged session, from 0 to 1 — what the rule compares
    /// against <see cref="SameSessionOverlap"/>, and what an import reports for a workout that came
    /// near a session without being it.
    /// </summary>
    /// <remarks>
    /// A workout of no length — an app that wrote its start and end as one instant — has no share
    /// to measure, so it lies wholly inside a session when its moment does, ends included — which
    /// is how two copies of one such workout are found to be one.
    /// </remarks>
    public static double ShareInside((DateTimeOffset Start, DateTimeOffset End) workout, Period logged)
    {
        if (Span(logged) is not { } loggedSpan) return 0;

        var own = workout.End - workout.Start;
        if (own <= TimeSpan.Zero) return workout.Start >= loggedSpan.Start && workout.Start <= loggedSpan.End ? 1 : 0;

        var shared = Min(workout.End, loggedSpan.End) - Max(workout.Start, loggedSpan.Start);
        return Math.Max(0, shared.TotalSeconds / own.TotalSeconds);
    }

    private static bool Overlaps((DateTimeOffset Start, DateTimeOffset End) workout, Period logged) =>
        ShareInside(workout, logged) >= SameSessionOverlap;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;
}
