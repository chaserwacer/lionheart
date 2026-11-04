using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using lionheart.Services.Llm;
using lionheart.Services.Llm.Anthropic;
using lionheart.Services.Llm.Relay;

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
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            server.ReceivedRequests.Add(body);

            var canned = server.Next();
            if (canned.Body is null)
                throw new HttpRequestException("Connection refused (fake Anthropic is unreachable).");

            // A request that asks to stream gets the canned message told as the API's event stream,
            // so one canned body serves both routes. A refusal of the request itself is answered
            // before any stream begins, as the API answers it.
            var streams = JsonNode.Parse(body)?["stream"]?.GetValue<bool>() is true;
            return new HttpResponseMessage(canned.Status)
            {
                Content = streams && canned.Status == HttpStatusCode.OK
                    ? new StringContent(
                        string.Concat(RelayStream.Events(JsonNode.Parse(canned.Body)!.AsObject()).Select(e => e.Frame())),
                        Encoding.UTF8, RelayStream.ContentType)
                    : new StringContent(canned.Body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
