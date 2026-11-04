using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace lionheart.Services.Llm.Relay;

/// <summary>
/// The network under the Anthropic SDK, replaced by a folder. Each request the SDK would send is
/// written there; the answer is whatever response file appears beside it.
/// </summary>
/// <remarks>
/// Swapped in at the transport rather than behind <see cref="IChatModel"/>, so the request in the
/// folder is the exact body production would send to Anthropic (cache breakpoints, mid-conversation
/// system messages and tool schemas included) and the response is read by the same code that reads
/// Anthropic's. Nothing here can reach the network: the client is given a placeholder key and a
/// base address that does not resolve, and this handler never forwards anything.
/// <para>
/// A request waits for as long as its caller does. Whoever is playing the model takes minutes, not
/// seconds, so the SDK's own timeout is lifted and only cancellation (the athlete's request being
/// abandoned, or the host stopping) ends the wait. See docs/chat-relay.md.
/// </para>
/// </remarks>
public sealed class RelayTransport(string folder, ILogger logger) : HttpMessageHandler
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly string _folder = Path.GetFullPath(folder);
    private readonly ILogger _logger = logger;

    /// <summary>An Anthropic client whose every request is answered from <paramref name="folder"/>.</summary>
    public static global::Anthropic.AnthropicClient Client(string folder, ILogger logger) => new()
    {
        ApiKey = "relay-no-key",
        BaseUrl = "http://relay.invalid",
        MaxRetries = 0,
        Timeout = TimeSpan.FromHours(6),
        HttpClient = new HttpClient(new RelayTransport(folder, logger)) { Timeout = Timeout.InfiniteTimeSpan },
    };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonNode.Parse(body)?.AsObject() ?? [];

        var kind = RelayFiles.KindOf(parsed);

        Directory.CreateDirectory(_folder);
        var sequence = await ClaimAsync(body, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_folder, RelayFiles.ReadableName(sequence, kind)),
            RelayFiles.Readable(parsed, sequence, kind), cancellationToken);

        _logger.LogInformation(
            "Relay request {Sequence} ({Kind}) is waiting in {Folder} for {Response} or {Text}.",
            RelayFiles.Number(sequence), kind, _folder,
            RelayFiles.ResponseName(sequence), RelayFiles.TextResponseName(sequence));

        var answer = await AwaitAnswerAsync(sequence, cancellationToken);
        var completed = RelayFiles.Completed(answer, parsed, sequence);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(completed.ToJsonString(), Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>
    /// The next free number, claimed by creating its request file. Creating rather than checking
    /// first is what makes it safe for the chat and narrative clients, two instances over one
    /// folder, to claim at once.
    /// </summary>
    private async Task<int> ClaimAsync(string body, CancellationToken ct)
    {
        var next = Directory.EnumerateFiles(_folder, "*" + RelayFiles.RequestSuffix)
            .Select(path => int.TryParse(Path.GetFileName(path).Split('.')[0], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        while (true)
        {
            var path = Path.Combine(_folder, RelayFiles.RequestName(next));
            try
            {
                await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                await file.WriteAsync(Encoding.UTF8.GetBytes(body), ct);
                return next;
            }
            catch (IOException) when (File.Exists(path))
            {
                next++;
            }
        }
    }

    /// <summary>
    /// The answer, once its file appears and has stopped changing. A JSON answer caught half-written
    /// fails to parse and is read again; a text answer is taken only when two polls in a row read
    /// the same words, since a half-written text file would parse as a shorter answer.
    /// </summary>
    private async Task<JsonObject> AwaitAnswerAsync(int sequence, CancellationToken ct)
    {
        var wire = Path.Combine(_folder, RelayFiles.ResponseName(sequence));
        var text = Path.Combine(_folder, RelayFiles.TextResponseName(sequence));
        string? lastText = null;
        var reportedMalformed = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (File.Exists(wire))
            {
                try
                {
                    if (JsonNode.Parse(await File.ReadAllTextAsync(wire, ct)) is JsonObject answer) return answer;
                }
                catch (JsonException exception) when (!reportedMalformed)
                {
                    // Usually still being written; said once in case it is simply wrong.
                    reportedMalformed = true;
                    _logger.LogWarning("Relay answer {File} does not parse yet: {Reason}", wire, exception.Message);
                }
                catch (JsonException)
                {
                }
            }
            else if (File.Exists(text))
            {
                var words = await File.ReadAllTextAsync(text, ct);
                if (words.Length > 0 && words == lastText) return RelayFiles.FromText(words);
                lastText = words;
            }

            await Task.Delay(PollInterval, ct);
        }
    }
}
