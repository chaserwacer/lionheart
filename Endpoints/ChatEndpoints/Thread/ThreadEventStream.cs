using System.Text.Json;
using System.Threading.Channels;
using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Thread;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints.Thread;

/// <summary>
/// One exchange told to the phone as it happens, as server-sent events: the answer's text as it is
/// written, each round of tools as it is run, and then the stored session or why there is none.
/// </summary>
/// <remarks>
/// The exchange and the connection are kept apart on purpose. The exchange runs to the end on the
/// host's lifetime rather than the request's, so an athlete who loses signal between the gym's
/// doors still has his answer stored, and the phone reads it back from the thread when it returns.
/// The connection only ever reads what the exchange has said so far, from a queue: a slow or
/// vanished phone can delay nothing but its own copy.
/// <list type="table">
/// <item><term><c>delta</c></term><description><c>{ text }</c>: the next piece of the answer.</description></item>
/// <item><term><c>tools</c></term><description><c>{ tools }</c>: tools being run; text sent so far is withdrawn.</description></item>
/// <item><term><c>done</c></term><description>the <c>ThreadSessionView</c> the exchange was stored in.</description></item>
/// <item><term><c>failed</c></term><description>the problem document the plain route would have answered with; nothing was stored.</description></item>
/// </list>
/// A comment line goes every <see cref="HeartbeatInterval"/> while nothing else does, so neither
/// the phone's idle timeout nor the gateway in front of the server closes a connection while the
/// model is thinking or a tool is running.
/// </remarks>
public static class ThreadEventStream
{
    public const string ContentType = "text/event-stream";

    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    public const string Heartbeat = ": still working\n\n";

    /// <summary>Runs <paramref name="exchange"/> to its end, telling <paramref name="response"/> what it can.</summary>
    /// <param name="connection">Ends only the telling: the request's own token.</param>
    /// <param name="stopping">Ends the exchange itself: the host shutting down.</param>
    public static async Task RunAsync(
        Func<IExchangeObserver, CancellationToken, Task<Result<ThreadSessionView, Problem>>> exchange,
        JsonSerializerOptions json,
        HttpResponse response,
        CancellationToken connection,
        CancellationToken stopping)
    {
        var frames = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        var exchanged = ExchangeAsync(exchange, json, frames.Writer, stopping);
        await TellAsync(frames.Reader, response, connection);
        await exchanged;
    }

    /// <summary>One event in the wire's framing. Its data is one line, since the JSON is not indented.</summary>
    public static string Frame(string name, object data, JsonSerializerOptions json) =>
        $"event: {name}\ndata: {JsonSerializer.Serialize(data, json)}\n\n";

    public static string Progress(ExchangeProgress progress, JsonSerializerOptions json) => progress switch
    {
        ExchangeProgress.Wrote wrote => Frame("delta", new Delta(wrote.Text), json),
        ExchangeProgress.Consulting consulting => Frame("tools", new Consulted(consulting.Tools), json),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    public static string Outcome(Result<ThreadSessionView, Problem> result, JsonSerializerOptions json) =>
        result.Match(
            session => Frame("done", session, json),
            problem => Frame("failed", ResultToHttp.Described(problem), json));

    private static async Task ExchangeAsync(
        Func<IExchangeObserver, CancellationToken, Task<Result<ThreadSessionView, Problem>>> exchange,
        JsonSerializerOptions json,
        ChannelWriter<string> frames,
        CancellationToken stopping)
    {
        try
        {
            frames.TryWrite(Outcome(await exchange(new Queued(frames, json), stopping), json));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // What the plain route answers when an exchange throws: a 500, nothing stored. Said
            // here because the response has begun and the exception handler can no longer say it;
            // without it the phone would read a dropped connection and wait for an answer that is
            // never coming. Rethrown after, so the host still logs it.
            frames.TryWrite(Outcome(
                Result<ThreadSessionView, Problem>.Failure(new Problem.Unexpected("The exchange failed on the server.")),
                json));
            throw;
        }
        finally
        {
            frames.Complete();
        }
    }

    /// <summary>
    /// Copies what the exchange has said to the response until it says no more or the phone goes.
    /// A phone that goes is not a failure here: the exchange carries on without it.
    /// </summary>
    private static async Task TellAsync(ChannelReader<string> frames, HttpResponse response, CancellationToken connection)
    {
        response.ContentType = ContentType;
        response.Headers.CacheControl = "no-cache";
        try
        {
            await response.StartAsync(connection);
            while (await NextAsync(frames, response, connection))
            {
                while (frames.TryRead(out var frame)) await response.WriteAsync(frame, connection);
                await response.Body.FlushAsync(connection);
            }
        }
        catch (OperationCanceledException) when (connection.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Waits for the next frame, sending a heartbeat each time the wait runs long.</summary>
    private static async Task<bool> NextAsync(ChannelReader<string> frames, HttpResponse response, CancellationToken connection)
    {
        while (true)
        {
            using var beat = CancellationTokenSource.CreateLinkedTokenSource(connection);
            beat.CancelAfter(HeartbeatInterval);
            try
            {
                return await frames.WaitToReadAsync(beat.Token);
            }
            catch (OperationCanceledException) when (!connection.IsCancellationRequested)
            {
                await response.WriteAsync(Heartbeat, connection);
                await response.Body.FlushAsync(connection);
            }
        }
    }

    private sealed class Queued(ChannelWriter<string> frames, JsonSerializerOptions json) : IExchangeObserver
    {
        public void Observe(ExchangeProgress progress) => frames.TryWrite(Progress(progress, json));
    }

    private sealed record Delta(string Text);

    private sealed record Consulted(IReadOnlyList<string> Tools);
}
