using lionheart.Functional;

namespace lionheart.Tests.Functional;

/// <summary>
/// <c>Recover</c>: a failure brought back onto the success track as a value. Its laws are that a
/// success passes through untouched without running the selector, and a failure becomes exactly
/// what the selector made of its problem.
/// </summary>
public class ResultRecoverTests
{
    [Fact]
    public void A_success_passes_through_and_the_selector_never_runs()
    {
        var ran = false;
        var source = Result<int, Problem>.Success(3);

        var recovered = source.Recover(_ => { ran = true; return 0; });

        Assert.Equal(source, recovered);
        Assert.False(ran);
    }

    [Fact]
    public void A_failure_becomes_the_value_made_from_its_problem()
    {
        var source = Result<string, Problem>.Failure(new Problem.Upstream("down"));

        Assert.Equal(Result<string, Problem>.Success("down"), source.Recover(problem => problem.Description));
    }

    [Fact]
    public async Task The_task_form_recovers_the_same_way()
    {
        var source = Task.FromResult(Result<int, Problem>.Failure(new Problem.Upstream("down")));

        Assert.Equal(Result<int, Problem>.Success(-1), await source.RecoverAsync(_ => -1));
    }
}
