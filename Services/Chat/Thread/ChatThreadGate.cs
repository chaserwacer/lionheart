namespace lionheart.Services.Chat.Thread;

/// <summary>
/// Two locks, one process-wide: one writer to the thread at a time, and one writer to memory.
/// </summary>
/// <remarks>
/// The thread lock covers an exchange and a pass of consolidation. Two messages racing would each
/// find the newest session ended and open two sessions; a consolidation racing an exchange that
/// consolidates lazily would distil one session twice. It is held for as long as a model answers.
/// <para>
/// The memory lock covers every write to memory: the athlete correcting or forgetting a memory, and
/// a consolidation applying its changes. It is held only for the write, never across a model call,
/// so a correction made while a reply is out waits a moment rather than for the reply. A
/// consolidation re-reads the memories it is about to change inside it, so a change the athlete
/// made while the model was deciding is seen, and a revision of a memory he has since replaced or
/// forgotten is set aside.
/// </para>
/// <para>
/// One process serves one athlete, so process-wide locks cost nothing he would notice, and they are
/// simpler than a concurrency token on every row the writers share.
/// </para>
/// </remarks>
public sealed class ChatThreadGate : IDisposable
{
    private readonly SemaphoreSlim _thread = new(1, 1);
    private readonly SemaphoreSlim _memory = new(1, 1);

    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
        HoldingAsync(_thread, work, ct);

    public Task<T> WriteMemoryAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
        HoldingAsync(_memory, work, ct);

    public void Dispose()
    {
        _thread.Dispose();
        _memory.Dispose();
    }

    private static async Task<T> HoldingAsync<T>(SemaphoreSlim held, Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await held.WaitAsync(ct);
        try
        {
            return await work(ct);
        }
        finally
        {
            held.Release();
        }
    }
}
