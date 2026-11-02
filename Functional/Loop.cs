namespace lionheart.Functional;

/// <summary>
/// What one step of a repeating walk decided: carry on from a new state, or stop with an answer.
/// </summary>
/// <remarks>
/// Two cases rather than a nullable "next" value, because "keep going" and "finished" are
/// different answers and a step that returns the second still has a result to hand back. This is
/// the type that lets a paging crawl be an expression instead of a <c>while</c> loop whose body
/// inspects a <see cref="Result{T, TProblem}"/>'s case by hand.
/// </remarks>
public abstract record Loop<TState, TResult>
{
    private protected Loop() { }

    public sealed record Continue(TState State) : Loop<TState, TResult>;

    public sealed record Done(TResult Result) : Loop<TState, TResult>;
}
