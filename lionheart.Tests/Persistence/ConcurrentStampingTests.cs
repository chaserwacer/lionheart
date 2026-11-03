using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// StampDerivedTimeColumns walks the whole ChangeTracker on every save and writes a shadow
/// property per entry. With several stamped entities in flight — same context or two contexts
/// on one connection — the risk is one entry's Period landing on another entry's column, which
/// no round-trip test would see because the JSON stays right.
/// </summary>
public sealed class ConcurrentStampingTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static Instant At(int day) => new(new DateTime(2026, 7, day, 6, 0, 0, DateTimeKind.Utc));

    private DateTime StampFor(Guid sessionId) =>
        _db.ReadDateTimeColumn($"SELECT StartTime FROM Sessions WHERE TrainingSessionId = '{RemodelDatabase.SqlGuid(sessionId)}'");

    [Fact]
    public void Many_sessions_in_one_save_each_get_their_own_period()
    {
        var sessions = Enumerable.Range(1, 10)
            .Select(day => Some.CompletedSession(_db.UserId) with { Period = At(day) })
            .ToList();

        using (var context = _db.CreateContext())
        {
            context.Sessions.AddRange(sessions);
            context.SaveChanges();
        }

        foreach (var session in sessions)
        {
            Assert.Equal(session.Period.StartTimeUtc, StampFor(session.TrainingSessionId));
        }
    }

    [Fact]
    public void Mixed_stamped_entity_types_in_one_save_do_not_cross_contaminate()
    {
        var session = Some.CompletedSession(_db.UserId) with { Period = At(1) };
        var stateContext = Some.StateContext(_db.UserId) with { Period = At(2) };
        var entry = Some.AnalyzableEntry(_db.UserId) with { Period = At(3) };

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.Contexts.Add(stateContext);
            context.AnalyzableEntries.Add(entry);
            context.SaveChanges();
        }

        Assert.Equal(At(1).At.UtcDateTime, StampFor(session.TrainingSessionId));
        Assert.Equal(At(2).At.UtcDateTime, _db.ReadDateTimeColumn("SELECT StartTime FROM Contexts"));
        Assert.Equal(At(3).At.UtcDateTime, _db.ReadDateTimeColumn("SELECT StartTime FROM AnalyzableEntries"));
    }

    /// <summary>
    /// Unstamped lifecycle states share the Sessions table. The stamp must skip them rather than
    /// borrow a neighbour's period — a planned session carrying a completed session's StartTime
    /// would surface in every completed-session window query.
    /// </summary>
    [Fact]
    public void Unstamped_lifecycle_states_are_left_null_alongside_a_stamped_one()
    {
        var completed = Some.CompletedSession(_db.UserId) with { Period = At(1) };
        var planned = Some.PlannedSession(_db.UserId);

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(completed);
            context.Sessions.Add(planned);
            context.SaveChanges();
        }

        Assert.Equal(At(1).At.UtcDateTime, StampFor(completed.TrainingSessionId));
        Assert.Null(_db.ExecuteScalar(
            $"SELECT StartTime FROM Sessions WHERE TrainingSessionId = '{RemodelDatabase.SqlGuid(planned.TrainingSessionId)}'"));
    }

    [Fact]
    public void Two_contexts_on_one_connection_stamp_their_own_entities()
    {
        var first = Some.CompletedSession(_db.UserId) with { Period = At(1) };
        var second = Some.CompletedSession(_db.UserId) with { Period = At(2) };

        using var contextA = _db.CreateContext();
        using var contextB = _db.CreateContext();
        contextA.Sessions.Add(first);
        contextB.Sessions.Add(second);

        contextA.SaveChanges();
        contextB.SaveChanges();

        Assert.Equal(At(1).At.UtcDateTime, StampFor(first.TrainingSessionId));
        Assert.Equal(At(2).At.UtcDateTime, StampFor(second.TrainingSessionId));
    }

    [Fact]
    public void Interleaved_edits_across_two_contexts_keep_each_stamp_true_to_its_own_period()
    {
        var first = Some.CompletedSession(_db.UserId) with { Period = At(1) };
        var second = Some.CompletedSession(_db.UserId) with { Period = At(2) };

        using (var seed = _db.CreateContext())
        {
            seed.Sessions.AddRange(first, second);
            seed.SaveChanges();
        }

        using var contextA = _db.CreateContext();
        using var contextB = _db.CreateContext();
        var trackedA = contextA.Sessions.Single(s => s.TrainingSessionId == first.TrainingSessionId);
        var trackedB = contextB.Sessions.Single(s => s.TrainingSessionId == second.TrainingSessionId);

        contextA.Entry(trackedA).Property(nameof(CompletedTrainingSession.Period)).CurrentValue = At(20);
        contextB.Entry(trackedB).Property(nameof(CompletedTrainingSession.Period)).CurrentValue = At(21);

        contextA.SaveChanges();
        contextB.SaveChanges();

        Assert.Equal(At(20).At.UtcDateTime, StampFor(first.TrainingSessionId));
        Assert.Equal(At(21).At.UtcDateTime, StampFor(second.TrainingSessionId));
    }

    /// <summary>
    /// An entity tracked but untouched must not be restamped: the stamp only runs for Added and
    /// Modified entries, so a save triggered by a neighbour leaves it alone.
    /// </summary>
    [Fact]
    public void Saving_one_entity_does_not_restamp_an_untouched_sibling()
    {
        var untouched = Some.CompletedSession(_db.UserId) with { Period = At(1) };
        using (var seed = _db.CreateContext())
        {
            seed.Sessions.Add(untouched);
            seed.SaveChanges();
        }

        using var context = _db.CreateContext();
        _ = context.Sessions.Single();
        var newcomer = Some.CompletedSession(_db.UserId) with { Period = At(9) };
        context.Sessions.Add(newcomer);
        context.SaveChanges();

        Assert.Equal(At(1).At.UtcDateTime, StampFor(untouched.TrainingSessionId));
        Assert.Equal(At(9).At.UtcDateTime, StampFor(newcomer.TrainingSessionId));
    }
}
