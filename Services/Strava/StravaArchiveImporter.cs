using System.Collections.Immutable;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;
using lionheart.Services.Health.Recordings;
using lionheart.Services.Profile;
using lionheart.Services.Strava.Files;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Strava;

/// <summary>
/// Imports the athlete's Strava archive: every activity the log does not already hold becomes a
/// completed session with its recording, and every one it does hold fills in what that session's
/// recording lacks.
/// </summary>
/// <remarks>
/// Strava sent most of his runs to Apple Health, which the Health import already took; the archive
/// is the rest — years of rides and runs from before Strava wrote to Health, and the activities it
/// never sent — and the routes Strava did not write there.
/// <para>
/// <b>Counted once.</b> An activity lying inside a session the log holds is that session, by the
/// rule every source is held to (<see cref="StravaActivityDecisions"/>). Its session keeps
/// everything it has; its recording takes the archive's route, heart rate or metrics only where it
/// holds none (<see cref="WorkoutRecording.Fill"/>).
/// </para>
/// <para>
/// <b>Re-runnable.</b> A session's id derives from the activity's, so a second run recognises what
/// it holds, adds nothing, and fills nothing it filled the first time. A session the athlete
/// deleted is remembered (<see cref="DeletedStravaActivity"/>) and stays deleted.
/// </para>
/// <para>
/// <b>Records rebuilt.</b> The archive reaches back a decade, older than every record standing, so
/// as the coached-programme import does, the run replays every completed session through the
/// detector, oldest first, in its own transaction (<see cref="PersonalRecordRebuild"/>); a run that
/// adds nothing leaves the records alone. All or nothing: sessions, recordings, fills and records
/// commit together.
/// </para>
/// </remarks>
public sealed class StravaArchiveImporter
{
    private readonly ModelContext _context;
    private readonly PersonalRecordRebuild _records;
    private readonly TimeZoneInfo _zone;
    private readonly TimeProvider _clock;

    /// <param name="zone">Where the athlete's clock was; a session is placed on its local day there.</param>
    public StravaArchiveImporter(ModelContext context, PersonalRecordRebuild records, TimeZoneInfo zone, TimeProvider clock)
    {
        _context = context;
        _records = records;
        _zone = zone;
        _clock = clock;
    }

    public Task<Result<StravaArchiveImportReport, Problem>> ImportAsync(string archive, Guid userId, CancellationToken ct) =>
        _context.KnownAthleteAsync(userId, ct)
            .BindAsync(_ => _records.ReplayableAsync(userId, ct))
            .BindAsync(_ => StravaActivitiesReader.Read(archive))
            .MapAsync(activities => PlanAsync(archive, userId, activities, ct))
            .MapAsync(plan => StageAsync(userId, plan, ct))
            .BindAsync(staged => _context.InTransactionAsync(() => WriteAsync(userId, staged, ct), ct));

    // ---- Plan ----

    /// <summary>Every activity decided, oldest first, against the log as it stands and what this run adds before it.</summary>
    private async Task<ArchivePlan> PlanAsync(
        string archive, Guid userId, IReadOnlyList<StravaActivity> activities, CancellationToken ct) =>
        activities
            .OrderBy(activity => activity.Start)
            .Aggregate(ArchivePlan.Start(await LogAsync(userId, activities, ct)), (plan, activity) => plan.After(archive, activity));

    private async Task<ArchiveLog> LogAsync(Guid userId, IReadOnlyList<StravaActivity> activities, CancellationToken ct)
    {
        var ids = activities.Select(activity => StravaSessions.SessionIdFor(activity.Id, userId)).ToList();

        return new ArchiveLog(
            await _context.Sessions.OfType<CompletedTrainingSession>()
                .AsNoTracking()
                .Where(session => session.UserId == userId)
                .ToListAsync(ct),
            (await _context.Sessions
                .Where(session => ids.Contains(session.TrainingSessionId))
                .Select(session => session.TrainingSessionId)
                .ToListAsync(ct)).ToHashSet(),
            (await _context.DeletedStravaActivities
                .Where(deleted => deleted.UserId == userId)
                .Select(deleted => deleted.ActivityId)
                .ToListAsync(ct)).ToHashSet(),
            new ArchivePlacement(userId, _zone, _clock.GetUtcNow().UtcDateTime));
    }

    // ---- Stage ----

    private async Task<StagedArchive> StageAsync(Guid userId, ArchivePlan plan, CancellationToken ct)
    {
        var added = plan.Outcomes.Select(outcome => outcome.Decision).ToList();
        _context.Sessions.AddRange(added.OfType<StravaActivityDecision.Added>().Select(decision => decision.Session));
        _context.Sessions.AddRange(added.OfType<StravaActivityDecision.AddedLifting>().Select(decision => decision.Session));
        _context.WorkoutRecordings.AddRange(added.OfType<StravaActivityDecision.Added>().Select(decision => decision.Recording));

        var fills = await FillAsync(plan, ct);
        if (plan.AddedCount > 0) await _context.StageRecentStateInvalidationAsync(userId, ct);

        return new StagedArchive(plan, fills, await _context.DeletedHealthWorkouts.CountAsync(deleted => deleted.UserId == userId, ct));
    }

    /// <summary>
    /// Fills each held session's recording from the activity it is, where the recording holds none
    /// of a part, and counts the clockless routes kept where the archive's would have kept time.
    /// </summary>
    private async Task<ArchiveFills> FillAsync(ArchivePlan plan, CancellationToken ct)
    {
        var logged = plan.Log.Logged.ToDictionary(session => session.TrainingSessionId);
        var candidates = plan.Outcomes.Select(FillTarget.Of).OfType<FillTarget>().ToList();
        var ids = candidates.Select(target => target.Held).Distinct().ToList();
        var recordings = await _context.WorkoutRecordings
            .Where(recording => ids.Contains(recording.TrainingSessionId))
            .ToDictionaryAsync(recording => recording.TrainingSessionId, ct);

        // A session added by this run has its recording staged rather than held, and was filled from its own file.
        var targets = candidates
            .Where(target => recordings.ContainsKey(target.Held) && logged.ContainsKey(target.Held))
            .Select(target => (Target: target, Recording: recordings[target.Held], Session: logged[target.Held]))
            .ToList();

        // Counted before filling, since a fill can give a held recording the route it was missing.
        var clockless = targets.Count(held => KeepsClocklessRoute(held.Recording, held.Session, held.Target.Track));

        var enriched = new List<EnrichedSession>();
        foreach (var (target, recording, _) in targets)
        {
            var fill = recording.Fill(target.Track.Route, target.Track.HeartRate, target.Track.Metrics);
            if (fill.Any) enriched.Add(new EnrichedSession(target.Activity, target.Held, fill));
        }

        return new ArchiveFills(enriched, clockless);
    }

    private static bool KeepsClocklessRoute(WorkoutRecording held, CompletedTrainingSession session, ActivityTrack track) =>
        SessionMatching.Span(session.Period) is { } span
        && held.Route.HasPath && !RouteAnalysis.KeepsTime(held.Route, span.End - span.Start)
        && RouteAnalysis.KeepsTime(track.Route, span.End - span.Start);

    // ---- Commit ----

    /// <remarks>The rebuild saves inside the import's transaction, so a rebuild that refuses leaves nothing of the import written.</remarks>
    private async Task<Result<StravaArchiveImportReport, Problem>> WriteAsync(Guid userId, StagedArchive staged, CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);

        var records = staged.Plan.AddedCount == 0
            ? Result<RecordsOutcome, Problem>.Success(new RecordsOutcome.Untouched())
            : await _records.ReplayAsync(userId, ct).MapAsync(RecordsOutcome (report) => new RecordsOutcome.Rebuilt(report));

        return records.Map(outcome => staged.Report(outcome));
    }

    // ---- The values carried between steps ----

    private sealed record ActivityOutcome(StravaActivity Activity, StravaActivityDecision Decision, ActivityTrack Track);

    /// <summary>The archive decided so far: the log as this run has grown it, and what became of each activity.</summary>
    private sealed record ArchivePlan(
        ArchiveLog Log,
        ImmutableList<ActivityOutcome> Outcomes,
        ImmutableList<UnreadableFile> Unreadable,
        ImmutableList<NearSession> Near)
    {
        public static ArchivePlan Start(ArchiveLog log) => new(log, [], [], []);

        public int AddedCount => Outcomes.Count(outcome => outcome.Decision is StravaActivityDecision.Added or StravaActivityDecision.AddedLifting);

        /// <summary>The plan with one more activity read and decided; a session it adds joins the log the next one is decided against.</summary>
        public ArchivePlan After(string archive, StravaActivity activity)
        {
            var (track, unreadable) = ActivityFiles.Read(archive, activity.File).Match(
                read => (read, ImmutableList<UnreadableFile>.Empty),
                reason => (ActivityTrack.None, [new UnreadableFile(activity, reason)]));
            var decision = StravaActivityDecisions.Decide(activity, track, Log);
            var session = decision switch
            {
                StravaActivityDecision.Added added => added.Session,
                StravaActivityDecision.AddedLifting lifting => lifting.Session,
                _ => null,
            };

            return new ArchivePlan(
                session is null ? Log : Log with { Logged = [.. Log.Logged, session] },
                Outcomes.Add(new ActivityOutcome(activity, decision, track)),
                Unreadable.AddRange(unreadable),
                session is null ? Near : Near.AddRange(Nearest(activity, Log.Logged)));
        }

        /// <summary>The held session an added activity came closest to lying inside, if it touched any.</summary>
        private static IEnumerable<NearSession> Nearest(StravaActivity activity, IReadOnlyList<CompletedTrainingSession> logged) =>
            logged
                .Select(session => new NearSession(activity, session.TrainingSessionId, SessionMatching.ShareInside(activity.Span, session.Period)))
                .Where(near => near.Share > 0)
                .OrderByDescending(near => near.Share)
                .Take(1);
    }

    /// <summary>An activity that is a held session with a recording to fill: one an earlier run imported, or a cardio session it matched.</summary>
    private sealed record FillTarget(StravaActivity Activity, ActivityTrack Track, Guid Held)
    {
        /// <remarks>A lifting session it lies inside has no recording, so it is no target.</remarks>
        public static FillTarget? Of(ActivityOutcome outcome) => outcome.Decision switch
        {
            StravaActivityDecision.AlreadyImported imported => new FillTarget(outcome.Activity, outcome.Track, imported.TrainingSessionId),
            StravaActivityDecision.Matched { Match: SessionMatch.LoggedRun run } => new FillTarget(outcome.Activity, outcome.Track, run.TrainingSessionId),
            _ => null,
        };
    }

    private sealed record ArchiveFills(IReadOnlyList<EnrichedSession> Enriched, int ClocklessRoutesKept);

    /// <param name="DeletedHealthWorkouts">
    /// Health workouts whose sessions the athlete deleted. One carries no time, so the archive cannot
    /// tell which activity it was; the report says how many there are, to be checked by hand.
    /// </param>
    private sealed record StagedArchive(ArchivePlan Plan, ArchiveFills Fills, int DeletedHealthWorkouts)
    {
        public StravaArchiveImportReport Report(RecordsOutcome records)
        {
            var logged = Plan.Log.Logged.ToDictionary(session => session.TrainingSessionId);

            return new StravaArchiveImportReport(
                Plan.Outcomes.Count,
                [
                    .. Plan.Outcomes.Select(outcome => outcome.Decision switch
                    {
                        StravaActivityDecision.Added added => new AddedActivity(outcome.Activity, Became(added.Session)),
                        StravaActivityDecision.AddedLifting => new AddedActivity(outcome.Activity, "lifting"),
                        _ => null,
                    }).OfType<AddedActivity>(),
                ],
                [
                    .. Plan.Outcomes.Select(outcome => outcome.Decision is StravaActivityDecision.Matched matched
                        ? new MatchedActivity(outcome.Activity, matched.Match, logged[MatchedId(matched.Match)].Source)
                        : null).OfType<MatchedActivity>(),
                ],
                Plan.Outcomes.Count(outcome => outcome.Decision is StravaActivityDecision.AlreadyImported),
                Fills.Enriched,
                [
                    .. Plan.Outcomes.Select(outcome => outcome.Decision is StravaActivityDecision.LeftOut left
                        ? new LeftOutActivity(outcome.Activity, left.Reason)
                        : null).OfType<LeftOutActivity>(),
                ],
                Plan.Unreadable,
                Plan.Near,
                Fills.ClocklessRoutesKept,
                DeletedHealthWorkouts,
                records);
        }

        private static Guid MatchedId(SessionMatch match) => match switch
        {
            SessionMatch.LoggedRun run => run.TrainingSessionId,
            SessionMatch.LoggedLifting lifting => lifting.TrainingSessionId,
            _ => throw new System.Diagnostics.UnreachableException("A matched activity matched a session."),
        };

        private static string Became(CompletedTrainingSession session) =>
            session.Sets.EnduranceActivity switch
            {
                null => "run",
                EnduranceActivity.Ride => "ride",
                EnduranceActivity.IndoorRide => "indoor ride",
                EnduranceActivity.Hike => "hike",
                EnduranceActivity.Walk => "walk",
                EnduranceActivity.IndoorRun => "indoor run",
                EnduranceActivity.EBikeRide => "e-bike ride",
                EnduranceActivity.Paddle => "paddle",
                _ => throw new System.Diagnostics.UnreachableException(),
            };
    }
}
