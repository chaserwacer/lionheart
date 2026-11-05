using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Health;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Strava;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Api;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static lionheart.Tests.Strava.StravaArchives;

namespace lionheart.Tests.Strava;

/// <summary>
/// The Strava archive import against a real database: what it adds with what recording, what it
/// recognises as already held and fills in, that a second run adds nothing, that a deletion
/// stands, and the records it rebuilds.
/// </summary>
public class StravaArchiveImportTests : IDisposable
{
    private static readonly DateTimeOffset RideStart = new(2021, 5, 2, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RunStart = new(2022, 8, 20, 11, 0, 0, TimeSpan.Zero);

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    // ---- What it adds ----

    [Fact]
    public async Task An_activity_the_log_lacks_becomes_a_session_with_its_files_route_beside_it()
    {
        using var archive = new ArchiveFolder(
                Row(5001, RideStart, type: "Ride", name: "Afternoon Ride", elapsedSeconds: 2400, movingSeconds: 2300, meters: 7200, file: "activities/5001.gpx"))
            .With("activities/5001.gpx", Gpx(RideStart, meters: 7200, metersPerSecond: 3));

        var report = ResultAssert.Succeeded(await ImportAsync(archive));

        var added = Assert.Single(report.Added);
        Assert.Equal((5001L, "ride"), (added.Activity.Id, added.Became));
        await using var context = _athletes.CreateContext();
        var session = await context.Sessions.OfType<CompletedTrainingSession>().AsNoTracking().SingleAsync();
        Assert.Equal(new SessionSource.StravaArchive(5001), session.Source);
        var recording = await context.WorkoutRecordings.AsNoTracking().SingleAsync();
        Assert.Equal(session.TrainingSessionId, recording.TrainingSessionId);
        Assert.Equal(AHealthRun.North(RideStart, 7200, 3).Count, recording.Route.Points.Count);
    }

    [Fact]
    public async Task A_ride_the_archive_adds_takes_the_riding_records_in_a_rebuild_of_the_whole_history()
    {
        using var archive = new ArchiveFolder(Row(5001, RideStart, type: "Ride", meters: 20000, elapsedSeconds: 3600, movingSeconds: 3600));

        var report = ResultAssert.Succeeded(await ImportAsync(archive));

        Assert.IsType<RecordsOutcome.Rebuilt>(report.Records);
        var riding = (await RecordsAsync()).Where(record => record.Subject is RecordSubject.Riding).ToList();
        Assert.Contains(riding, record => record.Kind is RecordKind.LongestDistance && record.IsStanding);
    }

    [Fact]
    public async Task An_activity_whose_file_cannot_be_read_is_added_from_its_summary_and_its_file_named()
    {
        using var archive = new ArchiveFolder(Row(5001, RunStart, file: "activities/5001.gpx"))
            .With("activities/5001.gpx", "<gpx><trk><trkseg><trkpt lat=");

        var report = ResultAssert.Succeeded(await ImportAsync(archive));

        Assert.Single(report.Added);
        Assert.Equal(5001, Assert.Single(report.UnreadableFiles).Activity.Id);
        await using var context = _athletes.CreateContext();
        Assert.Empty((await context.WorkoutRecordings.AsNoTracking().SingleAsync()).Route.Points);
    }

    // ---- Counted once ----

    [Fact]
    public async Task Running_the_archive_again_adds_nothing_and_fills_nothing()
    {
        using var archive = new ArchiveFolder(
                Row(5001, RunStart, file: "activities/5001.gpx"),
                Row(5002, RunStart.AddDays(1), type: "Weight Training", meters: 0))
            .With("activities/5001.gpx", Gpx(RunStart, meters: 5000));
        ResultAssert.Succeeded(await ImportAsync(archive));
        var before = await SessionIdsAsync();

        var again = ResultAssert.Succeeded(await ImportAsync(archive));

        Assert.Equal((0, 2, 0), (again.Added.Count, again.AlreadyImported, again.Enriched.Count));
        Assert.IsType<RecordsOutcome.Untouched>(again.Records);
        Assert.Equal(before, await SessionIdsAsync());
    }

    [Fact]
    public async Task Stravas_copy_in_Health_is_matched_and_given_the_route_Health_did_not_carry()
    {
        var health = await PlantHealthRunAsync(RunStart, RunStart.AddMinutes(30), WorkoutRoute.None);
        using var archive = new ArchiveFolder(Row(5001, RunStart, file: "activities/5001.gpx"))
            .With("activities/5001.gpx", Gpx(RunStart, meters: 5000, bpm: 148));

        var report = ResultAssert.Succeeded(await ImportAsync(archive));

        Assert.Empty(report.Added);
        Assert.Equal(health, Assert.Single(report.Enriched).TrainingSessionId);
        Assert.Equal(new RecordingFill(Route: true, HeartRate: true, Metrics: false), report.Enriched[0].Fill);
        await using var context = _athletes.CreateContext();
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.NotEmpty((await context.WorkoutRecordings.AsNoTracking().SingleAsync()).Route.Points);
    }

    [Fact]
    public async Task A_route_the_held_session_already_has_is_kept_and_counted_when_the_archives_keeps_time()
    {
        // Strava's copy in Health: every fix stamped at the moment Strava wrote it, so the route has no clock.
        var stamped = RunStart.AddDays(2);
        var clockless = new WorkoutRoute([.. AHealthRun.Route(AHealthRun.North(RunStart, 5000, 3)).Points.Select(point => point with { At = stamped })]);
        await PlantHealthRunAsync(RunStart, RunStart.AddMinutes(30), clockless);
        using var archive = new ArchiveFolder(Row(5001, RunStart, file: "activities/5001.gpx"))
            .With("activities/5001.gpx", Gpx(RunStart, meters: 5000));

        var report = ResultAssert.Succeeded(await ImportAsync(archive));

        Assert.Empty(report.Enriched);
        Assert.Equal(1, report.ClocklessRoutesKept);
        await using var context = _athletes.CreateContext();
        Assert.All((await context.WorkoutRecordings.AsNoTracking().SingleAsync()).Route.Points, point => Assert.Equal(stamped, point.At));
    }

    [Fact]
    public async Task Two_copies_of_one_activity_in_the_archive_become_one_session()
    {
        using var archive = new ArchiveFolder(Row(5001, RunStart), Row(5002, RunStart.AddMinutes(1)));

        var report = ResultAssert.Succeeded(await ImportAsync(archive));

        Assert.Equal(5001, Assert.Single(report.Added).Activity.Id);
        Assert.Equal(new SessionSource.StravaArchive(5001), Assert.Single(report.Matched).HeldAs);
    }

    [Fact]
    public async Task An_activity_whose_session_the_athlete_deleted_is_not_imported_again()
    {
        using var archive = new ArchiveFolder(Row(5001, RunStart, file: "activities/5001.gpx"))
            .With("activities/5001.gpx", Gpx(RunStart, meters: 5000));
        ResultAssert.Succeeded(await ImportAsync(archive));
        var id = StravaSessions.SessionIdFor(5001, _athletes.OwnerId);
        ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.DeleteAsync(_athletes.Owner, id, default)));

        var again = ResultAssert.Succeeded(await ImportAsync(archive));

        Assert.Equal(StravaExclusion.Deleted, Assert.Single(again.LeftOut).Reason);
        await using var context = _athletes.CreateContext();
        Assert.Equal((0, 0), (await context.Sessions.CountAsync(), await context.WorkoutRecordings.CountAsync()));
    }

    // ---- Refusals ----

    [Fact]
    public async Task A_withdrawn_record_refuses_the_import_and_nothing_is_written()
    {
        using (var first = new ArchiveFolder(Row(5001, RideStart, type: "Ride", meters: 20000)))
            ResultAssert.Succeeded(await ImportAsync(first));
        await using (var context = _athletes.CreateContext())
        {
            context.PersonalRecords.AsEnumerable()
                .First(record => record.IsStanding)
                .Retract(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            await context.SaveChangesAsync();
        }

        var before = await SessionIdsAsync();
        using var archive = new ArchiveFolder(Row(5002, RideStart.AddYears(-1), type: "Ride", meters: 25000));

        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(await ImportAsync(archive)));
        Assert.Equal(before, await SessionIdsAsync());
    }

    // ---- Helpers ----

    private async Task<Result<StravaArchiveImportReport, Problem>> ImportAsync(ArchiveFolder archive)
    {
        await using var context = _athletes.CreateContext();
        var rebuild = new PersonalRecordRebuild(context, new PersonalRecordDetector(context, TimeProvider.System));
        return await new StravaArchiveImporter(context, rebuild, NewYork, TimeProvider.System)
            .ImportAsync(archive.Path, _athletes.OwnerId, CancellationToken.None);
    }

    private async Task<Guid> PlantHealthRunAsync(DateTimeOffset start, DateTimeOffset end, WorkoutRoute route)
    {
        var session = Some.CompletedSession(_athletes.OwnerId) with
        {
            Sets = [new TrailRunSet(5, DistanceUnit.Kilometers, end - start, 0, Rpe.Unrated) { Source = new Formed(), Facets = [] }],
            Period = new Interval(start, end),
            Source = AHealthRun.Source(),
        };
        await using var context = _athletes.CreateContext();
        context.Sessions.Add(session);
        context.WorkoutRecordings.Add(WorkoutRecording.Of(session.TrainingSessionId, _athletes.OwnerId, route, RecordedHeartRate.None, RecordedMetrics.None));
        await context.SaveChangesAsync();
        return session.TrainingSessionId;
    }

    private async Task<IReadOnlyList<Guid>> SessionIdsAsync()
    {
        await using var context = _athletes.CreateContext();
        return await context.Sessions.Select(session => session.TrainingSessionId).OrderBy(id => id).ToListAsync();
    }

    private async Task<IReadOnlyList<PersonalRecord>> RecordsAsync()
    {
        await using var context = _athletes.CreateContext();
        return await context.PersonalRecords.AsNoTracking().ToListAsync();
    }
}
