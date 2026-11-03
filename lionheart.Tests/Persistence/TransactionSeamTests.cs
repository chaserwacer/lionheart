using lionheart.Data;
using lionheart.Functional;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// The seam that keeps work saving part-way all-or-nothing: what it keeps and what it rolls back.
/// </summary>
public class TransactionSeamTests : IDisposable
{
    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Work_that_succeeds_is_kept()
    {
        var kept = await RunAsync(saved => Result<int, Problem>.Success(saved), keep: _ => true);

        Assert.Equal(1, ResultAssert.Succeeded(kept));
        Assert.Equal(1, await ProgramsAsync());
    }

    [Fact]
    public async Task Work_that_saved_and_then_failed_leaves_nothing_written()
    {
        var failed = await RunAsync(_ => Result<int, Problem>.Failure(new Problem.Conflict("refused after saving")), keep: _ => true);

        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(failed));
        Assert.Equal(0, await ProgramsAsync());
    }

    [Fact]
    public async Task A_success_not_worth_keeping_is_rolled_back_and_still_returned()
    {
        var unkept = await RunAsync(saved => Result<int, Problem>.Success(saved), keep: _ => false);

        Assert.Equal(1, ResultAssert.Succeeded(unkept));
        Assert.Equal(0, await ProgramsAsync());
    }

    /// <summary>Saves one program inside the seam, then answers as the test says.</summary>
    private async Task<Result<int, Problem>> RunAsync(Func<int, Result<int, Problem>> answer, Func<int, bool> keep)
    {
        await using var context = _database.CreateContext();
        return await context.InTransactionAsync(
            async () =>
            {
                context.Programs.Add(Some.CompletedProgram(_database.UserId));
                return answer(await context.SaveChangesAsync());
            },
            keep,
            CancellationToken.None);
    }

    private async Task<int> ProgramsAsync()
    {
        await using var context = _database.CreateContext();
        return await context.Programs.CountAsync();
    }
}
