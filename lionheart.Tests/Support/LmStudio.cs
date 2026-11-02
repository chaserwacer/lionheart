using System.ClientModel;
using System.Diagnostics;
using lionheart.Services.Llm;
using lionheart.Services.Llm.LmStudio;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace lionheart.Tests.Support;

/// <summary>
/// Brings up (or gives up on) a real LM Studio server, once per test run, and hands out clients
/// pointed at it.
/// </summary>
/// <remarks>
/// The rule is that a machine without a local model is never a failing machine: every way this
/// can not-work resolves to <see cref="Unavailable"/> carrying the key or step that failed, and
/// <see cref="LmStudioFactAttribute"/> turns that into a skip. The probe is lazy so a plain
/// <c>dotnet test</c> — which filters these tests out before running any of them — never touches
/// the network or starts a server.
/// </remarks>
public static class LmStudio
{
    private static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private static readonly Lazy<(LmStudioSettings? Settings, string? Unavailable)> Probe = new(Resolve);

    /// <summary>Null when the local model is usable; otherwise why it is not, naming the key or step.</summary>
    public static string? Unavailable => Probe.Value.Unavailable;

    public static ChatClient CreateChatClient() =>
        new(Settings().Model, Credential(), ClientOptions());

    public static ChatClient CreateChatClient(string model) =>
        new(model, Credential(), ClientOptions());

    /// <summary>The LM Studio provider against the live server, as every consumer sees it.</summary>
    public static IChatModel CreateChatModel() => new LmStudioChatModel(CreateChatClient());

    /// <summary>
    /// Built through the application's own factory from the <c>Embedding__*</c> configuration, so
    /// the live tests reach the embedding server exactly the way the running app does.
    /// </summary>
    public static EmbeddingClient CreateEmbeddingClient() =>
        ChatModelFactory.CreateEmbeddingClient(Settings().Embedding);

    private static LmStudioSettings Settings() =>
        Probe.Value.Settings ?? throw new InvalidOperationException($"Local model unavailable: {Unavailable}");

    private static ApiKeyCredential Credential() => new(Settings().ApiKey);

    private static OpenAIClientOptions ClientOptions() => new() { Endpoint = new Uri(Settings().BaseUrl) };

    private static (LmStudioSettings?, string?) Resolve()
    {
        var (settings, missingKey) = LmStudioSettings.Read();
        if (settings is null)
            return (null, $"{missingKey} is not set (add it to .env — see lionheart.Tests/README.md).");

        var unavailable = EnsureServerAnswers(settings);
        return (unavailable is null ? settings : null, unavailable);
    }

    private static string? EnsureServerAnswers(LmStudioSettings settings)
    {
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
            return $"{LmStudioSettings.BaseUrlKey} is not an absolute URI: '{settings.BaseUrl}'.";

        using var probe = new HttpClient { Timeout = ProbeTimeout };

        // A running server that rejects the key is a different problem from no server, and
        // restarting it would not help — report it instead of entering the start-and-wait path.
        var initial = Ping(probe, baseUri, settings.ApiKey);
        if (initial is Reachability.Reachable) return null;
        if (initial is Reachability.Unauthorized) return KeyRejected(baseUri);

        var startFailure = TryStartServer(settings);
        if (startFailure is not null) return startFailure;

        var deadline = DateTime.UtcNow + StartupBudget;
        while (DateTime.UtcNow < deadline)
        {
            var reachability = Ping(probe, baseUri, settings.ApiKey);
            if (reachability is Reachability.Reachable) return null;
            if (reachability is Reachability.Unauthorized) return KeyRejected(baseUri);

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        return $"LM Studio did not answer {baseUri} within {StartupBudget.TotalSeconds:N0}s of 'lms server start'.";
    }

    private static string KeyRejected(Uri baseUri) =>
        $"LM Studio at {baseUri} rejected {LmStudioSettings.ApiKeyKey} (HTTP 401). "
        + "Copy the token from LM Studio's Developer tab into .env.";

    private enum Reachability { Reachable, Unauthorized, Unreachable }

    /// <summary>
    /// The /v1 root's models listing is the cheapest proof the server is up. It is an
    /// authenticated route: without the key LM Studio answers 401, which looks identical to a
    /// dead server unless the key is sent — and would send this off restarting a server that
    /// was running the whole time.
    /// </summary>
    private static Reachability Ping(HttpClient probe, Uri baseUri, string apiKey)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "models"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            using var response = probe.Send(request);
            if (response.IsSuccessStatusCode) return Reachability.Reachable;

            return response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? Reachability.Unauthorized
                : Reachability.Unreachable;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return Reachability.Unreachable;
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
