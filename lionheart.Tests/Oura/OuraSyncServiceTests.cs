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
            ["daily_activity", "daily_readiness", "daily_resilience", "daily_sleep", "daily_spo2",
             "daily_stress", "session", "sleep", "tag", "workout"],
            _api.RequestedUris.Select(CollectionOf).Order());
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
