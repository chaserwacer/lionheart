using System.Net;
using System.Text;

namespace lionheart.Tests.Oura;

/// <summary>
/// A stand-in for Oura's API, wired in as the client's own <see cref="HttpMessageHandler"/>.
/// </summary>
/// <remarks>
/// Faked at the wire for the reason given on <see cref="Support.FakeChatServer"/>: it needs no
/// production seam, and it keeps the real request building, the real paging and the real
/// deserialization in the path. Paging is the part most worth exercising, because a cursor bound
/// by the wrong name fails silently — it reads the first page and reports success.
/// </remarks>
public sealed class FakeOuraApi : IDisposable
{
    private readonly Queue<(HttpStatusCode Status, string? Body)> _responses = new();
    private readonly Dictionary<string, (HttpStatusCode Status, string? Body)> _byCollection = [];
    private (HttpStatusCode Status, string? Body)? _fallback;

    /// <summary>Every URL the client asked for, in order — so a test can assert what it paged.</summary>
    public List<string> RequestedUris { get; } = [];

    /// <summary>The bearer token on each request, to prove the credential is actually sent.</summary>
    public List<string?> SentTokens { get; } = [];

    public FakeOuraApi Responds(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue((status, body));
        return this;
    }

    /// <summary>
    /// Answers one named collection, whenever it is asked for.
    /// </summary>
    /// <remarks>
    /// Keyed by collection rather than queued, because a sync asks for ten of them and a test that
    /// cares about one should not have to know where in the order it falls. Pair it with
    /// <see cref="AnswersEverythingElseEmpty"/>.
    /// </remarks>
    public FakeOuraApi RespondsTo(string collection, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _byCollection[collection] = (status, body);
        return this;
    }

    /// <summary>An empty page for any collection no <see cref="RespondsTo"/> named.</summary>
    public FakeOuraApi AnswersEverythingElseEmpty()
    {
        _fallback = (HttpStatusCode.OK, Page(string.Empty));
        return this;
    }

    /// <summary>Oura unreachable: the request fails before any response exists.</summary>
    public FakeOuraApi RespondsWithTransportFailure()
    {
        _responses.Enqueue((HttpStatusCode.OK, null));
        return this;
    }

    /// <summary>A page of documents, with a cursor to the next one when <paramref name="nextToken"/> is given.</summary>
    public static string Page(string documents, string? nextToken = null) =>
        $"{{\"data\":[{documents}],\"next_token\":{(nextToken is null ? "null" : $"\"{nextToken}\"")}}}";

    public HttpClient CreateClient() => new(new Handler(this), disposeHandler: false);

    public void Dispose() { }

    /// <summary>
    /// What to answer this request with: the collection's own canned response, then the queue,
    /// then the fallback. Running out is a defective test rather than a failure to simulate, so it
    /// throws and names the request.
    /// </summary>
    private (HttpStatusCode Status, string? Body) Answer(Uri uri)
    {
        var collection = uri.AbsolutePath.Split("/usercollection/")[^1];

        if (_byCollection.TryGetValue(collection, out var canned)) return canned;
        if (_responses.Count > 0) return _responses.Dequeue();
        if (_fallback is { } fallback) return fallback;

        throw new InvalidOperationException(
            $"The fake Oura API was asked for {uri} with no response queued for '{collection}'.");
    }

    private sealed class Handler(FakeOuraApi api) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            api.RequestedUris.Add(request.RequestUri!.ToString());
            api.SentTokens.Add(request.Headers.Authorization?.Parameter);

            var (status, body) = api.Answer(request.RequestUri!);

            if (body is null)
            {
                throw new HttpRequestException("No route to the Oura API.");
            }

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
