using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// Routines and the origin a session carries back to one, through real SQLite: the aggregate's
/// movement order in its JSON column, and the origin's fill-state as a Guid sentinel.
/// </summary>
public sealed class RoutinePersistenceTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void A_routine_round_trips_with_its_movement_order()
    {
        var routine = Some.UpperDay(_db.UserId);
        using (var context = _db.CreateContext())
        {
            context.Routines.Add(routine);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.Routines.Single();

        Assert.Equal(
            (routine.RoutineId, routine.UserId, routine.Name, routine.Notes, routine.CreatedAt),
            (loaded.RoutineId, loaded.UserId, loaded.Name, loaded.Notes, loaded.CreatedAt));
        Assert.Equal(routine.Movements, loaded.Movements);
    }

    [Fact]
    public void A_revised_routine_is_saved_in_place()
    {
        var routine = Some.UpperDay(_db.UserId);
        using (var context = _db.CreateContext())
        {
            context.Routines.Add(routine);
            context.SaveChanges();
        }

        var movement = Guid.NewGuid();
        using (var context = _db.CreateContext())
        {
            context.Routines.Single().Revise(
                ResultAssert.Succeeded(RoutineContent.Of("Upper B", string.Empty, [new RoutineMovement(movement, 4)])));
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.Routines.Single();
        Assert.Equal(("Upper B", routine.RoutineId), (loaded.Name, loaded.RoutineId));
        Assert.Equal([new RoutineMovement(movement, 4)], loaded.Movements);
    }

    [Fact]
    public void Deleting_a_user_cascades_to_their_routines()
    {
        using (var context = _db.CreateContext())
        {
            context.Routines.Add(Some.UpperDay(_db.UserId));
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            context.LionheartUsers.Remove(context.LionheartUsers.Single(u => u.UserID == _db.UserId));
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Empty(fresh.Routines);
    }

    [Fact]
    public void A_session_trained_from_a_routine_round_trips_its_origin()
    {
        var routineId = Guid.NewGuid();
        var session = Some.CompletedSession(_db.UserId) with { Origin = new RoutineOrigin.FromRoutine(routineId) };
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(new RoutineOrigin.FromRoutine(routineId), fresh.Sessions.Single().Origin);
        Assert.Equal(
            RemodelDatabase.SqlGuid(routineId),
            _db.ExecuteScalar("SELECT RoutineId FROM Sessions"));
    }

    [Fact]
    public void An_ad_hoc_session_stores_the_empty_guid_and_reads_back_ad_hoc()
    {
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId));
            context.SaveChanges();
        }

        Assert.Equal(RemodelDatabase.SqlGuid(Guid.Empty), _db.ExecuteScalar("SELECT RoutineId FROM Sessions"));
        using var fresh = _db.CreateContext();
        Assert.IsType<RoutineOrigin.AdHoc>(fresh.Sessions.Single().Origin);
    }

    [Fact]
    public void Deleting_a_routine_leaves_the_sessions_trained_from_it_naming_it()
    {
        var routine = Some.UpperDay(_db.UserId);
        using (var context = _db.CreateContext())
        {
            context.Routines.Add(routine);
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with
            {
                Origin = new RoutineOrigin.FromRoutine(routine.RoutineId),
            });
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            context.Routines.Remove(context.Routines.Single());
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        Assert.Equal(new RoutineOrigin.FromRoutine(routine.RoutineId), fresh.Sessions.Single().Origin);
    }

    /// <summary>
    /// The routine screen's last-session read filters on the origin. It must reach SQL as a
    /// comparison on the indexed column, not load every session to compare in app.
    /// </summary>
    [Fact]
    public void Origin_equality_translates_to_a_comparison_on_the_routine_column()
    {
        var routineId = Guid.NewGuid();
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Origin = new RoutineOrigin.FromRoutine(routineId) });
            context.Sessions.Add(Some.CompletedSession(_db.UserId));
            context.SaveChanges();
        }

        RoutineOrigin target = new RoutineOrigin.FromRoutine(routineId);
        RoutineOrigin adHoc = new RoutineOrigin.AdHoc();
        using var fresh = _db.CreateContext();
        var fromRoutine = fresh.Sessions.Where(s => s.Origin == target);

        Assert.Contains("\"RoutineId\"", fromRoutine.ToQueryString());
        Assert.Equal(target, Assert.Single(fromRoutine.ToList()).Origin);
        Assert.Equal(target, Assert.Single(fresh.Sessions.Where(s => s.Origin != adHoc).ToList()).Origin);
    }
}
