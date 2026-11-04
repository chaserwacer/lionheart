using System.Net;
using lionheart.Functional;
using lionheart.Model.Oura.Wire;
using lionheart.Services.Oura;
using lionheart.Tests.Support;

namespace lionheart.Tests.Oura;

/// <summary>
/// What the client does with Oura's answers: follows its paging, and states every way the call can
/// fail as one problem the transport edge turns into 502.
/// </summary>
public class OuraClientTests
{
    private static readonly DateOnly From = new(2026, 7, 1);
    private static readonly DateOnly To = new(2026, 7, 31);

    [Fact]
    public async Task A_single_page_is_read_whole()
    {
        using var api = new FakeOuraApi().Responds(FakeOuraApi.Page(TwoSleepDays));

        var documents = ResultAssert.Succeeded(await ReadAsync(api));

        Assert.Equal(2, documents.Count);
        Assert.Equal("2024-01-26", documents[0].Day);
        Assert.Single(api.RequestedUris);
    }

    [Fact]
    public async Task Paging_follows_Ouras_next_token_until_it_runs_out()
    {
        // A cursor bound by the wrong name is the failure with no symptom: paging stops after the
        // first page and the sync reports success having stored less than Oura holds.
        using var api = new FakeOuraApi()
            .Responds(FakeOuraApi.Page(TwoSleepDays, nextToken: "page-2"))
            .Responds(FakeOuraApi.Page(TwoSleepDays, nextToken: "page-3"))
            .Responds(FakeOuraApi.Page(TwoSleepDays));

        var documents = ResultAssert.Succeeded(await ReadAsync(api));

        Assert.Equal(6, documents.Count);
        Assert.Equal(3, api.RequestedUris.Count);
        Assert.DoesNotContain("next_token", api.RequestedUris[0]);
        Assert.Contains("next_token=page-2", api.RequestedUris[1]);
        Assert.Contains("next_token=page-3", api.RequestedUris[2]);
    }

    [Fact]
    public async Task An_empty_cursor_ends_the_paging_rather_than_asking_for_another_page()
    {
        using var api = new FakeOuraApi().Responds(FakeOuraApi.Page(TwoSleepDays, nextToken: ""));

        var documents = ResultAssert.Succeeded(await ReadAsync(api));

        Assert.Equal(2, documents.Count);
        Assert.Single(api.RequestedUris);
    }

    [Fact]
    public async Task The_request_carries_the_collection_the_dates_and_the_bearer_token()
    {
        using var api = new FakeOuraApi().Responds(FakeOuraApi.Page(TwoSleepDays));

        await ReadAsync(api);

        var uri = Assert.Single(api.RequestedUris);
        Assert.Contains("/v2/usercollection/daily_sleep", uri);
        Assert.Contains("start_date=2026-07-01", uri);
        Assert.Contains("end_date=2026-07-31", uri);
        Assert.Equal("token-abc", Assert.Single(api.SentTokens));
    }

    [Fact]
    public async Task A_page_with_no_documents_is_an_empty_answer_not_a_failure()
    {
        using var api = new FakeOuraApi().Responds(FakeOuraApi.Page(string.Empty));

        Assert.Empty(ResultAssert.Succeeded(await ReadAsync(api)));
    }

    // ---- Failures ----

    [Fact]
    public async Task A_rejected_token_is_an_upstream_problem_that_names_the_token_as_the_cause()
    {
        // 401 is the one failure the athlete can act on, and the action changed: Oura retired
        // personal access tokens at the end of 2025, so the message says so rather than leaving
        // a stale credential looking like an outage.
        using var api = new FakeOuraApi().Responds("{\"detail\":\"Unauthorized\"}", HttpStatusCode.Unauthorized);

        var problem = ResultAssert.Failed(await ReadAsync(api));

        var upstream = Assert.IsType<Problem.Upstream>(problem);
        Assert.Contains("access token", upstream.Detail);
        Assert.Contains("daily_sleep", upstream.Detail);
    }

    [Fact]
    public async Task Rate_limiting_is_an_upstream_problem_that_says_to_retry()
    {
        using var api = new FakeOuraApi().Responds("{}", HttpStatusCode.TooManyRequests);

        var upstream = Assert.IsType<Problem.Upstream>(ResultAssert.Failed(await ReadAsync(api)));
        Assert.Contains("rate-limiting", upstream.Detail);
    }

    [Fact]
    public async Task A_server_error_is_an_upstream_problem_carrying_its_status()
    {
        using var api = new FakeOuraApi().Responds("{}", HttpStatusCode.ServiceUnavailable);

        var upstream = Assert.IsType<Problem.Upstream>(ResultAssert.Failed(await ReadAsync(api)));
        Assert.Contains("503", upstream.Detail);
    }

    [Fact]
    public async Task Oura_being_unreachable_is_an_upstream_problem_rather_than_an_exception()
    {
        using var api = new FakeOuraApi().RespondsWithTransportFailure();

        var upstream = Assert.IsType<Problem.Upstream>(ResultAssert.Failed(await ReadAsync(api)));
        Assert.Contains("could not be reached", upstream.Detail);
    }

    [Fact]
    public async Task A_body_that_is_not_the_expected_shape_is_an_upstream_problem()
    {
        using var api = new FakeOuraApi().Responds("<html>upstream proxy error</html>");

        var upstream = Assert.IsType<Problem.Upstream>(ResultAssert.Failed(await ReadAsync(api)));
        Assert.Contains("could not read", upstream.Detail);
    }

    [Fact]
    public async Task A_failure_on_a_later_page_fails_the_whole_read()
    {
        // A partial answer must not look like a complete one: storing page one and reporting
        // success would leave a gap nothing afterwards knows to go back for.
        using var api = new FakeOuraApi()
            .Responds(FakeOuraApi.Page(TwoSleepDays, nextToken: "page-2"))
            .Responds("{}", HttpStatusCode.InternalServerError);

        Assert.IsType<Problem.Upstream>(ResultAssert.Failed(await ReadAsync(api)));
        Assert.Equal(2, api.RequestedUris.Count);
    }

    private static Task<Result<IReadOnlyList<WireDailySleep>, Problem>> ReadAsync(FakeOuraApi api) =>
        new OuraClient(api.CreateClient())
            .ReadAsync<WireDailySleep>("daily_sleep", From, To, "token-abc", CancellationToken.None);

    private const string TwoSleepDays = """
        {"id":"6748c745-2688-4430-948f-7edbe0e27531","day":"2024-01-26","score":62,
         "contributors":{"deep_sleep":91,"efficiency":67,"latency":76,"rem_sleep":33,
                         "restfulness":64,"timing":24,"total_sleep":61}},
        {"id":"c178e022-f769-4df2-b5cf-8eece3a32c2d","day":"2024-01-27","score":60,
         "contributors":{"deep_sleep":88,"efficiency":53,"latency":81,"rem_sleep":29,
                         "restfulness":61,"timing":27,"total_sleep":59}}
        """;
}
