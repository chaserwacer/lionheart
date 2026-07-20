using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// The stamped columns only earn their keep if the queries that read them reach SQL. A filter
/// or sort that quietly falls back to client evaluation still returns the right answer on four
/// rows, so correctness assertions alone would never catch it — each test also inspects the
/// generated SQL.
/// </summary>
public sealed class QueryTranslationTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private void Seed()
    {
        using var context = _db.CreateContext();
        context.Sessions.AddRange(
            Some.CompletedSession(_db.UserId) with { Period = new Instant(new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc)) },
            Some.CompletedSession(_db.UserId) with { Period = new Instant(new DateTime(2026, 8, 3, 7, 0, 0, DateTimeKind.Utc)) },
            Some.CompletedSession(_db.UserId) with { Period = new Instant(new DateTime(2026, 7, 4, 18, 0, 0, DateTimeKind.Utc)) },
            Some.PlannedSession(_db.UserId),
            Some.ActiveSession(_db.UserId),
            Some.SkippedSession(_db.UserId));
        context.SaveChanges();
    }

    [Fact]
    public void Completed_sessions_filter_and_order_by_stamped_start_time_in_one_query()
    {
        Seed();
        var windowStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        using var context = _db.CreateContext();
        var query = context.Sessions
            .OfType<CompletedTrainingSession>()
            .Where(s => EF.Property<DateTime>(s, "StartTime") >= windowStart)
            .OrderByDescending(s => EF.Property<DateTime>(s, "StartTime"));

        var sql = query.ToQueryString();
        Assert.Contains("WHERE", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("\"Lifecycle\"", sql);
        Assert.DoesNotContain("SELECT 1", sql);

        var dates = query.Select(s => s.Period.StartTime).ToList();
        Assert.Equal(
            [new DateTime(2026, 8, 3, 7, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 18, 0, 0, DateTimeKind.Utc)],
            dates);
    }

    [Fact]
    public void Of_type_filters_by_discriminator_in_sql_rather_than_in_memory()
    {
        Seed();

        using var context = _db.CreateContext();
        var sql = context.Sessions.OfType<CompletedTrainingSession>().ToQueryString();

        Assert.Contains("'completed'", sql);
        Assert.Equal(3, context.Sessions.OfType<CompletedTrainingSession>().Count());
    }

    [Fact]
    public void Counting_completed_sessions_pushes_the_count_into_sql()
    {
        Seed();

        using var context = _db.CreateContext();
        var sql = context.Sessions.OfType<CompletedTrainingSession>().Select(s => s.TrainingSessionId).ToQueryString();

        Assert.StartsWith("SELECT", sql.TrimStart());
        Assert.Contains("\"Lifecycle\" = 'completed'", sql);
    }

    /// <summary>
    /// Membership is a converted column, so equality against a fill-state value has to become a
    /// Guid comparison in SQL. If this ever falls back to client evaluation, every membership
    /// lookup silently loads the whole Sessions table first.
    /// </summary>
    [Fact]
    public void Membership_equality_translates_to_a_guid_comparison()
    {
        var programId = Guid.NewGuid();
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Membership = new InProgram(programId) });
            context.Sessions.Add(Some.CompletedSession(_db.UserId));
            context.SaveChanges();
        }

        ProgramMembership target = new InProgram(programId);
        using var fresh = _db.CreateContext();
        var query = fresh.Sessions.Where(s => s.Membership == target);

        Assert.Contains("\"TrainingProgramId\"", query.ToQueryString());
        Assert.Equal(programId, Assert.IsType<InProgram>(Assert.Single(query.ToList()).Membership).TrainingProgramId);
    }

    [Fact]
    public void Standalone_membership_is_queryable_as_the_sentinel()
    {
        var programId = Guid.NewGuid();
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Membership = new InProgram(programId) });
            context.Sessions.Add(Some.CompletedSession(_db.UserId));
            context.SaveChanges();
        }

        ProgramMembership standalone = new Standalone();
        using var fresh = _db.CreateContext();

        Assert.Single(fresh.Sessions.Where(s => s.Membership == standalone).ToList());
    }

    [Fact]
    public void Context_window_query_uses_the_composite_user_and_start_time_index()
    {
        using (var context = _db.CreateContext())
        {
            context.Contexts.Add(Some.StateContext(_db.UserId));
            context.Contexts.Add(Some.ExertionContext(_db.UserId) with
            {
                Period = new Instant(new DateTime(2026, 8, 3, 7, 0, 0, DateTimeKind.Utc)),
            });
            context.SaveChanges();
        }

        var windowStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        using var fresh = _db.CreateContext();
        var query = fresh.Contexts
            .Where(c => c.UserId == _db.UserId && EF.Property<DateTime>(c, "StartTime") >= windowStart);

        var sql = query.ToQueryString();
        Assert.Contains("\"UserId\"", sql);
        Assert.Contains("\"StartTime\"", sql);

        Assert.IsType<ExertionContext>(Assert.Single(query.ToList()));
    }

    [Fact]
    public void Analyzable_entries_filter_by_kind_in_sql()
    {
        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId));
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId) with { Kind = EntryKind.Context });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var query = fresh.AnalyzableEntries.Where(e => e.Kind == EntryKind.Session);

        Assert.Contains("\"Kind\"", query.ToQueryString());
        Assert.Equal(EntryKind.Session, Assert.Single(query.ToList()).Kind);
    }

    /// <summary>
    /// Facets live inside a JSON column, so the design says facet filtering loads and filters in
    /// the app. Pinned so that stays a deliberate choice: if someone later writes a facet
    /// predicate expecting SQL to do the work, this is where the assumption is written down.
    /// </summary>
    [Fact]
    public void Facet_filtering_is_an_in_app_pass_over_a_narrowed_window()
    {
        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId));
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId) with
            {
                Facets = [new(FacetAxis.Modality, "run")],
            });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var matching = fresh.AnalyzableEntries
            .Where(e => e.UserId == _db.UserId)
            .AsEnumerable()
            .Where(e => e.Facets.Any(f => f.Axis == FacetAxis.Modality && f.Value == "run"))
            .ToList();

        Assert.Single(matching);
    }
}
