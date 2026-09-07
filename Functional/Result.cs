namespace lionheart.Functional;

/// <summary>
/// The outcome of an operation that is allowed to refuse: either a value, or the problem that
/// stopped it. Expected failures travel here rather than as exceptions or bare nulls.
/// </summary>
/// <remarks>
/// The <c>private protected</c> constructor closes the hierarchy to this file, so
/// <see cref="Success{T, TProblem}"/> and <see cref="Failure{T, TProblem}"/> are the only cases
/// a switch has to consider. Compose with the combinators in <see cref="ResultExtensions"/>
/// rather than unwrapping; <c>using static</c> on a closed
/// <c>Result&lt;SomeType, Problem&gt;</c> keeps files that construct many of these readable.
/// </remarks>
public abstract record Result<T, TProblem>
{
    private protected Result() { }

    public abstract bool IsSuccess { get; }

    public static Result<T, TProblem> Success(T value) => new Success<T, TProblem>(value);

    public static Result<T, TProblem> Failure(TProblem problem) => new Failure<T, TProblem>(problem);
}

public sealed record Success<T, TProblem>(T Value) : Result<T, TProblem>
{
    public override bool IsSuccess => true;
}

public sealed record Failure<T, TProblem>(TProblem Problem) : Result<T, TProblem>
{
    public override bool IsSuccess => false;
}

/// <summary>
/// The value an operation returns when succeeding says everything there is to say — a delete, a
/// sync. Keeps such operations inside the same <see cref="Result{T, TProblem}"/> vocabulary as
/// every other fallible call instead of needing a second, value-less result type.
/// </summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
