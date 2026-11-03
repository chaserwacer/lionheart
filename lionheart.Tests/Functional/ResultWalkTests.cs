using lionheart.Functional;
using lionheart.Tests.Support;

namespace lionheart.Tests.Functional;

/// <summary>
/// The two combinators the Oura work added to the monad layer: an unfold, for a walk whose next
/// step comes out of the last one, and a partition, for a batch where one refusal must not stand
/// for the whole.
/// </summary>
/// <remarks>
/// They are tested here rather than only through their callers because the layer every pipeline
/// composes through is the wrong place for an untested shape — a short-circuit that does not
/// short-circuit is invisible until something depends on it.
/// </remarks>
public class ResultWalkTests
{
    // ---- UnfoldAsync ----

    [Fact]
    public async Task An_unfold_whose_first_step_is_done_runs_that_step_once()
    {
        var steps = 0;

        var walked = await Result.UnfoldAsync(
            0,
            (state, _) => Step(() => { steps++; return Done($"stopped at {state}"); }),
            CancellationToken.None);

        Assert.Equal("stopped at 0", ResultAssert.Succeeded(walked));
        Assert.Equal(1, steps);
    }

    [Fact]
    public async Task An_unfold_carries_each_steps_state_into_the_next()
    {
        var seen = new List<int>();

        var walked = await Result.UnfoldAsync(
            1,
            (state, _) => Step(() =>
            {
                seen.Add(state);
                return state < 4 ? Continue(state + 1) : Done($"ended on {state}");
            }),
            CancellationToken.None);

        Assert.Equal("ended on 4", ResultAssert.Succeeded(walked));
        Assert.Equal([1, 2, 3, 4], seen);
    }

    [Fact]
    public async Task A_step_that_fails_ends_the_walk_with_that_problem_and_runs_no_more()
    {
        var steps = 0;

        var walked = await Result.UnfoldAsync(
            1,
            (state, _) => Step(() =>
            {
                steps++;
                return state is 3
                    ? Result<Loop<int, string>, Problem>.Failure(new Problem.Upstream("page three"))
                    : Continue(state + 1);
            }),
            CancellationToken.None);

        var problem = Assert.IsType<Problem.Upstream>(ResultAssert.Failed(walked));
        Assert.Equal("page three", problem.Detail);
        Assert.Equal(3, steps);
    }

    [Fact]
    public async Task An_unfold_hands_each_step_the_callers_token()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationToken handed = default;

        await Result.UnfoldAsync(
            0,
            (_, ct) => Step(() => { handed = ct; return Done("done"); }),
            cancellation.Token);

        Assert.Equal(cancellation.Token, handed);
    }

    [Fact]
    public async Task An_unfold_cancelled_part_way_through_stops_walking()
    {
        using var cancellation = new CancellationTokenSource();

        var walking = Result.UnfoldAsync(
            1,
            (state, _) => Step(() =>
            {
                if (state is 3) cancellation.Cancel();
                return Continue(state + 1);
            }),
            cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => walking);
    }

    // ---- Partition ----

    [Fact]
    public void A_partition_keeps_both_what_succeeded_and_every_refusal()
    {
        var partitioned = new[] { 1, 2, 3, 4, 5 }.Partition(Even);

        Assert.Equal([2, 4], partitioned.Delivered);
        Assert.Equal(
            ["1 is odd", "3 is odd", "5 is odd"],
            partitioned.Refused.Select(problem => problem.Description));
    }

    [Fact]
    public void A_partition_does_not_stop_at_the_first_refusal()
    {
        // The whole difference from Traverse: one bad record out of a thousand must be reportable
        // without costing the other nine hundred and ninety-nine.
        var partitioned = new[] { 1, 2 }.Partition(Even);

        Assert.Single(partitioned.Delivered);
        Assert.Single(partitioned.Refused);
    }

    [Fact]
    public void A_partition_of_nothing_delivers_and_refuses_nothing()
    {
        var partitioned = Array.Empty<int>().Partition(Even);

        Assert.Empty(partitioned.Delivered);
        Assert.Empty(partitioned.Refused);
    }

    [Fact]
    public void A_partition_where_everything_succeeds_matches_what_Traverse_delivers()
    {
        var items = new[] { 2, 4, 6 };

        var partitioned = items.Partition(Even);
        var traversed = ResultAssert.Succeeded(items.Traverse(Even));

        Assert.Equal(traversed, partitioned.Delivered);
        Assert.Empty(partitioned.Refused);
    }

    [Fact]
    public void A_partition_preserves_the_order_of_what_it_delivers()
    {
        var partitioned = new[] { 4, 1, 2, 3, 6 }.Partition(Even);

        Assert.Equal([4, 2, 6], partitioned.Delivered);
    }

    [Fact]
    public void A_partition_runs_its_step_exactly_once_per_item()
    {
        var calls = 0;

        var partitioned = new[] { 1, 2, 3 }.Partition(item =>
        {
            calls++;
            return Even(item);
        });

        Assert.Equal(3, calls);
        Assert.Single(partitioned.Delivered);
    }

    // ---- Helpers ----

    private static Result<int, Problem> Even(int value) =>
        value % 2 is 0
            ? Result<int, Problem>.Success(value)
            : Result<int, Problem>.Failure(new Problem.Invalid("value", $"{value} is odd"));

    private static Task<Result<Loop<int, string>, Problem>> Step(Func<Result<Loop<int, string>, Problem>> step) =>
        Task.FromResult(step());

    private static Result<Loop<int, string>, Problem> Continue(int state) =>
        Result<Loop<int, string>, Problem>.Success(new Loop<int, string>.Continue(state));

    private static Result<Loop<int, string>, Problem> Done(string result) =>
        Result<Loop<int, string>, Problem>.Success(new Loop<int, string>.Done(result));
}
