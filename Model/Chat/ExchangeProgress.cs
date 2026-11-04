namespace lionheart.Model.Chat;

/// <summary>
/// What an exchange has done so far, said while it is still running so the athlete is not left
/// looking at a wait. How it ends — the stored session, or why it failed — is the exchange's
/// result, not progress.
/// </summary>
public abstract record ExchangeProgress
{
    private protected ExchangeProgress() { }

    /// <summary>The next piece of the answer's text, as the model wrote it.</summary>
    public sealed record Wrote(string Text) : ExchangeProgress;

    /// <summary>
    /// The model ended a round by asking for these tools, and they are being run. Whatever it wrote
    /// in that round was a preamble rather than the answer, and is withdrawn: only the last round's
    /// text is the answer, and only it is stored.
    /// </summary>
    public sealed record Consulting(IReadOnlyList<string> Tools) : ExchangeProgress;
}

/// <summary>Whoever is following an exchange as it runs.</summary>
/// <remarks>
/// Synchronous on purpose. It is told from inside the model's stream, and an observer that could
/// make the stream wait — a slow phone on a weak signal — would hold the model's connection open
/// for as long as the phone took. An observer that has somewhere slow to send things queues them.
/// </remarks>
public interface IExchangeObserver
{
    void Observe(ExchangeProgress progress);
}

public static class ExchangeObserver
{
    /// <summary>For an exchange nobody is following: answered in one piece once it is done.</summary>
    public static IExchangeObserver Unwatched { get; } = new Nobody();

    private sealed class Nobody : IExchangeObserver
    {
        public void Observe(ExchangeProgress progress) { }
    }
}
