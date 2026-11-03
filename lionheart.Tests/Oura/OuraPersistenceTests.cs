using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Services.Oura;
using lionheart.Tests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Oura;

/// <summary>
/// The Oura tables through the real schema: what survives a round trip, and what the columns the
/// domain never names actually hold.
/// </summary>
public class OuraPersistenceTests : IDisposable
{
    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_daily_document_round_trips_with_every_measure_and_every_absence()
    {
        var reading = Some.ActivityReading with { Score = null };

        await using (var context = _database.CreateContext())
        {
            context.OuraDailyActivity.Add(
                new OuraDailyActivity(OuraId, _database.UserId, new DateOnly(2026, 3, 14), reading));
            await context.SaveChangesAsync();
        }

        await using var reread = _database.CreateContext();
        var stored = await reread.OuraDailyActivity.SingleAsync();

        Assert.Equal(OuraId, stored.OuraId);
        Assert.Equal(new DateOnly(2026, 3, 14), stored.Day);
        Assert.Equal(reading, stored.Reading);
        Assert.Null(stored.Reading.Score);
    }

    [Fact]
    public async Task A_sleep_period_round_trips_its_sample_runs_including_the_gaps_in_them()
    {
        // Materialization through a JSON column breaks silently, and a run of samples with holes
        // in it is the shape most likely to come back subtly wrong — a null collapsed to zero
        // would read as a heart rate of zero at that minute of the night.
        var reading = Some.SleepReading;

        await using (var context = _database.CreateContext())
        {
            context.OuraSleepPeriods.Add(
                new OuraSleepPeriod(OuraId, _database.UserId, new DateOnly(2026, 3, 14), reading));
            await context.SaveChangesAsync();
        }

        await using var reread = _database.CreateContext();
        var stored = await reread.OuraSleepPeriods.SingleAsync();

        Assert.Equal(reading, stored.Reading);
        Assert.Equal([52, null, 49], stored.Reading.HeartRate.Samples);
        Assert.Equal(300, stored.Reading.HeartRate.IntervalSeconds);
        Assert.Equal(reading.Hypnogram, stored.Reading.Hypnogram);
    }

    [Fact]
    public async Task An_events_period_keeps_the_offset_it_was_recorded_at()
    {
        await using (var context = _database.CreateContext())
        {
            context.OuraSleepPeriods.Add(
                new OuraSleepPeriod(OuraId, _database.UserId, new DateOnly(2026, 3, 14), Some.SleepReading));
            await context.SaveChangesAsync();
        }

        await using var reread = _database.CreateContext();
        var period = Assert.IsType<Interval>((await reread.OuraSleepPeriods.SingleAsync()).Period);

        Assert.Equal(TimeSpan.FromHours(-4), period.Start.Offset);
        Assert.Equal(Bedtime, period.Start);
    }

    [Fact]
    public async Task An_events_start_instant_is_stamped_into_the_indexed_column_windows_query()
    {
        await using (var context = _database.CreateContext())
        {
            context.OuraSleepPeriods.Add(
                new OuraSleepPeriod(OuraId, _database.UserId, new DateOnly(2026, 3, 14), Some.SleepReading));
            await context.SaveChangesAsync();
        }

        var stamped = _database.ReadDateTimeColumn(
            $"select StartTime from OuraSleepPeriods where OuraId = '{RemodelDatabase.SqlGuid(OuraId)}'");

        Assert.Equal(Bedtime.UtcDateTime, stamped);
    }

    [Fact]
    public async Task The_period_is_stored_once_inside_the_reading_and_not_again_as_a_column()
    {
        // Period is derived from the reading rather than mapped, so a revision cannot leave two
        // copies of "when" disagreeing. This asserts the schema actually reflects that.
        await using (var context = _database.CreateContext())
        {
            context.OuraSleepPeriods.Add(
                new OuraSleepPeriod(OuraId, _database.UserId, new DateOnly(2026, 3, 14), Some.SleepReading));
            await context.SaveChangesAsync();
        }

        var columns = _database.ExecuteScalar(
            "select group_concat(name) from pragma_table_info('OuraSleepPeriods')") as string;

        Assert.Equal("OuraId,StartTime,UserId,Day,Reading", columns);
    }

    [Fact]
    public async Task A_revised_document_replaces_the_payload_on_the_row_it_already_had()
    {
        // What a re-sync does when Oura has reprocessed a day: one row, the newer payload, the
        // same key — not a second row and not a delete-and-reinsert.
        await using (var context = _database.CreateContext())
        {
            context.OuraDailyActivity.Add(
                new OuraDailyActivity(OuraId, _database.UserId, Day, Some.ActivityReading with { Score = 50 }));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            await context.OuraDailyActivity.UpsertAsync<OuraDailyActivity, DailyActivityReading>(
                [new OuraDailyActivity(OuraId, _database.UserId, Day, Some.ActivityReading with { Score = 83 })],
                CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var reread = _database.CreateContext();
        var stored = await reread.OuraDailyActivity.SingleAsync();

        Assert.Equal(OuraId, stored.OuraId);
        Assert.Equal(83, stored.Reading.Score);
    }

    [Fact]
    public async Task A_revision_that_moves_the_day_moves_the_indexed_column_with_it()
    {
        // Day and Period are two deliberately separate facts; this is the one way they could come
        // to disagree, so the upsert replaces both.
        await using (var context = _database.CreateContext())
        {
            context.OuraSleepPeriods.Add(
                new OuraSleepPeriod(OuraId, _database.UserId, Day, Some.SleepReading));
            await context.SaveChangesAsync();
        }

        var restaged = Some.SleepReading with { Period = new Interval(Bedtime.AddDays(1), Bedtime.AddDays(1).AddHours(8)) };

        await using (var context = _database.CreateContext())
        {
            await context.OuraSleepPeriods.UpsertAsync<OuraSleepPeriod, SleepPeriodReading>(
                [new OuraSleepPeriod(OuraId, _database.UserId, Day.AddDays(1), restaged)],
                CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var reread = _database.CreateContext();
        var stored = await reread.OuraSleepPeriods.SingleAsync();

        Assert.Equal(Day.AddDays(1), stored.Day);
        Assert.Equal(Bedtime.AddDays(1), stored.Period.StartTime);
        Assert.Equal(
            Bedtime.AddDays(1).UtcDateTime,
            _database.ReadDateTimeColumn(
                $"select StartTime from OuraSleepPeriods where OuraId = '{RemodelDatabase.SqlGuid(OuraId)}'"));
    }

    [Fact]
    public async Task Upserting_an_unchanged_document_writes_nothing()
    {
        // The property idempotency rests on: the JSON column's comparer sees an identical payload
        // as unchanged, so a second import or sync produces no UPDATE at all.
        await using (var context = _database.CreateContext())
        {
            context.OuraDailyActivity.Add(new OuraDailyActivity(OuraId, _database.UserId, Day, Some.ActivityReading));
            await context.SaveChangesAsync();
        }

        await using var again = _database.CreateContext();
        await again.OuraDailyActivity.UpsertAsync<OuraDailyActivity, DailyActivityReading>(
            [new OuraDailyActivity(OuraId, _database.UserId, Day, Some.ActivityReading)],
            CancellationToken.None);

        Assert.Equal(0, await again.SaveChangesAsync());
    }

    [Fact]
    public async Task One_batch_carrying_a_document_twice_stores_it_once()
    {
        // Oura has sent duplicate pages, and a caller may hand over overlapping ranges. Without
        // the de-duplication this would be a primary-key violation rather than an upsert.
        await using var context = _database.CreateContext();

        await context.OuraDailyActivity.UpsertAsync<OuraDailyActivity, DailyActivityReading>(
            [
                new OuraDailyActivity(OuraId, _database.UserId, Day, Some.ActivityReading with { Score = 50 }),
                new OuraDailyActivity(OuraId, _database.UserId, Day, Some.ActivityReading with { Score = 83 }),
            ],
            CancellationToken.None);
        await context.SaveChangesAsync();

        await using var reread = _database.CreateContext();
        Assert.Equal(83, (await reread.OuraDailyActivity.SingleAsync()).Reading.Score);
    }

    [Fact]
    public async Task Deleting_the_athlete_takes_their_wearable_data_with_them()
    {
        // Device readings are derived data about one person: left behind, they would go on
        // answering queries for an athlete who no longer exists. Same reasoning as AnalyzableEntry.
        await using (var context = _database.CreateContext())
        {
            context.OuraDailyActivity.Add(new OuraDailyActivity(OuraId, _database.UserId, Day, Some.ActivityReading));
            context.OuraSleepPeriods.Add(
                new OuraSleepPeriod(Guid.NewGuid(), _database.UserId, Day, Some.SleepReading));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            context.LionheartUsers.Remove(
                await context.LionheartUsers.SingleAsync(user => user.UserID == _database.UserId));
            await context.SaveChangesAsync();
        }

        await using var reread = _database.CreateContext();
        Assert.Empty(await reread.OuraDailyActivity.ToListAsync());
        Assert.Empty(await reread.OuraSleepPeriods.ToListAsync());
    }

    private static readonly Guid OuraId = Guid.Parse("74a6336b-ba4f-4c24-be80-d51a32eb5629");
    private static readonly DateOnly Day = new(2026, 3, 14);

    private static readonly DateTimeOffset Bedtime =
        new(2026, 3, 14, 1, 7, 1, TimeSpan.FromHours(-4));

    /// <summary>
    /// Readings valid by default, for tests that care about one thing on them. Named for the
    /// domain state rather than the contents, so a test reads as its own premise.
    /// </summary>
    private static class Some
    {
        public static DailyActivityReading ActivityReading => new(
            Score: 83,
            Contributors: new ActivityContributors(1, 100, 100, 100, 1, 2),
            Steps: 9512,
            ActiveCalories: 401,
            TotalCalories: 2850,
            TargetCalories: 450,
            EquivalentWalkingDistanceMeters: 6902,
            MetersToTarget: -3400,
            TargetMeters: 9000,
            InactivityAlerts: 0,
            AverageMet: 1.40625,
            LowActivityMetMinutes: 149,
            MediumActivityMetMinutes: 137,
            HighActivityMetMinutes: 8,
            SedentaryMetMinutes: 11,
            RestingTimeSeconds: 34980,
            SedentaryTimeSeconds: 32040,
            LowActivityTimeSeconds: 12000,
            MediumActivityTimeSeconds: 2160,
            HighActivityTimeSeconds: 60,
            NonWearTimeSeconds: 4140);

        public static SleepPeriodReading SleepReading => new(
            Period: new Interval(Bedtime, Bedtime.AddHours(8)),
            Kind: SleepPeriodReading.MainSleepKind,
            PeriodIndex: 0,
            TimeInBedSeconds: 28800,
            TotalSleepSeconds: 25200,
            DeepSleepSeconds: 5400,
            LightSleepSeconds: 14400,
            RemSleepSeconds: 5400,
            AwakeSeconds: 3600,
            LatencySeconds: 480,
            Efficiency: 88,
            RestlessPeriods: 20,
            AverageHeartRate: 52.5,
            LowestHeartRate: 47,
            AverageHrv: 70,
            AverageBreath: 14.5,
            ReadinessScoreDelta: 0,
            SleepScoreDelta: 0,
            AlgorithmVersion: "v2",
            AnalysisReason: "foreground_sleep_analysis",
            Hypnogram: "4221111",
            HeartRate: new OuraSampleSeries(300, [52, null, 49], Bedtime),
            Hrv: new OuraSampleSeries(300, [70, null, 72], Bedtime));
    }
}
