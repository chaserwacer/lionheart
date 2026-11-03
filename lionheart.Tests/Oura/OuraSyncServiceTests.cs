using System.Net;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.User;
using lionheart.Services.Oura;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Oura;

/// <summary>
/// The sync orchestrator over a faked Oura API.
/// </summary>
/// <remarks>
/// The live pull is the one deliverable that cannot be checked against the real thing — it needs
/// the athlete's credential — so everything short of the network is asserted here: that all ten
/// collections are asked for, that the archived payloads they answer with are stored, that a
/// document Oura sends which cannot be read is reported rather than fatal, and that the failures
/// come back as the problems the transport edge knows how to turn into a status.
/// </remarks>
public class OuraSyncServiceTests : IAsyncLifetime
{
    private static readonly DayRange Week =
        new(new DateOnly(2026, 7, 25), new DateOnly(2026, 7, 31));

    private readonly RemodelDatabase _database = new();
    private readonly FakeOuraApi _api = new();
    private IdentityUser _user = null!;

    public async Task InitializeAsync()
    {
        _user = new IdentityUser("tester@lionheart.local") { Id = _database.UserId.ToString() };

        await using var context = _database.CreateContext();
        context.ApiAccessTokens.Add(new ApiAccessToken
        {
            ObjectID = Guid.NewGuid(),
            UserID = _database.UserId,
            ApplicationName = "oura",
            PersonalAccessToken = "token-abc",
        });
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        _database.Dispose();
        return Task.CompletedTask;
    }

    // ---- What it asks Oura for ----

    [Fact]
    public async Task Every_collection_this_application_stores_is_asked_for_once()
    {
        _api.AnswersEverythingElseEmpty();

        ResultAssert.Succeeded(await Sync());

        Assert.Equal(
            ["daily_activity", "daily_cardiovascular_age", "daily_readiness", "daily_resilience",
             "daily_sleep", "daily_spo2", "daily_stress", "heartrate", "session", "sleep", "tag",
             "vO2_max", "workout"],
            _api.RequestedUris.Select(CollectionOf).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_stored_credential_is_sent_as_the_bearer_token()
    {
        _api.AnswersEverythingElseEmpty();

        await Sync();

        Assert.All(_api.SentTokens, token => Assert.Equal("token-abc", token));
    }

    [Fact]
    public async Task The_activity_collection_is_asked_for_one_day_past_the_range()
    {
        // Oura omits the last day when asked for an end_date of exactly that day. The workaround
        // is applied to that collection alone, so a test has to show both halves of that.
        _api.AnswersEverythingElseEmpty();

        await Sync();

        Assert.Contains("end_date=2026-08-01", UriFor("daily_activity"));
        Assert.Contains("end_date=2026-07-31", UriFor("daily_readiness"));
        Assert.Contains("start_date=2026-07-25", UriFor("daily_activity"));
    }

    // ---- What it stores ----

    [Fact]
    public async Task The_documents_Oura_answers_with_are_stored()
    {
        _api.AnswersEverythingElseEmpty().RespondsTo("daily_sleep", FakeOuraApi.Page(string.Join(",", RecordedPayloads.Lines("daily_sleep"))));

        var report = ResultAssert.Succeeded(await Sync());

        var tally = Assert.Single(report.Collections, c => c.Collection == "daily_sleep");
        Assert.Equal(2, tally.Received);
        Assert.Equal(2, tally.Stored);
        Assert.Empty(tally.Rejected);

        await using var context = _database.CreateContext();
        Assert.Equal(2, await context.OuraDailySleep.CountAsync());
    }

    [Fact]
    public async Task A_second_sync_over_the_same_days_stores_the_same_rows_again_not_new_ones()
    {
        // Idempotency on the pull path, for the same reason as on the import path: Oura's own
        // document id is the key, so a day re-synced after Oura finalized it updates in place.
        _api.AnswersEverythingElseEmpty().RespondsTo("sleep", FakeOuraApi.Page(string.Join(",", RecordedPayloads.Lines("sleep"))));
        await Sync();

        _api.AnswersEverythingElseEmpty().RespondsTo("sleep", FakeOuraApi.Page(string.Join(",", RecordedPayloads.Lines("sleep"))));
        await Sync();

        await using var context = _database.CreateContext();
        Assert.Equal(3, await context.OuraSleepPeriods.CountAsync());
    }

    // ---- The all-day heart-rate series ----

    [Fact]
    public async Task Heart_rate_is_asked_for_in_whole_UTC_days_through_the_day_after_the_range()
    {
        // The athlete's evening is the next UTC day anywhere west of Greenwich, so the window runs
        // to the midnight two days past the last day asked for. Instants go as UTC with a Z: a +hh
        // offset would arrive as a space unless escaped.
        _api.AnswersEverythingElseEmpty();

        await Sync();

        var uri = UriFor("heartrate");
        Assert.Contains("start_datetime=2026-07-25T00:00:00Z", uri);
        Assert.Contains("end_datetime=2026-08-02T00:00:00Z", uri);
    }

    [Fact]
    public async Task A_range_longer_than_a_slice_is_asked_for_in_consecutive_slices()
    {
        _api.AnswersEverythingElseEmpty();
        var quarter = new DayRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 7, 31));

        ResultAssert.Succeeded(await SyncService().SyncAsync(_user, quarter, CancellationToken.None));

        var asked = _api.RequestedUris.Where(uri => CollectionOf(uri) == "heartrate").ToList();
        Assert.Equal(
            ["start_datetime=2026-05-01T00:00:00Z&end_datetime=2026-05-31T00:00:00Z",
             "start_datetime=2026-05-31T00:00:00Z&end_datetime=2026-06-30T00:00:00Z",
             "start_datetime=2026-06-30T00:00:00Z&end_datetime=2026-07-30T00:00:00Z",
             "start_datetime=2026-07-30T00:00:00Z&end_datetime=2026-08-02T00:00:00Z"],
            asked.Select(uri => uri.Split('?')[1]));
    }

    [Fact]
    public async Task Heart_rate_samples_are_stored_as_one_run_per_UTC_day_oldest_first()
    {
        _api.AnswersEverythingElseEmpty().RespondsTo("heartrate", FakeOuraApi.Page(string.Join(",",
            Beat("2026-07-26T23:55:00+00:00", 58, "awake"),
            Beat("2026-07-26T23:50:00+00:00", 57, "awake"),
            Beat("2026-07-27T00:05:00+00:00", 49, "sleep"))));

        var report = ResultAssert.Succeeded(await Sync());

        var tally = Assert.Single(report.Collections, c => c.Collection == "heartrate");
        Assert.Equal(3, tally.Received);
        Assert.Equal(3, tally.Stored);

        await using var context = _database.CreateContext();
        var days = await context.OuraHeartRateDays.OrderBy(day => day.Day).ToListAsync();
        Assert.Equal([new DateOnly(2026, 7, 26), new DateOnly(2026, 7, 27)], days.Select(day => day.Day));
        Assert.Equal([57, 58], days[0].Reading.Samples.Select(sample => sample.Bpm));
        Assert.Equal("sleep", Assert.Single(days[1].Reading.Samples).Source);
    }

    [Fact]
    public async Task A_re_synced_heart_rate_day_is_replaced_by_what_Oura_now_holds()
    {
        // Whole UTC days are what make this safe: the slice covers the day entirely, so the answer
        // is the day as Oura holds it now rather than part of it.
        _api.AnswersEverythingElseEmpty().RespondsTo("heartrate", FakeOuraApi.Page(
            Beat("2026-07-26T10:00:00+00:00", 70, "awake")));
        await Sync();

        _api.AnswersEverythingElseEmpty().RespondsTo("heartrate", FakeOuraApi.Page(string.Join(",",
            Beat("2026-07-26T10:00:00+00:00", 71, "awake"),
            Beat("2026-07-26T10:05:00+00:00", 72, "awake"))));
        await Sync();

        await using var context = _database.CreateContext();
        var day = Assert.Single(await context.OuraHeartRateDays.ToListAsync());
        Assert.Equal([71, 72], day.Reading.Samples.Select(sample => sample.Bpm));
    }

    [Fact]
    public async Task An_unreadable_heart_rate_sample_is_reported_and_the_rest_of_its_day_is_stored()
    {
        _api.AnswersEverythingElseEmpty().RespondsTo("heartrate", FakeOuraApi.Page(string.Join(",",
            Beat("2026-07-26T10:00:00+00:00", 70, "awake"),
            """{"timestamp":"2026-07-26T10:05:00+00:00","source":"awake"}""")));

        var report = ResultAssert.Succeeded(await Sync());

        var tally = Assert.Single(report.Collections, c => c.Collection == "heartrate");
        Assert.Equal(2, tally.Received);
        Assert.Equal(1, tally.Stored);
        Assert.Contains("bpm: missing", Assert.Single(tally.Rejected));
    }

    [Fact]
    public async Task A_heart_rate_sample_outside_the_slice_it_came_back_in_is_not_filed()
    {
        // Oura does not document whether its window ends are inclusive; a sample on the closing
        // midnight belongs to the next slice, which files it with the rest of its day.
        _api.AnswersEverythingElseEmpty().RespondsTo("heartrate", FakeOuraApi.Page(
            Beat("2026-08-02T00:00:00+00:00", 60, "sleep")));

        var report = ResultAssert.Succeeded(await Sync());

        Assert.Equal(0, Assert.Single(report.Collections, c => c.Collection == "heartrate").Stored);
    }

    /// <summary>One heart-rate row in Oura's documented shape (OpenAPI 1.43, PublicHeartRateRow).</summary>
    private static string Beat(string at, int bpm, string source) =>
        $$"""{"timestamp":"{{at}}","timestamp_unix":0,"bpm":{{bpm}},"source":"{{source}}"}""";

    // ---- Untrusted upstream ----

    [Fact]
    public async Task A_document_Oura_sends_that_cannot_be_read_is_reported_and_the_rest_are_stored()
    {
        // A malformed document from Oura must not cost the good days around it. The run succeeds,
        // and what was skipped travels back in the tally rather than into a log nobody reads.
        _api.AnswersEverythingElseEmpty().RespondsTo("daily_sleep", FakeOuraApi.Page(
            $"{RecordedPayloads.Lines("daily_sleep")[0]},{{\"id\":\"not-a-uuid\",\"day\":\"2026-07-30\"}}"));

        var report = ResultAssert.Succeeded(await Sync());

        var tally = Assert.Single(report.Collections, c => c.Collection == "daily_sleep");
        Assert.Equal(2, tally.Received);
        Assert.Equal(1, tally.Stored);
        Assert.Contains("id", Assert.Single(tally.Rejected));
        Assert.Equal(1, report.Rejected);
    }

    [Fact]
    public async Task Oura_refusing_the_token_fails_the_run_as_an_upstream_problem()
    {
        // Not a Conflict and not a 500: the transport edge turns Upstream into 502, which tells
        // the caller the failure was someone else's and may be worth retrying.
        _api
            .RespondsTo("daily_activity", "{\"detail\":\"Unauthorized\"}", HttpStatusCode.Unauthorized)
            .AnswersEverythingElseEmpty();

        var problem = ResultAssert.Failed(await Sync());

        Assert.IsType<Problem.Upstream>(problem);
    }

    [Fact]
    public async Task A_failed_collection_leaves_the_collections_before_it_stored()
    {
        // Worth stating because it is a deliberate consequence of saving per collection rather
        // than once at the end: a sync that dies halfway is not all-or-nothing, and re-running it
        // is how it is recovered — which costs nothing, because the pull is idempotent.
        _api
            .RespondsTo("daily_activity", FakeOuraApi.Page(
                string.Join(",", RecordedPayloads.Lines("daily_activity"))))
            .RespondsTo("daily_readiness", "{}", HttpStatusCode.ServiceUnavailable)
            .AnswersEverythingElseEmpty();

        Assert.IsType<Problem.Upstream>(ResultAssert.Failed(await Sync()));

        await using var context = _database.CreateContext();
        Assert.Equal(2, await context.OuraDailyActivity.CountAsync());
    }

    // ---- Refusals before any call is made ----

    [Fact]
    public async Task An_athlete_with_no_stored_Oura_credential_is_refused_without_calling_Oura()
    {
        await using (var context = _database.CreateContext())
        {
            context.ApiAccessTokens.RemoveRange(await context.ApiAccessTokens.ToListAsync());
            await context.SaveChangesAsync();
        }

        var problem = ResultAssert.Failed(await Sync());

        Assert.IsType<Problem.Conflict>(problem);
        Assert.Empty(_api.RequestedUris);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_without_calling_Oura()
    {
        var backwards = new DayRange(new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 25));

        var problem = ResultAssert.Failed(await SyncService().SyncAsync(_user, backwards, CancellationToken.None));

        Assert.IsType<Problem.Invalid>(problem);
        Assert.Empty(_api.RequestedUris);
    }

    [Fact]
    public async Task An_account_with_no_Lionheart_profile_is_refused_without_calling_Oura()
    {
        var stranger = new IdentityUser("stranger@lionheart.local") { Id = Guid.NewGuid().ToString() };

        var problem = ResultAssert.Failed(
            await SyncService().SyncAsync(stranger, Week, CancellationToken.None));

        Assert.IsType<Problem.Conflict>(problem);
        Assert.Empty(_api.RequestedUris);
    }

    // ---- Helpers ----

    private Task<Result<OuraSyncReport, Problem>> Sync() =>
        SyncService().SyncAsync(_user, Week, CancellationToken.None);

    private OuraSyncService SyncService() => new(_database.CreateContext(), _api.CreateClient());

    private static string CollectionOf(string uri) =>
        uri.Split("/usercollection/")[1].Split('?')[0];

    private string UriFor(string collection) =>
        Assert.Single(_api.RequestedUris, uri => CollectionOf(uri) == collection);
}
