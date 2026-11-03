using lionheart.Functional;

namespace lionheart.Tests.Functional;

/// <summary>
/// <c>Tap</c> and <c>TapError</c>: a side effect on one track that leaves the result alone. Their
/// laws are what every caller relies on — a <c>Tap</c> that altered the value, or ran on a failure,
/// would be a silent corruption rather than a visible bug.
/// </summary>
public class ResultTapTests
{
    private static Result<int, Problem> Succeeded(int value = 3) => Result<int, Problem>.Success(value);

    private static Result<int, Problem> Refused(string why = "nope") =>
        Result<int, Problem>.Failure(new Problem.Upstream(why));

    // ---- Tap ----

    [Fact]
    public void Tap_never_changes_the_value()
    {
        var source = Succeeded();

        Assert.Equal(source, source.Tap(_ => { }));
    }

    [Fact]
    public void Tap_never_changes_the_problem()
    {
        var source = Refused();

        Assert.Equal(source, source.Tap(_ => { }));
    }

    [Fact]
    public void Tap_runs_its_effect_on_the_value()
    {
        var seen = new List<int>();

        Succeeded(7).Tap(seen.Add);

        Assert.Equal([7], seen);
    }

    [Fact]
    public void Tap_never_runs_on_a_failure()
    {
        var ran = false;

        Refused().Tap(_ => ran = true);

        Assert.False(ran);
    }

    // ---- TapError ----

    [Fact]
    public void TapError_never_changes_the_problem()
    {
        var source = Refused();

        Assert.Equal(source, source.TapError(_ => { }));
    }

    [Fact]
    public void TapError_runs_its_effect_on_the_problem()
    {
        var seen = new List<string>();

        Refused("the model refused").TapError(problem => seen.Add(problem.Description));

        Assert.Equal(["the model refused"], seen);
    }

    [Fact]
    public void TapError_never_runs_on_a_success()
    {
        var ran = false;

        Succeeded().TapError(_ => ran = true);

        Assert.False(ran);
    }

    // ---- The async cells agree with the synchronous ones ----

    /// <summary>
    /// The async overload's failure mode is not a wrong law but disagreement with the sibling it
    /// mirrors, so it is tested as agreement.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TapAsync_agrees_with_Tap(bool succeeded)
    {
        var source = succeeded ? Succeeded(7) : Refused();
        var synchronous = new List<int>();
        var asynchronous = new List<int>();

        var expected = source.Tap(synchronous.Add);
        var actual = await Task.FromResult(source).TapAsync(asynchronous.Add);

        Assert.Equal(expected, actual);
        Assert.Equal(synchronous, asynchronous);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TapErrorAsync_agrees_with_TapError(bool succeeded)
    {
        var source = succeeded ? Succeeded(7) : Refused();
        var synchronous = new List<string>();
        var asynchronous = new List<string>();

        var expected = source.TapError(p => synchronous.Add(p.Description));
        var actual = await Task.FromResult(source).TapErrorAsync(p => asynchronous.Add(p.Description));

        Assert.Equal(expected, actual);
        Assert.Equal(synchronous, asynchronous);
    }

    // ---- An awaited effect on the failure track ----

    [Fact]
    public async Task An_awaited_TapErrorAsync_finishes_its_effect_before_the_failure_passes_on()
    {
        var seen = new List<string>();

        var result = await Task.FromResult(Refused("the model refused")).TapErrorAsync(async problem =>
        {
            await Task.Yield();
            seen.Add(problem.Description);
        });

        Assert.Equal(["the model refused"], seen);
        Assert.Equal(Refused("the model refused"), result);
    }

    [Fact]
    public async Task An_awaited_TapErrorAsync_never_runs_on_a_success()
    {
        var ran = false;

        var result = await Task.FromResult(Succeeded(7)).TapErrorAsync(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.False(ran);
        Assert.Equal(Succeeded(7), result);
    }
}
