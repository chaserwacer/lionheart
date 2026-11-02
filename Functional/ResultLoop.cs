namespace lionheart.Functional;

/// <summary>
/// What one step of an <see cref="Result.UnfoldAsync{TState, TResult, TProblem}"/> decided: carry
/// on with a new state, or stop with the walk's outcome.
/// </summary>
/// <remarks>
/// The two cases are what a hand-written loop hides in a <c>bool keepGoing</c> plus an early
/// <c>return</c>. Naming them is what lets the loop body be an ordinary fallible step and keeps the
/// case test inside the monad layer, where it belongs.
/// </remarks>
public abstract record Loop<TState, TResult>
{
    private protected Loop() { }

    public sealed record Continue(TState State) : Loop<TState, TResult>;

    public sealed record Done(TResult Result) : Loop<TState, TResult>;
}

/// <summary>
/// Combinators that do not hang off a single <see cref="Result{T, TProblem}"/> receiver. Sits
/// beside the generic type because the two differ in arity.
/// </summary>
public static class Result
{
    /// <summary>
    /// Runs <paramref name="step"/> from <paramref name="seed"/>, feeding each state into the next,
    /// until a step answers <see cref="Loop{TState, TResult}.Done"/> — or fails, which ends the walk
    /// on the failure track.
    /// </summary>
    /// <remarks>
    /// The sequence is not known up front: each round's input comes out of the last one. A model's
    /// tool loop and a paged listing are the same shape, and this is the one place the <c>while</c>
    /// and the case test that drive them are written.
    /// </remarks>
    public static async Task<Result<TResult, TProblem>> UnfoldAsync<TState, TResult, TProblem>(
        TState seed,
        Func<TState, CancellationToken, Task<Result<Loop<TState, TResult>, TProblem>>> step,
        CancellationToken cancellationToken = default)
    {
        var state = seed;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (await step(state, cancellationToken))
            {
                case Success<Loop<TState, TResult>, TProblem> { Value: Loop<TState, TResult>.Continue next }:
                    state = next.State;
                    continue;
                case Success<Loop<TState, TResult>, TProblem> { Value: Loop<TState, TResult>.Done done }:
                    return Result<TResult, TProblem>.Success(done.Result);
                case Failure<Loop<TState, TResult>, TProblem> refused:
                    return Result<TResult, TProblem>.Failure(refused.Problem);
            }
        }
    }
}
