using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace lionheart.Functional;

/// <summary>
/// The combinator vocabulary for <see cref="Result{T, TProblem}"/>, grouped by receiver.
/// </summary>
/// <remarks>
/// Real orchestrations are <c>Task&lt;Result&lt;T, TProblem&gt;&gt;</c> — two monads stacked, which
/// C# cannot abstract over — so every operation is written out for both source shapes and both
/// selector shapes. A missing cell is what forces <c>await (await …)</c> at a call site, so the
/// grid is filled rather than grown on demand.
/// <para>
/// On the <c>Task</c> source the sync- and async-selector overloads are indistinguishable to
/// inference when the selector is an <c>async</c> lambda (both land on
/// <c>Func&lt;T, Task&lt;U&gt;&gt;</c>), so the async-selector member claims resolution priority.
/// </para>
/// </remarks>
public static class ResultExtensions
{
    extension<T, TProblem>(Result<T, TProblem> source)
    {
        /// <summary>A step that cannot fail: transforms the value, leaves the problem alone.</summary>
        public Result<U, TProblem> Map<U>(Func<T, U> selector) =>
            source switch
            {
                Success<T, TProblem> success => Result<U, TProblem>.Success(selector(success.Value)),
                Failure<T, TProblem> failure => Result<U, TProblem>.Failure(failure.Problem),
                _ => throw new UnreachableException()
            };

        /// <summary>A step that can fail: chains without unwrapping in between.</summary>
        public Result<U, TProblem> Bind<U>(Func<T, Result<U, TProblem>> selector) =>
            source switch
            {
                Success<T, TProblem> success => selector(success.Value),
                Failure<T, TProblem> failure => Result<U, TProblem>.Failure(failure.Problem),
                _ => throw new UnreachableException()
            };

        /// <summary>A layer boundary: restates a lower layer's problem in this layer's vocabulary.</summary>
        public Result<T, TOther> MapError<TOther>(Func<TProblem, TOther> selector) =>
            source switch
            {
                Success<T, TProblem> success => Result<T, TOther>.Success(success.Value),
                Failure<T, TProblem> failure => Result<T, TOther>.Failure(selector(failure.Problem)),
                _ => throw new UnreachableException()
            };

        /// <summary>Leaves the railway with both cases answered.</summary>
        public U Match<U>(Func<T, U> onSuccess, Func<TProblem, U> onFailure) =>
            source switch
            {
                Success<T, TProblem> success => onSuccess(success.Value),
                Failure<T, TProblem> failure => onFailure(failure.Problem),
                _ => throw new UnreachableException()
            };

        /// <summary>Turns a successful value that fails a condition into a problem.</summary>
        public Result<T, TProblem> Ensure(Func<T, bool> predicate, Func<T, TProblem> onFalse) =>
            source is Success<T, TProblem> success && !predicate(success.Value)
                ? Result<T, TProblem>.Failure(onFalse(success.Value))
                : source;

        public async Task<Result<U, TProblem>> MapAsync<U>(Func<T, Task<U>> selector) =>
            source switch
            {
                Success<T, TProblem> success => Result<U, TProblem>.Success(await selector(success.Value)),
                Failure<T, TProblem> failure => Result<U, TProblem>.Failure(failure.Problem),
                _ => throw new UnreachableException()
            };

        public Task<Result<U, TProblem>> BindAsync<U>(Func<T, Task<Result<U, TProblem>>> selector) =>
            source switch
            {
                Success<T, TProblem> success => selector(success.Value),
                Failure<T, TProblem> failure => Task.FromResult(Result<U, TProblem>.Failure(failure.Problem)),
                _ => throw new UnreachableException()
            };

        public async Task<Result<T, TOther>> MapErrorAsync<TOther>(Func<TProblem, Task<TOther>> selector) =>
            source switch
            {
                Success<T, TProblem> success => Result<T, TOther>.Success(success.Value),
                Failure<T, TProblem> failure => Result<T, TOther>.Failure(await selector(failure.Problem)),
                _ => throw new UnreachableException()
            };

        public Task<U> MatchAsync<U>(Func<T, Task<U>> onSuccess, Func<TProblem, Task<U>> onFailure) =>
            source switch
            {
                Success<T, TProblem> success => onSuccess(success.Value),
                Failure<T, TProblem> failure => onFailure(failure.Problem),
                _ => throw new UnreachableException()
            };
    }

    extension<T, TProblem>(Task<Result<T, TProblem>> source)
    {
        public async Task<Result<U, TProblem>> MapAsync<U>(Func<T, U> selector) =>
            (await source).Map(selector);

        [OverloadResolutionPriority(1)]
        public async Task<Result<U, TProblem>> MapAsync<U>(Func<T, Task<U>> selector)
        {
            var awaited = await source;
            return await awaited.MapAsync(selector);
        }

        public async Task<Result<U, TProblem>> BindAsync<U>(Func<T, Result<U, TProblem>> selector) =>
            (await source).Bind(selector);

        [OverloadResolutionPriority(1)]
        public async Task<Result<U, TProblem>> BindAsync<U>(Func<T, Task<Result<U, TProblem>>> selector)
        {
            var awaited = await source;
            return await awaited.BindAsync(selector);
        }

        public async Task<Result<T, TOther>> MapErrorAsync<TOther>(Func<TProblem, TOther> selector) =>
            (await source).MapError(selector);

        [OverloadResolutionPriority(1)]
        public async Task<Result<T, TOther>> MapErrorAsync<TOther>(Func<TProblem, Task<TOther>> selector)
        {
            var awaited = await source;
            return await awaited.MapErrorAsync(selector);
        }

        public async Task<Result<T, TProblem>> EnsureAsync(
            Func<T, bool> predicate, Func<T, TProblem> onFalse) =>
            (await source).Ensure(predicate, onFalse);

        public async Task<U> MatchAsync<U>(Func<T, U> onSuccess, Func<TProblem, U> onFailure) =>
            (await source).Match(onSuccess, onFailure);

        [OverloadResolutionPriority(1)]
        public async Task<U> MatchAsync<U>(Func<T, Task<U>> onSuccess, Func<TProblem, Task<U>> onFailure)
        {
            var awaited = await source;
            return await awaited.MatchAsync(onSuccess, onFailure);
        }
    }

    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// N results into one result of N, fail-fast: the first problem is the whole story and the
        /// remaining items are not evaluated.
        /// </summary>
        public Result<IReadOnlyList<U>, TProblem> Traverse<U, TProblem>(Func<T, Result<U, TProblem>> selector)
        {
            var mapped = new List<U>();
            foreach (var item in source)
            {
                switch (selector(item))
                {
                    case Success<U, TProblem> success:
                        mapped.Add(success.Value);
                        break;
                    case Failure<U, TProblem> failure:
                        return Result<IReadOnlyList<U>, TProblem>.Failure(failure.Problem);
                    default:
                        throw new UnreachableException();
                }
            }

            return Result<IReadOnlyList<U>, TProblem>.Success(mapped);
        }

        /// <summary>
        /// <see cref="Traverse"/> over a step that is asynchronous. The steps run one after
        /// another rather than concurrently: the callers here share one EF context per request,
        /// which is not safe for two operations at a time.
        /// </summary>
        public async Task<Result<IReadOnlyList<U>, TProblem>> TraverseAsync<U, TProblem>(
            Func<T, Task<Result<U, TProblem>>> selector)
        {
            var mapped = new List<U>();
            foreach (var item in source)
            {
                switch (await selector(item))
                {
                    case Success<U, TProblem> success:
                        mapped.Add(success.Value);
                        break;
                    case Failure<U, TProblem> failure:
                        return Result<IReadOnlyList<U>, TProblem>.Failure(failure.Problem);
                    default:
                        throw new UnreachableException();
                }
            }

            return Result<IReadOnlyList<U>, TProblem>.Success(mapped);
        }
    }
}
