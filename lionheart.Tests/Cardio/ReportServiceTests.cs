using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Services.Cardio;
using lionheart.Services.Habits;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Cardio;

/// <summary>
/// The cardio and habits reports through the real schema: the windowed reads, the days they are
/// widened by and filtered back to, and the heart rate read for a session's clock time.
/// </summary>
public class ReportServiceTests : IDisposable
{
    private static readonly TimeSpan Eastern = TimeSpan.FromHours(-4);
    private static readonly DayRange October = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

    private readonly RemodelDatabase _database = new();
    private readonly IdentityUser _user;

    public ReportServiceTests() =>
        _user = new IdentityUser("tester@lionheart.local") { Id = _database.UserId.ToString() };

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task An_evening_run_reads_its_heart_rate_across_the_UTC_midnight_it_crosses()
    {
        // 19:30 to 20:30 in New York is 23:30 to 00:30 UTC: two stored days for one session.
        var start = new DateTimeOffset(2026, 10, 14, 19, 30, 0, Eastern);
        await using (var context = _database.CreateContext())
        {
            (await context.LionheartUsers.SingleAsync()).Age = 20;
            context.OuraWorkouts.Add(new OuraWorkout(Guid.NewGuid(), _database.UserId, new DateOnly(2026, 10, 14),
                new WorkoutReading(new Interval(start, start.AddHours(1)), "running", "moderate", "confirmed", null, null, 8000)));
            context.OuraHeartRateDays.AddRange(
                Day(new DateOnly(2026, 10, 14), start, minutes: 30),
                Day(new DateOnly(2026, 10, 15), start.AddMinutes(30), minutes: 30));
            context.OuraVo2Max.Add(new OuraVo2Max(Guid.NewGuid(), _database.UserId, new DateOnly(2026, 10, 14),
                new Vo2MaxReading(start, 47)));
            await context.SaveChangesAsync();
        }

        var report = ResultAssert.Succeeded(await Cardio().GetReportAsync(_user, October, CancellationToken.None));

        var session = Assert.Single(report.Sessions);
        var recorded = Assert.IsType<SessionHeartRate.Recorded>(session.HeartRate);
        Assert.Equal(1, recorded.Coverage, precision: 6);
        Assert.Equal(60, recorded.Zones!.InZones, precision: 6);
        Assert.Equal([new DayValue(new DateOnly(2026, 10, 14), 47)], report.Vo2Max);
    }

    [Fact]
    public async Task A_cardio_report_longer_than_a_year_is_refused()
    {
        var long_ = new DayRange(new DateOnly(2025, 1, 1), new DateOnly(2026, 10, 31));

        var problem = ResultAssert.Failed(await Cardio().GetReportAsync(_user, long_, CancellationToken.None));

        Assert.IsType<Problem.Invalid>(problem);
    }

    [Fact]
    public async Task A_habits_day_places_the_night_and_the_evening_session_on_the_athletes_clock()
    {
        var day = new DateOnly(2026, 10, 14);
        var dayStart = new DateTimeOffset(2026, 10, 14, 4, 0, 0, Eastern);
        var bedtime = new DateTimeOffset(2026, 10, 14, 0, 30, 0, Eastern);
        await using (var context = _database.CreateContext())
        {
            context.OuraDailyActivity.Add(new OuraDailyActivity(Guid.NewGuid(), _database.UserId, day, Activity(dayStart)));
            context.OuraSleepPeriods.Add(new OuraSleepPeriod(Guid.NewGuid(), _database.UserId, day, Night(bedtime)));
            await context.SaveChangesAsync();
        }

        var detail = ResultAssert.Succeeded(await Habits().GetDayAsync(_user, day, CancellationToken.None));

        Assert.Equal(dayStart, detail.StartsAt);
        Assert.True(detail.Runs[0].IsAsleep);
        Assert.Equal(bedtime, Assert.Single(detail.Sleep).Start);
        Assert.Equal(bedtime, detail.Figures.Bedtime);
    }

    [Fact]
    public async Task A_day_with_no_activity_stored_is_not_found_rather_than_empty()
    {
        var problem = ResultAssert.Failed(
            await Habits().GetDayAsync(_user, new DateOnly(2026, 10, 14), CancellationToken.None));

        Assert.IsType<Problem.NotFound>(problem);
    }

    // ---- Builders ----

    private CardioReportService Cardio() => new(_database.CreateContext());

    private HabitsReportService Habits() => new(_database.CreateContext());

    /// <summary>A UTC day holding a sample a minute at 130 bpm from <paramref name="from"/>.</summary>
    private OuraHeartRateDay Day(DateOnly utcDay, DateTimeOffset from, int minutes) =>
        new(_database.UserId, utcDay, new HeartRateDayReading(
        [
            .. Enumerable.Range(0, minutes)
                .Select(minute => new HeartRateSample(from.AddMinutes(minute).ToUniversalTime(), 130, "workout"))
        ]));

    private static DailyActivityReading Activity(DateTimeOffset start) => new(
        80, new ActivityContributors(80, 80, 80, 80, 80, 80), 9000, 400, 2800, 450, 7000, 0, 9000, 0, 1.4,
        150, 100, 10, 10, 30000, 30000, 12000, 2000, 300, 0,
        new ActivityTimeline(start, new string('1', 72) + new string('3', 216)));

    private static SleepPeriodReading Night(DateTimeOffset bedtime)
    {
        var empty = new OuraSampleSeries(300, [], bedtime);
        return new SleepPeriodReading(
            new Interval(bedtime, bedtime.AddHours(9)), SleepPeriodReading.MainSleepKind, 0, 32400, 28800, 5400,
            16200, 7200, 3600, 600, 90, 10, 50, 44, 90, 14, null, null, null, null, "4222", empty, empty);
    }
}
