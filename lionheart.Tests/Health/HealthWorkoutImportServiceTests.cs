using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Health;
using lionheart.Tests.Api;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Health;

/// <summary>
/// Importing Apple Health runs through the real schema: a session and its recording written
/// together, an import that is idempotent on the workout's UUID, the records it sets, and a run's
/// page read back with its heart rate from the right source.
/// </summary>
public class HealthWorkoutImportServiceTests : IDisposable
{
    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    // ---- Importing ----

    [Fact]
    public async Task An_imported_run_is_a_completed_session_whose_recording_reads_back_as_the_phone_sent_it()
    {
        var workout = AHealthRun.Outdoor(meters: 5000);

        var imported = Assert.IsType<HealthImportOutcome.Imported>(await ImportAsync(workout));

        await using var context = _athletes.CreateContext();
        var session = await context.Sessions.OfType<CompletedTrainingSession>().SingleAsync();
        Assert.Equal(imported.Session.TrainingSessionId, session.TrainingSessionId);
        Assert.Equal(workout.WorkoutId, Assert.IsType<SessionSource.AppleHealth>(session.Source).WorkoutId);

        var recording = await context.WorkoutRecordings.SingleAsync();
        var stated = ResultAssert.Succeeded(workout.Stated());
        Assert.Equal(stated.Route, recording.Route);
        Assert.Equal(stated.HeartRate, recording.HeartRate);
    }

    [Fact]
    public async Task Offering_the_same_workout_again_answers_already_imported_and_writes_nothing()
    {
        var workout = AHealthRun.Outdoor();
        var first = Assert.IsType<HealthImportOutcome.Imported>(await ImportAsync(workout));

        var again = await ImportAsync(workout);

        Assert.Equal(new HealthImportOutcome.AlreadyImported(first.Session.TrainingSessionId), again);
        await using var context = _athletes.CreateContext();
        Assert.Equal((1, 1), (await context.Sessions.CountAsync(), await context.WorkoutRecordings.CountAsync()));
    }

    [Fact]
    public async Task A_workout_another_athlete_imported_is_still_new_to_this_one()
    {
        var workout = AHealthRun.Outdoor();
        await ImportAsync(workout);

        var theirs = await _athletes.HealthImportAsync(service => service.ImportAsync(_athletes.Stranger, workout, default));

        Assert.IsType<HealthImportOutcome.Imported>(ResultAssert.Succeeded(theirs));
    }

    [Fact]
    public async Task A_run_inside_one_already_logged_is_not_imported_and_names_the_session_it_is()
    {
        var first = Assert.IsType<HealthImportOutcome.Imported>(await ImportAsync(AHealthRun.Outdoor(meters: 5400)));
        var sameRunFromAnotherApp = AHealthRun.Outdoor(meters: 5400, start: AHealthRun.Start.AddSeconds(30)) with
        {
            AppName = "Runna",
            AppBundleId = "com.runna.app",
        };

        var outcome = await ImportAsync(sameRunFromAnotherApp);

        Assert.Equal(
            new HealthImportOutcome.NotImported(HealthWorkoutExclusion.SameAsLoggedRun, first.Session.TrainingSessionId),
            outcome);
    }

    [Fact]
    public async Task A_malformed_workout_is_refused_and_writes_nothing()
    {
        var problem = ResultAssert.Failed(await _athletes.HealthImportAsync(service => service.ImportAsync(
            _athletes.Owner, AHealthRun.Outdoor() with { End = AHealthRun.Start.AddMinutes(-1) }, default)));

        Assert.IsType<Problem.Invalid>(problem);
        await using var context = _athletes.CreateContext();
        Assert.Equal(0, await context.Sessions.CountAsync());
    }

    [Fact]
    public async Task An_account_with_no_profile_is_refused()
    {
        var problem = ResultAssert.Failed(await _athletes.HealthImportAsync(service =>
            service.ImportAsync(_athletes.Unprofiled, AHealthRun.Outdoor(), default)));

        Assert.IsType<Problem.Conflict>(problem);
    }

    // ---- Records ----

    [Fact]
    public async Task An_imported_run_sets_the_trail_running_records()
    {
        await ImportAsync(AHealthRun.Outdoor(meters: 5000));

        var records = await StandingAsync();

        Assert.All(records, record => Assert.IsType<RecordSubject.TrailRunning>(record.Subject));
        Assert.Contains(records, record => record.Achievement is Achievement.LongestDistance);
        Assert.Contains(records, record => record.Achievement is Achievement.BestPace);
    }

    [Fact]
    public async Task An_older_run_imported_after_a_newer_one_is_placed_in_the_order_they_happened()
    {
        // The newer, shorter run is imported first; the older, longer one arrives afterwards — the
        // first sync backfilling. Judged against what stands, the older run would claim to have
        // beaten a record set after it.
        await ImportAsync(AHealthRun.Outdoor(meters: 5000, start: AHealthRun.Start.AddDays(7)));

        await ImportAsync(AHealthRun.Outdoor(meters: 10000, start: AHealthRun.Start));

        var longest = Assert.Single(await StandingAsync(), record => record.Achievement is Achievement.LongestDistance);
        Assert.Equal(AHealthRun.Start.UtcDateTime, longest.AchievedAt);
        Assert.IsType<RecordLineage.FirstOfItsKind>(longest.Lineage);
    }

    [Fact]
    public async Task An_older_run_is_refused_and_nothing_written_while_a_record_is_withdrawn()
    {
        // Placing it means replaying the records, which would award the withdrawn one again.
        await ImportAsync(AHealthRun.Outdoor(meters: 5000, start: AHealthRun.Start.AddDays(7)));
        var pace = Assert.Single(await StandingAsync(), record => record.Achievement is Achievement.BestPace);
        ResultAssert.Succeeded(await _athletes.RecordsAsync(service =>
            service.RetractAsync(_athletes.Owner, pace.PersonalRecordId, default)));

        var problem = ResultAssert.Failed(await _athletes.HealthImportAsync(service =>
            service.ImportAsync(_athletes.Owner, AHealthRun.Outdoor(meters: 10000, start: AHealthRun.Start), default)));

        Assert.IsType<Problem.Conflict>(problem);
        await using var context = _athletes.CreateContext();
        Assert.Equal((1, 1), (await context.Sessions.CountAsync(), await context.WorkoutRecordings.CountAsync()));
    }

    // ---- Correcting ----

    [Fact]
    public async Task Correcting_an_imported_run_keeps_where_it_came_from_and_its_recording()
    {
        var workout = AHealthRun.Outdoor(meters: 5000);
        var imported = Assert.IsType<HealthImportOutcome.Imported>(await ImportAsync(workout));
        var id = imported.Session.TrainingSessionId;

        ResultAssert.Succeeded(await _athletes.SessionsAsync(service =>
            service.UpdateNotesAsync(_athletes.Owner, id, new SessionNotesRequest("hilly"), default)));
        ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.ReviseAsync(
            _athletes.Owner,
            id,
            new ReviseSessionRequest(
                [new TrailRunSetCommand(5.1, DistanceUnit.Kilometers, workout.Duration, 40, null)],
                new Model.Analysis.PeriodCommand(workout.Start, workout.End),
                "hilly"),
            default)));

        await using var context = _athletes.CreateContext();
        var session = await context.Sessions.SingleAsync();
        Assert.Equal(workout.WorkoutId, Assert.IsType<SessionSource.AppleHealth>(session.Source).WorkoutId);
        Assert.Equal(RemodelDatabaseGuid(workout.WorkoutId), await StampedWorkoutIdAsync(context));
        Assert.Equal(id, (await context.WorkoutRecordings.SingleAsync()).TrainingSessionId);
        Assert.Equal(new HealthImportOutcome.AlreadyImported(id), await ImportAsync(workout));
    }

    private static string RemodelDatabaseGuid(Guid id) => Persistence.RemodelDatabase.SqlGuid(id);

    private static async Task<string?> StampedWorkoutIdAsync(ModelContext context) =>
        (await context.Sessions
            .Select(session => EF.Property<Guid?>(session, RemodelConfiguration.HealthWorkoutIdColumn))
            .SingleAsync())?.ToString().ToUpperInvariant();

    // ---- Deleting ----

    [Fact]
    public async Task Deleting_an_imported_session_takes_its_route_with_it()
    {
        var imported = Assert.IsType<HealthImportOutcome.Imported>(await ImportAsync(AHealthRun.Outdoor()));

        await _athletes.SessionsAsync(service => service.DeleteAsync(_athletes.Owner, imported.Session.TrainingSessionId, default));

        await using var context = _athletes.CreateContext();
        Assert.Equal(0, await context.WorkoutRecordings.CountAsync());
    }

    // ---- Reading a run back ----

    [Fact]
    public async Task A_run_reads_back_with_its_splits_and_the_recording_apps_heart_rate()
    {
        var imported = Assert.IsType<HealthImportOutcome.Imported>(await ImportAsync(AHealthRun.Outdoor(meters: 2500, bpm: 152)));

        var run = ResultAssert.Succeeded(await _athletes.RecordedRunsAsync(service =>
            service.GetAsync(_athletes.Owner, imported.Session.TrainingSessionId, default)));

        Assert.Equal(2500, run.DistanceMeters, precision: 6);
        Assert.Equal(3, run.Kilometres.Count);
        Assert.Equal(2, run.Miles.Count);
        Assert.Equal(HeartRateSource.AppleHealth, run.Trace.Source);
        var heartRate = Assert.IsType<SessionHeartRate.Recorded>(run.HeartRate);
        Assert.Equal((152, HeartRateSource.AppleHealth), (heartRate.AverageBpm, heartRate.Source));
        Assert.Equal(AHealthRun.StravaName, run.RecordedBy.Name);
    }

    [Fact]
    public async Task A_run_the_app_wrote_no_heart_rate_for_reads_the_rings_series_and_says_so()
    {
        var workout = AHealthRun.Outdoor(meters: 2500) with { HeartRate = [] };
        var imported = Assert.IsType<HealthImportOutcome.Imported>(await ImportAsync(workout));
        await _athletes.PlantAsync(context => context.OuraHeartRateDays.Add(new OuraHeartRateDay(
            _athletes.OwnerId,
            DateOnly.FromDateTime(workout.Start.UtcDateTime),
            new HeartRateDayReading([.. Enumerable.Range(0, 3).Select(index =>
                new HeartRateSample(workout.Start.AddMinutes(5 * index).ToUniversalTime(), 141, "workout"))]))));

        var run = ResultAssert.Succeeded(await _athletes.RecordedRunsAsync(service =>
            service.GetAsync(_athletes.Owner, imported.Session.TrainingSessionId, default)));

        Assert.Equal(HeartRateSource.Ring, run.Trace.Source);
        Assert.Equal(3, run.Trace.Samples.Count);
        Assert.Equal(HeartRateSource.Ring, Assert.IsType<SessionHeartRate.Recorded>(run.HeartRate).Source);
    }

    [Fact]
    public async Task A_session_from_the_athletes_own_log_has_no_run_page()
    {
        var logged = Some.CompletedSession(_athletes.OwnerId);
        await _athletes.PlantAsync(context => context.Sessions.Add(logged));

        var problem = ResultAssert.Failed(await _athletes.RecordedRunsAsync(service =>
            service.GetAsync(_athletes.Owner, logged.TrainingSessionId, default)));

        Assert.IsType<Problem.NotFound>(problem);
    }

    [Fact]
    public async Task Another_athletes_run_reads_as_not_found()
    {
        var imported = Assert.IsType<HealthImportOutcome.Imported>(await ImportAsync(AHealthRun.Outdoor()));

        var problem = ResultAssert.Failed(await _athletes.RecordedRunsAsync(service =>
            service.GetAsync(_athletes.Stranger, imported.Session.TrainingSessionId, default)));

        Assert.IsType<Problem.NotFound>(problem);
    }

    [Fact]
    public async Task The_chat_model_reads_a_run_without_a_single_coordinate()
    {
        await ImportAsync(AHealthRun.Outdoor(meters: 5000));

        var runs = await _athletes.RecordedRunsAsync(service => ((RecordedRunService)service).GetRecordedRunsForChatAsync(
            _athletes.Owner, new Model.Analysis.DayRange(DateOnly.FromDateTime(AHealthRun.Start.DateTime), DateOnly.FromDateTime(AHealthRun.Start.DateTime))));

        var text = Assert.Single(runs);
        Assert.Contains("5.00 km", text);
        Assert.Contains("Splits per km", text);
        Assert.DoesNotContain("46.5", text);
        Assert.DoesNotContain("8.25", text);
    }

    // ---- Helpers ----

    private async Task<HealthImportOutcome> ImportAsync(HealthWorkoutImport workout) =>
        ResultAssert.Succeeded(await _athletes.HealthImportAsync(service => service.ImportAsync(_athletes.Owner, workout, default)));

    private async Task<List<PersonalRecord>> StandingAsync()
    {
        await using var context = _athletes.CreateContext();
        return await context.PersonalRecords
            .Where(record => record.UserId == _athletes.OwnerId
                             && EF.Property<bool>(record, RemodelConfiguration.RecordIsStandingColumn))
            .ToListAsync();
    }
}
