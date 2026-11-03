namespace lionheart.Services.Chat.Thread;

/// <summary>
/// One writer to the thread at a time: an exchange, or a pass of consolidation.
/// </summary>
/// <remarks>
/// Two messages racing would each find the newest session ended and open two sessions; a
/// consolidation racing an exchange that consolidates lazily would distil one session twice. One
/// process serves one athlete, so a process-wide gate costs nothing a single user would notice, and
/// it is simpler and stronger than a concurrency token on each row the two might touch.
/// </remarks>
public sealed class ChatThreadGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await work(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
