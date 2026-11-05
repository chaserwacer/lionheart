using System.Text;
using System.Text.Json;
using lionheart.Endpoints.ChatEndpoints.Thread;
using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Thread;
using Microsoft.AspNetCore.Http;

namespace lionheart.Tests.Endpoints;

/// <summary>
/// An exchange told to the phone as server-sent events, and the exchange outliving the phone.
/// </summary>
public sealed class ThreadEventStreamTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly ThreadSessionView Session = new(Guid.NewGuid(), new DateTime(2026, 1, 7, 6, 0, 0, DateTimeKind.Utc), [], []);

    [Fact]
    public async Task An_exchange_is_told_in_the_order_it_happened_and_ends_with_the_stored_session()
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await ThreadEventStream.RunAsync(
            (observer, _) =>
            {
                observer.Observe(new ExchangeProgress.Wrote("Let me look."));
                observer.Observe(new ExchangeProgress.Consulting(["GetTrainingScore"]));
                observer.Observe(new ExchangeProgress.Wrote("Rest "));
                observer.Observe(new ExchangeProgress.Wrote("today."));
                return Task.FromResult(Result<ThreadSessionView, Problem>.Success(Session));
            },
            Json, context.Response, CancellationToken.None, CancellationToken.None);

        Assert.Equal(ThreadEventStream.ContentType, context.Response.ContentType);
        Assert.Equal(
            [
                ("delta", """{"text":"Let me look."}"""),
                ("tools", """{"tools":["GetTrainingScore"]}"""),
                ("delta", """{"text":"Rest "}"""),
                ("delta", """{"text":"today."}"""),
                ("done", JsonSerializer.Serialize(Session, Json)),
            ],
            Events(body));
    }

    [Fact]
    public async Task A_failed_exchange_ends_with_the_problem_the_plain_route_would_have_answered()
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await ThreadEventStream.RunAsync(
            (_, _) => Task.FromResult(Result<ThreadSessionView, Problem>.Failure(new Problem.Upstream("Anthropic is unavailable."))),
            Json, context.Response, CancellationToken.None, CancellationToken.None);

        var (name, data) = Assert.Single(Events(body));
        Assert.Equal("failed", name);
        using var problem = JsonDocument.Parse(data);
        Assert.Equal(502, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Anthropic is unavailable.", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task An_exchange_that_throws_is_told_as_a_server_failure_and_still_surfaces()
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ThreadEventStream.RunAsync(
            (observer, _) =>
            {
                observer.Observe(new ExchangeProgress.Wrote("Rest "));
                throw new ArgumentOutOfRangeException("value");
            },
            Json, context.Response, CancellationToken.None, CancellationToken.None));

        var (name, data) = Events(body)[^1];
        Assert.Equal("failed", name);
        using var problem = JsonDocument.Parse(data);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task The_exchange_runs_to_its_end_after_the_phone_has_gone()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new GoneAfterFirstWrite();
        using var connection = new CancellationTokenSource();
        var answering = new TaskCompletionSource();
        var finished = false;
        var cancelledUnderIt = true;

        var running = ThreadEventStream.RunAsync(
            async (observer, token) =>
            {
                observer.Observe(new ExchangeProgress.Wrote("Rest "));
                await answering.Task;
                observer.Observe(new ExchangeProgress.Wrote("today."));
                cancelledUnderIt = token.IsCancellationRequested;
                finished = true;
                return Result<ThreadSessionView, Problem>.Success(Session);
            },
            Json, context.Response, connection.Token, stopping: CancellationToken.None);

        await connection.CancelAsync();
        answering.SetResult();
        await running;

        Assert.True(finished);
        Assert.False(cancelledUnderIt);
    }

    private static List<(string Name, string Data)> Events(MemoryStream body) =>
        [.. Encoding.UTF8.GetString(body.ToArray())
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(frame => frame.Split('\n'))
            .Select(lines => (lines[0]["event: ".Length..], lines[1]["data: ".Length..]))];

    /// <summary>A connection that drops after the first piece of the answer reaches it.</summary>
    private sealed class GoneAfterFirstWrite : MemoryStream
    {
        private int _writes;

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ++_writes > 1 ? throw new IOException("The phone has gone.") : base.WriteAsync(buffer, cancellationToken);
    }
}
