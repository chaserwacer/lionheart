using lionheart.Functional;

namespace lionheart.Data;

/// <summary>
/// The seam where a fallible piece of work that saves as it goes meets a database transaction.
/// </summary>
/// <remarks>
/// Most writes stage into the change tracker and save once, so a failure that returns before the
/// save writes nothing without any transaction. Work that has to save part-way — a bulk delete
/// before a replay reads what remains — needs a transaction to stay all-or-nothing, and this is
/// the one place that decides from the work's result whether to keep it.
/// </remarks>
internal static class Transactions
{
    extension(ModelContext context)
    {
        /// <summary>
        /// Runs the work in a transaction, committing only a success that <paramref name="keep"/>
        /// says is worth writing; anything else is rolled back.
        /// </summary>
        public async Task<Result<T, Problem>> InTransactionAsync<T>(
            Func<Task<Result<T, Problem>>> work, Func<T, bool> keep, CancellationToken ct)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var result = await work();
            if (result is Success<T, Problem> success && keep(success.Value))
                await transaction.CommitAsync(ct);
            else
                await transaction.RollbackAsync(ct);

            return result;
        }

        /// <summary>Runs the work in a transaction, committing it if it succeeds.</summary>
        public Task<Result<T, Problem>> InTransactionAsync<T>(Func<Task<Result<T, Problem>>> work, CancellationToken ct) =>
            context.InTransactionAsync(work, _ => true, ct);
    }
}
