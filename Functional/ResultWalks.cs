using System.Diagnostics;

namespace lionheart.Functional;

/// <summary>
/// The combinators whose receiver is not one <see cref="Result{T, TProblem}"/>: walks that repeat a
/// fallible step, and the split of many results into what succeeded and what did not.
/// </summary>
/// <remarks>
/// These are the two places an orchestrator would otherwise write a loop that inspects a result's
/// case — the thing the house standards reserve to this layer. Both live on a non-generic
/// <c>Result</c>, which can sit beside the generic record because the two differ in arity.
/// </remarks>
public static class Result
{
    /// <summary>
    /// Repeats a step that produces the input to the next one, until the step says it is done.
    /// </summary>
    /// <remarks>
    /// The shape of walking a paged API: the cursor for page two comes out of page one, so the
    /// sequence is not known up front and a <c>Traverse</c> cannot express it. The step runs first
    /// on <paramref name="seed"/>, carries whatever it needs in its state, and answers with a
    /// <see cref="Loop{TState, TResult}"/>; a failure from any step ends the walk with that
    /// problem. The result type is deliberately free of the state type, so a walk can answer with
    /// its outcome rather than its bookkeeping.
    /// </remarks>
    public static async Task<Result<TResult, TProblem>> UnfoldAsync<TState, TResult, TProblem>(
        TState seed,
        Func<TState, CancellationToken, Task<Result<Loop<TState, TResult>, TProblem>>> step,
        CancellationToken ct)
    {
        var state = seed;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            switch (await step(state, ct))
            {
                case Success<Loop<TState, TResult>, TProblem> stepped:
                    switch (stepped.Value)
                    {
                        case Loop<TState, TResult>.Continue carry:
                            state = carry.State;
                            continue;
                        case Loop<TState, TResult>.Done done:
                            return Result<TResult, TProblem>.Success(done.Result);
                        default:
                            throw new UnreachableException();
                    }

                case Failure<Loop<TState, TResult>, TProblem> failed:
                    return Result<TResult, TProblem>.Failure(failed.Problem);

                default:
                    throw new UnreachableException();
            }
        }
    }
}

/// <summary>What a batch of independent attempts produced, and what each refusal was.</summary>
/// <remarks>
/// Both halves are answers. A batch that refuses nine of ten items still delivered one, and a
/// caller that has to report what it skipped needs the refusals as data rather than as a single
/// problem that discarded the other eight.
/// </remarks>
public sealed record Partitioned<T, TProblem>(IReadOnlyList<T> Delivered, IReadOnlyList<TProblem> Refused);

public static class PartitionExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// Runs a fallible step over every item and keeps both outcomes, rather than letting the
        /// first refusal stand for the whole batch.
        /// </summary>
        /// <remarks>
        /// The shape <see cref="ResultExtensions.Traverse"/> cannot express, and the one a bulk
        /// load needs: one malformed record out of a thousand must be reportable without costing
        /// the other nine hundred and ninety-nine. Where a refusal genuinely makes the remaining
        /// work meaningless, that is still <c>Traverse</c>.
        /// </remarks>
        public Partitioned<U, TProblem> Partition<U, TProblem>(Func<T, Result<U, TProblem>> selector)
        {
            var delivered = new List<U>();
            var refused = new List<TProblem>();

            foreach (var item in source)
            {
                switch (selector(item))
                {
                    case Success<U, TProblem> success:
                        delivered.Add(success.Value);
                        break;
                    case Failure<U, TProblem> failure:
                        refused.Add(failure.Problem);
                        break;
                    default:
                        throw new UnreachableException();
                }
            }

            return new Partitioned<U, TProblem>(delivered, refused);
        }
    }
}
