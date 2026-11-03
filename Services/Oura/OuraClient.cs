using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using lionheart.Functional;
using lionheart.Model.Oura.Wire;

namespace lionheart.Services.Oura;

/// <summary>
/// Reads one Oura collection over a date range, following Oura's paging to the end.
/// </summary>
/// <remarks>
/// The only part of the integration that talks to the network, and the only one that has to turn
/// an HTTP outcome into a <see cref="Problem"/>. Nothing here interprets a payload — that is
/// <see cref="OuraDocumentMapping"/>'s job — so this type's whole contract is "the documents Oura
/// holds for these dates, or why it could not say".
/// </remarks>
internal sealed class OuraClient
{
    private const string CollectionRoot = "https://api.ouraring.com/v2/usercollection";

    /// <summary>
    /// Oura's payloads are snake_case and every member carries an explicit name, so no naming
    /// policy applies. Stated rather than left to the ambient default so the options the wire
    /// records are read with are visible at the one place that reads them.
    /// </summary>
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerOptions.Default);

    private readonly HttpClient _httpClient;

    public OuraClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// Every document in <paramref name="collection"/> whose day falls in the range, across as
    /// many pages as Oura answers with.
    /// </summary>
    public Task<Result<IReadOnlyList<T>, Problem>> ReadAsync<T>(
        string collection, DateOnly from, DateOnly to, string token, CancellationToken ct) =>
        ReadAsync<T>(collection, $"start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}", token, ct);

    /// <summary>
    /// Every sample in a time-series collection between two instants, across as many pages as Oura
    /// answers with.
    /// </summary>
    /// <remarks>
    /// Oura's time series take instants rather than days. They are written in UTC with a <c>Z</c>,
    /// because a <c>+hh:mm</c> offset would have to be escaped and a UTC instant needs no offset at
    /// all.
    /// </remarks>
    public Task<Result<IReadOnlyList<T>, Problem>> ReadAsync<T>(
        string collection, DateTimeOffset from, DateTimeOffset to, string token, CancellationToken ct) =>
        ReadAsync<T>(
            collection,
            $"start_datetime={from.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}&end_datetime={to.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}",
            token,
            ct);

    private Task<Result<IReadOnlyList<T>, Problem>> ReadAsync<T>(
        string collection, string range, string token, CancellationToken ct) =>
        Result.UnfoldAsync(
            new Pull<T>(collection, range, token, Cursor: null, Gathered: []),
            (pull, cancel) => NextPageAsync(pull, cancel),
            ct);

    /// <summary>
    /// A walk through one collection's pages: where it is, and what it has collected so far.
    /// </summary>
    /// <param name="Range">The query naming what to read — days or instants, already formatted.</param>
    private sealed record Pull<T>(
        string Collection,
        string Range,
        string Token,
        string? Cursor,
        IReadOnlyList<T> Gathered)
    {
        public Loop<Pull<T>, IReadOnlyList<T>> After(OuraPage<T> page)
        {
            IReadOnlyList<T> gathered = [.. Gathered, .. page.Data ?? []];

            return page.NextToken is { Length: > 0 } cursor
                ? new Loop<Pull<T>, IReadOnlyList<T>>.Continue(this with { Cursor = cursor, Gathered = gathered })
                : new Loop<Pull<T>, IReadOnlyList<T>>.Done(gathered);
        }

        public Uri PageUri()
        {
            var query =
                $"?{Range}" +
                (Cursor is null ? string.Empty : $"&next_token={Uri.EscapeDataString(Cursor)}");

            return new Uri($"{CollectionRoot}/{Collection}{query}");
        }
    }

    private Task<Result<Loop<Pull<T>, IReadOnlyList<T>>, Problem>> NextPageAsync<T>(
        Pull<T> pull, CancellationToken ct) =>
        FetchAsync<T>(pull, ct).MapAsync(pull.After);

    private async Task<Result<OuraPage<T>, Problem>> FetchAsync<T>(Pull<T> pull, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, pull.PageUri());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pull.Token);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException exception)
        {
            return Refused<T>(pull, $"could not be reached ({exception.Message})");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return Refused<T>(pull, Explain(response.StatusCode));
            }

            try
            {
                var page = JsonSerializer.Deserialize<OuraPage<T>>(body, WireJson);

                return page is null
                    ? Refused<T>(pull, "answered with a null body")
                    : Result<OuraPage<T>, Problem>.Success(page);
            }
            catch (JsonException exception)
            {
                return Refused<T>(pull, $"answered with a body this client could not read ({exception.Message})");
            }
        }
    }

    /// <remarks>
    /// The status codes worth distinguishing in the message, because each one needs a different
    /// thing done about it. Everything else travels as its code.
    /// </remarks>
    private static string Explain(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "rejected the access token. Oura retired personal access tokens at the end of 2025, so a "
            + "token that used to work may now need replacing with an OAuth2 credential.",
        HttpStatusCode.TooManyRequests => "is rate-limiting this client; retry later.",
        _ => $"answered {(int)status} {status}."
    };

    private static Result<OuraPage<T>, Problem> Refused<T>(Pull<T> pull, string detail) =>
        Result<OuraPage<T>, Problem>.Failure(
            new Problem.Upstream($"The Oura API {detail} (collection '{pull.Collection}')."));
}
