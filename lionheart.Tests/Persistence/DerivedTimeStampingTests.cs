using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// Time-truth lives inside JSON Period columns; StampDerivedTimeColumns duplicates it into
/// a real StartTime column on every save. These pin the stamp on insert, its refresh on
/// update (the drift-proofing claim), and that the stamped column actually serves queries.
/// </summary>
public sealed class DerivedTimeStampingTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Completed_session_start_time_column_matches_its_period()
    {
        var session = Some.CompletedSession(_db.UserId);

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        Assert.Equal(session.Period.StartTime, _db.ReadDateTimeColumn("SELECT StartTime FROM Sessions"));
    }

    [Fact]
    public void Context_start_time_column_matches_its_instant()
    {
        var stateContext = Some.StateContext(_db.UserId);

        using (var context = _db.CreateContext())
        {
            context.Contexts.Add(stateContext);
            context.SaveChanges();
        }

        Assert.Equal(stateContext.Period.StartTime, _db.ReadDateTimeColumn("SELECT StartTime FROM Contexts"));
    }

    [Fact]
    public void Analyzable_entry_start_time_column_matches_its_period()
    {
        var entry = Some.AnalyzableEntry(_db.UserId);

        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(entry);
            context.SaveChanges();
        }

        Assert.Equal(entry.Period.StartTime, _db.ReadDateTimeColumn("SELECT StartTime FROM AnalyzableEntries"));
    }

    [Fact]
    public void Restamping_on_update_keeps_the_column_true_to_a_changed_period()
    {
        var session = Some.CompletedSession(_db.UserId);

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        var movedTo = new DateTime(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
        using (var context = _db.CreateContext())
        {
            var moved = session with { Period = new Instant(movedTo) };
            context.Update(moved);
            context.SaveChanges();
        }

        Assert.Equal(movedTo, _db.ReadDateTimeColumn("SELECT StartTime FROM Sessions"));
    }

    [Fact]
    public void Stamped_column_serves_time_window_queries()
    {
        var july = Some.AnalyzableEntry(_db.UserId);
        var august = Some.AnalyzableEntry(_db.UserId) with
        {
            Period = new Instant(new DateTime(2026, 8, 3, 7, 0, 0, DateTimeKind.Utc)),
        };

        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.AddRange(july, august);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var windowStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var inWindow = fresh.AnalyzableEntries
            .Where(e => EF.Property<DateTime>(e, "StartTime") >= windowStart)
            .ToList();

        Assert.Equal(august.EntryId, Assert.Single(inWindow).EntryId);
    }
}
