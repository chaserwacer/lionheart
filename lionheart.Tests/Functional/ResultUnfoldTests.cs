using lionheart.Functional;
using lionheart.Tests.Support;

namespace lionheart.Tests.Functional;

/// <summary>
/// <see cref="Result.UnfoldAsync{TState, TResult, TProblem}"/>, the combinator the chat tool loop
/// is built on. The whole point of it is that the <c>while</c> and the case test live here rather
/// than in every orchestrator that repeats, so what it owes its callers is asserted once.
/// </summary>
public class ResultUnfoldTests
{
    private static Task<Result<Loop<int, string>, Problem>> Continue(int next) =>
        Task.FromResult(Result<Loop<int, string>, Problem>.Success(new Loop<int, string>.Continue(next)));

    private static Task<Result<Loop<int, string>, Problem>> Done(string result) =>
        Task.FromResult(Result<Loop<int, string>, Problem>.Success(new Loop<int, string>.Done(result)));

    private static Task<Result<Loop<int, string>, Problem>> Refuse(string why) =>
        Task.FromResult(Result<Loop<int, string>, Problem>.Failure(new Problem.Upstream(why)));

    // ---- The laws ----

    /// <summary>A step that is done on the seed is the whole walk: the seed is not skipped.</summary>
    [Fact]
    public async Task A_walk_that_finishes_immediately_returns_the_first_steps_result()
    {
        var seen = new List<int>();

        var result = await Result.UnfoldAsync(7, (state, _) =>
        {
            seen.Add(state);
            return Done($"done at {state}");
        });

        Assert.Equal("done at 7", ResultAssert.Succeeded(result));
        Assert.Equal([7], seen);
    }

    /// <summary>Each round's state is the one the round before produced, in order.</summary>
    [Fact]
    public async Task Each_step_runs_on_the_state_the_previous_one_produced()
    {
        var seen = new List<int>();

        var result = await Result.UnfoldAsync(0, (state, _) =>
        {
            seen.Add(state);
            return state < 3 ? Continue(state + 1) : Done($"stopped at {state}");
        });

        Assert.Equal("stopped at 3", ResultAssert.Succeeded(result));
        Assert.Equal([0, 1, 2, 3], seen);
    }

    /// <summary>
    /// The result is the walk's outcome, not its bookkeeping — the two are allowed to be different
    /// types, which is what lets a tool loop carry a conversation and return a response.
    /// </summary>
    [Fact]
    public async Task The_result_is_the_outcome_rather_than_the_final_state()
    {
        var result = await Result.UnfoldAsync(1, (state, _) =>
            state < 3 ? Continue(state + 1) : Done("an outcome, not a number"));

        Assert.Equal("an outcome, not a number", ResultAssert.Succeeded(result));
    }

    [Fact]
    public async Task A_failing_step_fails_the_whole_walk_with_its_problem()
    {
        var result = await Result.UnfoldAsync(0, (state, _) =>
            state < 2 ? Continue(state + 1) : Refuse("the source gave up"));

        Assert.Contains("the source gave up", ResultAssert.Failed(result).Description);
    }

    // ---- The short-circuit ----

    /// <summary>
    /// The claim every caller relies on: once a step fails, nothing further runs. A combinator that
    /// kept walking would pass every law above while doing work it was meant to skip.
    /// </summary>
    [Fact]
    public async Task A_failure_stops_the_walk_rather_than_continuing_past_it()
    {
        var rounds = 0;

        var result = await Result.UnfoldAsync(0, (state, _) =>
        {
            rounds++;
            return state == 1 ? Refuse("stop here") : Continue(state + 1);
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(2, rounds);
    }

    [Fact]
    public async Task A_done_step_stops_the_walk_rather_than_running_another_round()
    {
        var rounds = 0;

        await Result.UnfoldAsync(0, (state, _) =>
        {
            rounds++;
            return state == 1 ? Done("finished") : Continue(state + 1);
        });

        Assert.Equal(2, rounds);
    }

    // ---- Cancellation ----

    /// <summary>
    /// An unbounded loop has to answer a cancellation, and the token reaching each step is what
    /// lets a step abandon its own I/O rather than only the walk abandoning the next step.
    /// </summary>
    [Fact]
    public async Task A_cancelled_walk_stops_rather_than_running_forever()
    {
        using var cancellation = new CancellationTokenSource();
        var rounds = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Result.UnfoldAsync(0, (state, _) =>
            {
                rounds++;
                if (rounds == 3) cancellation.Cancel();
                return Continue(state + 1);
            }, cancellation.Token));

        Assert.Equal(3, rounds);
    }

    [Fact]
    public async Task The_token_reaches_each_step()
    {
        using var cancellation = new CancellationTokenSource();

        var result = await Result.UnfoldAsync(0, (_, token) =>
            Done(token == cancellation.Token ? "the step got the token" : "the step got some other token"),
            cancellation.Token);

        Assert.Equal("the step got the token", ResultAssert.Succeeded(result));
    }

    // ---- The Loop hierarchy ----

    /// <summary>
    /// <see cref="Loop{TState, TResult}"/> is a closed set: a third case would mean a round that is
    /// neither carrying on nor finished, which the combinator could not answer.
    /// </summary>
    [Fact]
    public void A_round_either_carries_on_or_is_done_and_nothing_else()
    {
        var declared = typeof(Loop<,>).GetNestedTypes()
            .Where(t => t.IsSealed && t.BaseType?.GetGenericTypeDefinition() == typeof(Loop<,>))
            .ToList();

        Assert.Equal(2, declared.Count);
        Assert.Contains(typeof(Loop<,>.Continue), declared);
        Assert.Contains(typeof(Loop<,>.Done), declared);
    }

    /// <summary>
    /// The base constructor is <c>private protected</c>, so no file but its own can declare a
    /// third round. The record's compiler-generated copy constructor is excluded: it takes the
    /// declaring type and is not a way to subclass anything.
    /// </summary>
    [Fact]
    public void The_hierarchy_cannot_be_extended_from_outside_its_own_file()
    {
        var declarable = typeof(Loop<int, string>)
            .GetConstructors(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Where(c => c.GetParameters() is not [{ ParameterType: var p }] || p != typeof(Loop<int, string>))
            .ToList();

        Assert.All(declarable, constructor => Assert.True(constructor.IsFamilyAndAssembly));
    }
}
