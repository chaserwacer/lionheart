using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// A moment has to answer two different questions, and each needs a different half of it. The
/// stamped StartTime column is offset-free SQLite TEXT and holds the absolute instant, so windows
/// and ordering work on one timeline. The JSON Period keeps the offset the entry was logged at,
/// which is the only thing that can say which calendar day the athlete trained on. These cover
/// both halves, the precision that survives each, and the cases where the two disagree.
/// </summary>
public sealed class DateTimeFidelityTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private CompletedTrainingSession SaveAndReload(Period period)
    {
        var session = Some.CompletedSession(_db.UserId) with { Period = period };
        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(session);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        return (CompletedTrainingSession)fresh.Sessions.Single(s => s.TrainingSessionId == session.TrainingSessionId);
    }

    [Fact]
    public void Sub_second_ticks_survive_the_json_column()
    {
        var precise = new DateTime(2026, 7, 18, 9, 30, 0, DateTimeKind.Utc).AddTicks(1234567);

        var loaded = SaveAndReload(new Instant(precise));

        Assert.Equal(precise.Ticks, loaded.Period.StartTime.Ticks);
    }

    [Fact]
    public void Sub_second_interval_endpoints_survive_the_json_column()
    {
        var start = new DateTime(2026, 7, 18, 9, 30, 0, DateTimeKind.Utc).AddTicks(1);
        var end = start.AddTicks(3);

        var interval = Assert.IsType<Interval>(SaveAndReload(new Interval(start, end)).Period);

        Assert.Equal(start.Ticks, interval.Start.Ticks);
        Assert.Equal(end.Ticks, interval.End.Ticks);
        Assert.Equal(TimeSpan.FromTicks(3), interval.Duration);
    }

    /// <summary>
    /// The offset a session was logged at survives storage — it is the only thing that can
    /// answer which calendar day the athlete trained on.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(9)]
    public void The_captured_offset_survives_the_json_column(int offsetHours)
    {
        var moment = new DateTimeOffset(2026, 7, 18, 9, 30, 0, TimeSpan.FromHours(offsetHours));

        var loaded = SaveAndReload(new Instant(moment));

        Assert.Equal(TimeSpan.FromHours(offsetHours), loaded.Period.StartTime.Offset);
        Assert.Equal(moment, loaded.Period.StartTime);
    }

    /// <summary>
    /// The case that motivated carrying the offset: an evening session west of Greenwich is
    /// tomorrow in UTC, but the athlete trained today.
    /// </summary>
    [Fact]
    public void An_evening_session_keeps_the_athletes_day_not_the_utc_one()
    {
        var lateEveningInNewYork = new DateTimeOffset(2026, 7, 18, 23, 30, 0, TimeSpan.FromHours(-4));

        var loaded = SaveAndReload(new Instant(lateEveningInNewYork));

        Assert.Equal(new DateOnly(2026, 7, 18), loaded.Period.LocalDate);
        Assert.Equal(new DateOnly(2026, 7, 19), DateOnly.FromDateTime(loaded.Period.StartTimeUtc));
    }

    [Fact]
    public void An_early_morning_session_east_of_greenwich_keeps_the_athletes_day()
    {
        var earlyMorningInTokyo = new DateTimeOffset(2026, 7, 18, 6, 0, 0, TimeSpan.FromHours(9));

        var loaded = SaveAndReload(new Instant(earlyMorningInTokyo));

        Assert.Equal(new DateOnly(2026, 7, 18), loaded.Period.LocalDate);
        Assert.Equal(new DateOnly(2026, 7, 17), DateOnly.FromDateTime(loaded.Period.StartTimeUtc));
    }

    [Fact]
    public void An_interval_orders_its_endpoints_by_absolute_instant_not_wall_clock()
    {
        var startInNewYork = new DateTimeOffset(2026, 7, 18, 20, 0, 0, TimeSpan.FromHours(-4));
        var endInUtc = startInNewYork.AddMinutes(75).ToUniversalTime();

        var interval = new Interval(startInNewYork, endInUtc);

        Assert.Equal(TimeSpan.FromMinutes(75), interval.Duration);
        Assert.Equal(TimeSpan.FromHours(-4), interval.Start.Offset);
    }

    /// <summary>
    /// Ordering is absolute, not by wall clock: these endpoints read 09:00 then 12:00, which
    /// looks forward, but the first is 13:00 UTC and the second 12:00 UTC.
    /// </summary>
    [Fact]
    public void An_interval_that_only_looks_forward_on_the_wall_clock_is_refused() =>
        Assert.Throws<ArgumentException>(() => new Interval(
            new DateTimeOffset(2026, 7, 18, 9, 0, 0, TimeSpan.FromHours(-4)),
            new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Stamped_column_carries_the_absolute_utc_instant()
    {
        var lateEveningInNewYork = new DateTimeOffset(2026, 7, 18, 23, 30, 0, TimeSpan.FromHours(-4));

        SaveAndReload(new Instant(lateEveningInNewYork));

        Assert.Equal(lateEveningInNewYork.UtcDateTime, _db.ReadDateTimeColumn("SELECT StartTime FROM Sessions"));
    }

    /// <summary>
    /// The index stays one timeline: the same real moment recorded at two different offsets is
    /// one point in StartTime, even though the two sessions belong to different calendar days.
    /// </summary>
    [Fact]
    public void Same_instant_at_two_offsets_indexes_identically_but_dates_differently()
    {
        var inNewYork = new DateTimeOffset(2026, 7, 18, 20, 0, 0, TimeSpan.FromHours(-4));
        var sameMomentInUtc = inNewYork.ToUniversalTime();

        using (var context = _db.CreateContext())
        {
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Period = new Instant(inNewYork) });
            context.Sessions.Add(Some.CompletedSession(_db.UserId) with { Period = new Instant(sameMomentInUtc) });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var sessions = fresh.Sessions.OfType<CompletedTrainingSession>().ToList();
        var stamped = sessions.Select(s => fresh.Entry(s).Property<DateTime>("StartTime").CurrentValue).Distinct();

        Assert.Single(stamped);
        Assert.Equal(
            [new DateOnly(2026, 7, 18), new DateOnly(2026, 7, 19)],
            sessions.Select(s => s.Date).OrderBy(d => d));
    }

    [Fact]
    public void Window_query_orders_correctly_across_a_sub_second_boundary()
    {
        var baseline = new DateTime(2026, 7, 18, 9, 30, 0, DateTimeKind.Utc);
        var earlier = baseline.AddTicks(1);
        var later = baseline.AddTicks(2);

        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId) with { Period = new Instant(later) });
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId) with { Period = new Instant(earlier) });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var ordered = fresh.AnalyzableEntries
            .OrderBy(e => EF.Property<DateTime>(e, "StartTime"))
            .Select(e => e.Period.StartTime)
            .ToList();

        Assert.Equal([earlier, later], ordered);
    }

    [Fact]
    public void Window_query_boundary_is_inclusive_at_the_exact_tick()
    {
        var boundary = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        using (var context = _db.CreateContext())
        {
            context.AnalyzableEntries.Add(Some.AnalyzableEntry(_db.UserId) with { Period = new Instant(boundary) });
            context.AnalyzableEntries.Add(
                Some.AnalyzableEntry(_db.UserId) with { Period = new Instant(boundary.AddTicks(-1)) });
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var inWindow = fresh.AnalyzableEntries
            .Where(e => EF.Property<DateTime>(e, "StartTime") >= boundary)
            .ToList();

        Assert.Equal(boundary, Assert.Single(inWindow).Period.StartTime);
    }

    [Fact]
    public void Date_only_extremes_round_trip_on_a_planned_session()
    {
        foreach (var day in new[] { DateOnly.MinValue, DateOnly.MaxValue })
        {
            var session = Some.PlannedSession(_db.UserId) with { PlannedFor = day };
            using (var context = _db.CreateContext())
            {
                context.Sessions.Add(session);
                context.SaveChanges();
            }

            using var fresh = _db.CreateContext();
            var loaded = (PlannedTrainingSession)fresh.Sessions.Single(s => s.TrainingSessionId == session.TrainingSessionId);
            Assert.Equal(day, loaded.PlannedFor);
            Assert.Equal(day, loaded.Date);
        }
    }
}
