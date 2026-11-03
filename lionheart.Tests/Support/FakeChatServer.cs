using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using OpenAI;
using OpenAI.Chat;

namespace lionheart.Tests.Support;

/// <summary>
/// A stand-in for LM Studio's OpenAI-compatible endpoint, wired in as the SDK's HTTP transport.
/// </summary>
/// <remarks>
/// Faking at the wire rather than behind an app-owned interface is deliberate: it needs no
/// production seam at all, and it keeps the real SDK request building, the real response
/// parsing, and — the part most likely to be wrong — <c>ChatCompletionResultExtensions.ToResult</c>,
/// which reads the raw body to catch LM Studio answering a failure with HTTP 200. An interface
/// fake would return a ChatCompletion that had never been through any of that.
/// </remarks>
public sealed class FakeChatServer : IDisposable
{
    private readonly Queue<CannedResponse> _responses = new();
    private readonly Handler _handler;
    private CannedResponse? _standingResponse;

    public FakeChatServer()
    {
        _handler = new Handler(this);
    }

    /// <summary>Bodies the model sent us, in order — for asserting on the tool-call round trip.</summary>
    public List<string> ReceivedRequests { get; } = [];

    public int RequestCount => ReceivedRequests.Count;

    public FakeChatServer Responds(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue(new CannedResponse(body, status));
        return this;
    }

    /// <summary>LM Studio not running at all — the request fails before any response exists.</summary>
    public FakeChatServer RespondsWithTransportFailure()
    {
        _responses.Enqueue(new CannedResponse(Body: null, HttpStatusCode.OK));
        return this;
    }

    /// <summary>Every call answers this body, however many arrive.</summary>
    public FakeChatServer AlwaysResponds(string body)
    {
        _standingResponse = new CannedResponse(body, HttpStatusCode.OK);
        return this;
    }

    /// <summary>
    /// Several clients may share the one handler, so none of them owns it — disposeHandler
    /// would let the first client disposed tear down the server for the rest.
    /// Retries are off so a test that queues one response gets exactly one request.
    /// </summary>
    public ChatClient CreateChatClient(string model = "test-model") =>
        new(model, new ApiKeyCredential("test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri("http://127.0.0.1:1234/v1"),
            Transport = new HttpClientPipelineTransport(new HttpClient(_handler, disposeHandler: false)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        });

    public void Dispose() => _handler.Dispose();

    private CannedResponse Next() => _responses.Count > 0
        ? _responses.Dequeue()
        : _standingResponse ?? throw new InvalidOperationException(
            $"The model was called {RequestCount} time(s) but no response was queued for call {RequestCount}.");

    private sealed record CannedResponse(string? Body, HttpStatusCode Status);

    private sealed class Handler(FakeChatServer server) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            server.ReceivedRequests.Add(
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

            var canned = server.Next();
            if (canned.Body is null)
                throw new HttpRequestException("Connection refused (fake LM Studio is down).");

            return new HttpResponseMessage(canned.Status)
            {
                Content = new StringContent(canned.Body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
