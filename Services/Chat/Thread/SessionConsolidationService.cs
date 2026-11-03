namespace lionheart.Services.Chat.Thread;

/// <summary>
/// Distils sessions that have ended while nobody was writing to the thread, so a new session opens
/// without waiting for its predecessor's consolidation, and the memory screen shows what the last
/// session left soon after it ends rather than only once the next one begins.
/// </summary>
/// <remarks>
/// The exchange that opens a new session still consolidates whatever this has not reached; this
/// only moves the work off that message's wait. Each pass takes the thread's gate, so it never runs
/// beside an exchange — which also keeps it from competing with one for a local model server that
/// answers one request at a time.
/// </remarks>
public sealed class SessionConsolidationService(
    IServiceScopeFactory scopes,
    ChatThreadGate gate,
    ILogger<SessionConsolidationService> logger) : BackgroundService
{
    /// <summary>
    /// Often enough that a session is distilled within minutes of its idle gap passing; rare enough
    /// that an empty pass, one indexed query, is nothing.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PassAsync(stoppingToken);
        }
    }

    /// <summary>
    /// One pass. The catch-all is the boundary that keeps a fault in one pass from ending every pass
    /// after it; it records the fault and lets cancellation through.
    /// </summary>
    private async Task PassAsync(CancellationToken ct)
    {
        try
        {
            var run = await gate.RunAsync(async token =>
            {
                await using var scope = scopes.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<ISessionConsolidator>().ConsolidateAllEndedAsync(token);
            }, ct);

            if (run.Consolidated > 0 || run.Failures.Count > 0)
            {
                logger.LogInformation(
                    "Consolidated {Consolidated} thread sessions; {Failed} failed.", run.Consolidated, run.Failures.Count);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "A consolidation pass failed.");
        }
    }
}
