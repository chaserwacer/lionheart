using System.Net;
using System.Text;
using lionheart.Services.Llm;
using lionheart.Services.Llm.Anthropic;

namespace lionheart.Tests.Support;

/// <summary>
/// A stand-in for Anthropic's Messages API, wired in as the SDK's own HttpClient.
/// </summary>
/// <remarks>
/// The same bargain <see cref="FakeChatServer"/> strikes for LM Studio, for the same reason:
/// faking at the wire keeps the real SDK building the request and parsing the response, including
/// the parts most likely to be wrong — the content-block union, the stop reason, and the thinking
/// signature that has to survive a round trip. A fake <see cref="IChatModel"/> would prove only
/// that the test's own object was returned.
/// </remarks>
public sealed class FakeAnthropicServer : IDisposable
{
    private readonly Queue<CannedResponse> _responses = new();
    private readonly Handler _handler;
    private CannedResponse? _standingResponse;

    public FakeAnthropicServer() => _handler = new Handler(this);

    /// <summary>Bodies the SDK sent us, in order — for asserting on the tool-call round trip.</summary>
    public List<string> ReceivedRequests { get; } = [];

    public int RequestCount => ReceivedRequests.Count;

    public FakeAnthropicServer Responds(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue(new CannedResponse(body, status));
        return this;
    }

    /// <summary>The network is down — the request fails before any response exists.</summary>
    public FakeAnthropicServer RespondsWithTransportFailure()
    {
        _responses.Enqueue(new CannedResponse(Body: null, HttpStatusCode.OK));
        return this;
    }

    /// <summary>Every call answers this body, however many arrive.</summary>
    public FakeAnthropicServer AlwaysResponds(string body)
    {
        _standingResponse = new CannedResponse(body, HttpStatusCode.OK);
        return this;
    }

    /// <summary>
    /// Retries are off so a test that queues one response gets exactly one request, and the
    /// handler is not owned by the client so several clients may share one server.
    /// </summary>
    public global::Anthropic.AnthropicClient CreateClient() => new()
    {
        ApiKey = "test-key",
        MaxRetries = 0,
        HttpClient = new HttpClient(_handler, disposeHandler: false),
    };

    public IChatModel CreateChatModel(string model = "claude-opus-5-5") =>
        new AnthropicChatModel(CreateClient(), model, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

    public void Dispose() => _handler.Dispose();

    private CannedResponse Next() => _responses.Count > 0
        ? _responses.Dequeue()
        : _standingResponse ?? throw new InvalidOperationException(
            $"The model was called {RequestCount} time(s) but no response was queued for call {RequestCount}.");

    private sealed record CannedResponse(string? Body, HttpStatusCode Status);

    private sealed class Handler(FakeAnthropicServer server) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            server.ReceivedRequests.Add(
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

            var canned = server.Next();
            if (canned.Body is null)
                throw new HttpRequestException("Connection refused (fake Anthropic is unreachable).");

            return new HttpResponseMessage(canned.Status)
            {
                Content = new StringContent(canned.Body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
