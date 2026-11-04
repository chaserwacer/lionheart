using lionheart.Model.Health;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// A session's source and an imported run's recording through real SQLite: the source as JSON with
/// its Health workout id stamped beside it, one session per workout, and the route and heart rate in
/// their column-by-column storage form.
/// </summary>
public sealed class WorkoutRecordingPersistenceTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void A_session_stored_before_sources_existed_reads_as_the_athletes_log()
    {
        _db.ExecuteNonQuery($$"""
            INSERT INTO Sessions
                (TrainingSessionId, UserId, Notes, Provenance, TrainingProgramId, RoutineId, CreatedAt, Lifecycle, PlannedFor)
            VALUES ('{{RemodelDatabase.SqlGuid(Guid.NewGuid())}}', '{{RemodelDatabase.SqlGuid(_db.UserId)}}',
                    'planted', 0, '{{RemodelDatabase.SqlGuid(Guid.Empty)}}', '{{RemodelDatabase.SqlGuid(Guid.Empty)}}', '2026-07-18 09:30:00',
                    'skipped', '2026-07-21');
            """);

        using var context = _db.CreateContext();
        Assert.IsType<SessionSource.AthleteLog>(context.Sessions.Single().Source);
        Assert.Equal("""{"$type":"athleteLog"}""", _db.ExecuteScalar("SELECT Source FROM Sessions"));
    }

    [Fact]
    public void An_imported_sessions_source_round_trips_and_its_workout_id_is_stamped_beside_it()
    {
        var workoutId = Guid.NewGuid();
        var session = Some.CompletedSession(_db.UserId) with { Source = AHealthRun.Source(workoutId) };
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(session.Source, fresh.Sessions.Single().Source);
        Assert.Equal(RemodelDatabase.SqlGuid(workoutId), _db.ExecuteScalar("SELECT HealthWorkoutId FROM Sessions"));
    }

    [Fact]
    public void A_session_from_the_athletes_log_stamps_no_workout_id()
    {
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId));
            context.SaveChanges();
        }

        Assert.Null(_db.ExecuteScalar("SELECT HealthWorkoutId FROM Sessions"));
    }

    [Fact]
    public void A_second_session_for_one_Health_workout_is_refused_by_the_index()
    {
        var workoutId = Guid.NewGuid();
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Source = AHealthRun.Source(workoutId) });
            context.SaveChanges();
        }

        using var second = _db.CreateContext();
        second.Sessions.Add(Some.CompletedSession(_db.UserId) with { Source = AHealthRun.Source(workoutId) });

        var refused = Assert.Throws<DbUpdateException>(() => second.SaveChanges());
        Assert.Equal(19, Assert.IsType<SqliteException>(refused.InnerException).SqliteErrorCode);
    }

    [Fact]
    public void A_recording_round_trips_its_route_and_heart_rate()
    {
        var recording = Recording();
        using (var context = _db.CreateContext())
        {
            context.WorkoutRecordings.Add(recording);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.WorkoutRecordings.Single();
        Assert.Equal(recording.Route, loaded.Route);
        Assert.Equal(recording.HeartRate, loaded.HeartRate);
    }

    [Fact]
    public void A_route_is_stored_as_parallel_columns_from_its_first_fix()
    {
        var start = new DateTimeOffset(2026, 9, 20, 5, 0, 0, TimeSpan.Zero);
        var route = new WorkoutRoute(
        [
            new RoutePoint(start, 46.5, 8.25, 400),
            new RoutePoint(start.AddSeconds(1.5), 46.500015, 8.25, null),
        ]);
        using (var context = _db.CreateContext())
        {
            context.WorkoutRecordings.Add(WorkoutRecording.Of(Guid.NewGuid(), _db.UserId, route, RecordedHeartRate.None));
            context.SaveChanges();
        }

        Assert.Equal(
            """{"Start":"2026-09-20T05:00:00+00:00","Ticks":[0,15000000],"Latitude":[46.5,46.500015],"Longitude":[8.25,8.25],"Altitude":[400,null]}""",
            _db.ExecuteScalar("SELECT Route FROM WorkoutRecordings"));
    }

    private WorkoutRecording Recording()
    {
        var start = AHealthRun.Start;
        return WorkoutRecording.Of(
            Guid.NewGuid(),
            _db.UserId,
            AHealthRun.Route(AHealthRun.North(start, 1000, 3, altitude: index => index % 3 == 0 ? null : 400 + index)),
            new RecordedHeartRate([.. Enumerable.Range(0, 20).Select(index =>
                new HeartRateSample(start.AddSeconds(index * 1.25), 140 + index, RecordedHeartRate.SampleSource))]));
    }
}
