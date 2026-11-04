using lionheart.Functional;
using lionheart.Services.Oura.Import;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Oura;

/// <summary>
/// The archive importer against a miniature archive in the shape Oura exports, and against records
/// broken on purpose.
/// </summary>
public class OuraArchiveImportTests : IDisposable
{
    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // ---- The happy path ----

    [Fact]
    public async Task Every_collection_in_the_archive_is_read_and_stored()
    {
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.ArchiveDirectory));

        Assert.Equal(24, report.Stored);
        Assert.Equal(0, report.Rejected);

        Assert.Equal(
            [("daily_activity", 2), ("daily_readiness", 2), ("daily_sleep", 2), ("daily_spo2", 4),
             ("daily_stress", 2), ("session", 3), ("sleep", 3), ("tag", 3), ("workout", 3)],
            report.Collections
                .Select(collection => (
                    collection.Collection,
                    Stored: collection.Outcome is OuraCollectionOutcome.Read read ? read.Stored : -1))
                .Where(entry => entry.Stored >= 0)
                .OrderBy(entry => entry.Collection)
                .ToArray());
    }

    [Fact]
    public async Task A_collection_the_archive_has_no_file_for_says_so_rather_than_reporting_none()
    {
        // The resilience collection was pulled by the live integration and never archived, so its
        // history begins whenever the sync first runs. A zero would read as "the ring recorded
        // none", which is a different and wrong thing.
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.ArchiveDirectory));

        var resilience = Assert.Single(report.Collections, c => c.Collection == "daily_resilience");
        var missing = Assert.IsType<OuraCollectionOutcome.NoArchiveFile>(resilience.Outcome);
        Assert.Equal("daily_resilience.jsonl", missing.ExpectedName);
    }

    [Fact]
    public async Task A_file_in_the_archive_this_application_does_not_store_is_named_in_the_report()
    {
        // Oura's bedtime advice is archived but deliberately not stored. Reporting the leftovers
        // generally, rather than hardcoding that one name, means a collection added later also
        // surfaces instead of being silently skipped.
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.ArchiveDirectory));

        Assert.Equal(["sleep_time.jsonl"], report.UncollectedFiles);
    }

    [Fact]
    public async Task The_stored_documents_carry_the_days_and_values_the_archive_held()
    {
        await ImportAsync(RecordedPayloads.ArchiveDirectory);

        await using var context = _database.CreateContext();

        var night = await context.OuraSleepPeriods
            .SingleAsync(period => period.Day == new DateOnly(2024, 1, 26));
        Assert.Equal(96, night.Reading.HeartRate.Samples.Count);
        Assert.Equal(6630, night.Reading.DeepSleepSeconds);

        var activity = await context.OuraDailyActivity
            .SingleAsync(day => day.Day == new DateOnly(2024, 1, 25));
        Assert.Equal(47, activity.Reading.Score);
    }

    [Theory]
    [InlineData("vO2_max")]
    [InlineData("daily_cardiovascular_age")]
    public async Task The_cardio_collections_the_archive_never_held_say_so(string collection)
    {
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.ArchiveDirectory));

        var outcome = Assert.Single(report.Collections, c => c.Collection == collection).Outcome;
        Assert.Equal($"{collection}.jsonl", Assert.IsType<OuraCollectionOutcome.NoArchiveFile>(outcome).ExpectedName);
    }

    [Fact]
    public async Task Importing_again_fills_in_the_classification_on_days_stored_without_one()
    {
        // The backfill path: days imported before the classification was kept hold no timeline,
        // and the same archive read again hands each its own, keyed by Oura's id as always.
        await ImportAsync(RecordedPayloads.ArchiveDirectory);
        await using (var context = _database.CreateContext())
        {
            foreach (var day in await context.OuraDailyActivity.ToListAsync())
            {
                day.Apply(day.Day, day.Reading with { Timeline = null });
            }
            await context.SaveChangesAsync();
        }

        await ImportAsync(RecordedPayloads.ArchiveDirectory);

        await using var reread = _database.CreateContext();
        var days = await reread.OuraDailyActivity.ToListAsync();
        Assert.Equal(2, days.Count);
        Assert.All(days, day => Assert.Equal(288, day.Reading.Timeline?.Classes.Length));
    }

    [Fact]
    public async Task Running_the_import_twice_stores_nothing_the_second_time()
    {
        // Idempotency falls out of the schema rather than being arranged: each table is keyed by
        // Oura's own document id, so the second pass applies payloads the change tracker sees as
        // unchanged. Row counts and keys both have to hold, since an upsert that deleted and
        // re-inserted would keep the count while changing every key.
        await ImportAsync(RecordedPayloads.ArchiveDirectory);
        var firstPass = await StoredKeysAsync();

        var second = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.ArchiveDirectory));

        Assert.Equal(24, second.Stored);
        Assert.Equal(firstPass, await StoredKeysAsync());
    }

    // ---- Untrusted input ----

    [Fact]
    public async Task One_unreadable_record_does_not_stop_the_ones_around_it()
    {
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.DamagedDirectory));

        var sleepDays = Assert.Single(report.Collections, c => c.Collection == "daily_sleep");
        var read = Assert.IsType<OuraCollectionOutcome.Read>(sleepDays.Outcome);

        Assert.Equal(8, read.Lines);
        Assert.Equal(2, read.Stored);
        Assert.Equal(6, read.Rejected.Count);
    }

    [Fact]
    public async Task A_record_carrying_a_field_this_application_does_not_know_is_still_stored()
    {
        // Oura adds fields. An unknown member has to be ignored rather than rejected, or the first
        // thing Oura ships breaks every import until the wire records catch up.
        await ImportAsync(RecordedPayloads.DamagedDirectory);

        await using var context = _database.CreateContext();
        Assert.True(await context.OuraDailySleep.AnyAsync(day => day.Day == new DateOnly(2024, 2, 2)));
    }

    [Fact]
    public async Task Each_rejection_names_the_file_the_line_and_what_was_wrong()
    {
        // The whole requirement on rejections: reportable, not silent. A line number is what makes
        // one actionable.
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.DamagedDirectory));

        var byLine = report.Rejections
            .Where(rejection => rejection.File == "daily_sleep.jsonl")
            .ToDictionary(rejection => rejection.Line, rejection => rejection.Reason);

        Assert.Equal([2, 4, 5, 6, 7, 8], byLine.Keys.Order());
        Assert.Contains("id", byLine[2]);
        Assert.Contains("day", byLine[4]);
        Assert.Contains("contributors", byLine[5]);
        Assert.Contains("unreadable JSON", byLine[6]);
        Assert.Contains("unreadable JSON", byLine[7]);
        Assert.Contains("null", byLine[8]);
    }

    [Fact]
    public async Task A_blank_line_is_skipped_without_shifting_the_line_numbers_after_it()
    {
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.DamagedDirectory));

        // Line 3 of the damaged file is blank; the record after it is still reported as line 4.
        Assert.DoesNotContain(
            3, report.Rejections.Where(r => r.File == "daily_sleep.jsonl").Select(r => r.Line));
        Assert.Contains(
            4, report.Rejections.Where(r => r.File == "daily_sleep.jsonl").Select(r => r.Line));
    }

    [Fact]
    public async Task A_night_whose_bedtime_ends_before_it_starts_is_rejected_not_thrown_on()
    {
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.DamagedDirectory));

        var reversed = Assert.Single(
            report.Rejections, rejection => rejection.File == "sleep.jsonl" && rejection.Line == 1);
        Assert.Contains("before its start", reversed.Reason);
    }

    [Fact]
    public async Task A_night_missing_a_sample_run_is_rejected_rather_than_stored_without_it()
    {
        var report = ResultAssert.Succeeded(await ImportAsync(RecordedPayloads.DamagedDirectory));

        var nights = Assert.Single(report.Collections, c => c.Collection == "sleep");
        var read = Assert.IsType<OuraCollectionOutcome.Read>(nights.Outcome);

        Assert.Equal(0, read.Stored);
        Assert.Equal(3, read.Rejected.Count);
        Assert.Contains("heart_rate", read.Rejected[1].Reason);
        Assert.Contains("hrv.items", read.Rejected[2].Reason);
    }

    // ---- Refusals before any file is read ----

    [Fact]
    public async Task An_import_for_an_athlete_who_does_not_exist_is_refused()
    {
        var refused = await new OuraArchiveImporter(_database.CreateContext())
            .ImportAsync(RecordedPayloads.ArchiveDirectory, Guid.NewGuid(), CancellationToken.None);

        var problem = ResultAssert.Failed(refused);
        Assert.IsType<Problem.NotFound>(problem);
    }

    [Fact]
    public async Task An_import_from_a_directory_that_is_not_there_is_refused()
    {
        var refused = await ImportAsync(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid()}"));

        var problem = ResultAssert.Failed(refused);
        var invalid = Assert.IsType<Problem.Invalid>(problem);
        Assert.Contains("No directory at", invalid.Description);
    }

    // ---- Helpers ----

    private async Task<Result<OuraImportReport, Problem>> ImportAsync(string directory)
    {
        await using var context = _database.CreateContext();
        return await new OuraArchiveImporter(context)
            .ImportAsync(directory, _database.UserId, CancellationToken.None);
    }

    /// <summary>Every stored document's key, so a test can tell "unchanged" from "replaced".</summary>
    private async Task<IReadOnlyList<Guid>> StoredKeysAsync()
    {
        await using var context = _database.CreateContext();

        return
        [
            .. (await context.OuraDailyActivity.Select(d => d.OuraId).ToListAsync())
                .Concat(await context.OuraDailyReadiness.Select(d => d.OuraId).ToListAsync())
                .Concat(await context.OuraDailySleep.Select(d => d.OuraId).ToListAsync())
                .Concat(await context.OuraDailySpo2.Select(d => d.OuraId).ToListAsync())
                .Concat(await context.OuraDailyStress.Select(d => d.OuraId).ToListAsync())
                .Concat(await context.OuraSleepPeriods.Select(d => d.OuraId).ToListAsync())
                .Concat(await context.OuraWorkouts.Select(d => d.OuraId).ToListAsync())
                .Concat(await context.OuraSessions.Select(d => d.OuraId).ToListAsync())
                .Concat(await context.OuraTags.Select(d => d.OuraId).ToListAsync())
                .Order()
        ];
    }
}
