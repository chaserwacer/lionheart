using System.ClientModel;
using System.Diagnostics;
using OpenAI;
using OpenAI.Chat;

namespace lionheart.Tests.LocalModel;

/// <summary>
/// Marker for the LocalModel collection, so the readiness probe and any server start happen
/// once per run rather than once per test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LocalModelCollection : ICollectionFixture<LocalModelFixture>
{
    public const string Name = "LocalModel";
}

/// <summary>
/// Brings up (or gives up on) a real LM Studio server for the opt-in integration tests.
/// </summary>
/// <remarks>
/// The rule is that a machine without a local model is never a failing machine: every way this
/// can not-work resolves to <see cref="Unavailable"/> carrying the key or step that failed, and
/// the tests turn that into a skip. Only a server that answers produces a client.
/// </remarks>
public sealed class LocalModelFixture : IDisposable
{
    private static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _probe = new() { Timeout = ProbeTimeout };
    private readonly LmStudioSettings? _settings;

    public LocalModelFixture()
    {
        var (settings, missingKey) = LmStudioSettings.Read();
        if (settings is null)
        {
            Unavailable = $"{missingKey} is not set (add it to .env — see lionheart.Tests/README.md).";
            return;
        }

        _settings = settings;
        Unavailable = EnsureServerAnswers(settings);
    }

    /// <summary>Null when the local model is usable; otherwise why it is not, naming the key or step.</summary>
    public string? Unavailable { get; }

    public ChatClient CreateChatClient()
    {
        if (_settings is null || Unavailable is not null)
            throw new InvalidOperationException($"Local model unavailable: {Unavailable}");

        return new ChatClient(
            _settings.Model,
            new ApiKeyCredential(_settings.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_settings.BaseUrl) });
    }

    public void Dispose() => _probe.Dispose();

    private string? EnsureServerAnswers(LmStudioSettings settings)
    {
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
            return $"{LmStudioSettings.BaseUrlKey} is not an absolute URI: '{settings.BaseUrl}'.";

        // A running server that rejects the key is a different problem from no server, and
        // restarting it would not help — report it instead of entering the start-and-wait path.
        var initial = Probe(baseUri, settings.ApiKey);
        if (initial is ProbeResult.Reachable) return null;
        if (initial is ProbeResult.Unauthorized) return KeyRejected(baseUri);

        var startFailure = TryStartServer(settings);
        if (startFailure is not null) return startFailure;

        var deadline = DateTime.UtcNow + StartupBudget;
        while (DateTime.UtcNow < deadline)
        {
            var probe = Probe(baseUri, settings.ApiKey);
            if (probe is ProbeResult.Reachable) return null;
            if (probe is ProbeResult.Unauthorized) return KeyRejected(baseUri);

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        return $"LM Studio did not answer {baseUri} within {StartupBudget.TotalSeconds:N0}s of 'lms server start'.";
    }

    private static string KeyRejected(Uri baseUri) =>
        $"LM Studio at {baseUri} rejected {LmStudioSettings.ApiKeyKey} (HTTP 401). "
        + "Copy the token from LM Studio's Developer tab into .env.";

    private enum ProbeResult { Reachable, Unauthorized, Unreachable }

    /// <summary>
    /// The /v1 root's models listing is the cheapest proof the server is up. It is an
    /// authenticated route: without the key LM Studio answers 401, which looks identical to a
    /// dead server unless the key is sent — and would send the fixture off restarting a server
    /// that was running the whole time.
    /// </summary>
    private ProbeResult Probe(Uri baseUri, string apiKey)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "models"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            using var response = _probe.Send(request);
            if (response.IsSuccessStatusCode) return ProbeResult.Reachable;

            return response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? ProbeResult.Unauthorized
                : ProbeResult.Unreachable;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return ProbeResult.Unreachable;
        }
    }

    private static string? TryStartServer(LmStudioSettings settings)
    {
        var start = Run("lms", "server start");
        if (start is not null) return start;

        // Loading is best effort: the model may already be resident, and `lms load` returns
        // non-zero in that case on some versions. A failure here is only fatal if the health
        // probe also never succeeds, which the caller's wait loop decides.
        Run("lms", $"load {settings.Model} --yes");
        return null;
    }

    private static string? Run(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null) return $"could not start '{fileName} {arguments}'.";

            if (!process.WaitForExit(StartupBudget))
            {
                process.Kill(entireProcessTree: true);
                return $"'{fileName} {arguments}' did not finish within {StartupBudget.TotalSeconds:N0}s.";
            }

            return null;
        }
        catch (Exception e)
        {
            return $"'{fileName}' CLI is not available ({e.GetType().Name}); start LM Studio manually.";
        }
    }
}
