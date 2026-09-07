using lionheart.Data;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

/// <summary>
/// Keeping the analysis read model in step with the rows it projects.
/// </summary>
internal static class AnalysisProjections
{
    extension(ModelContext context)
    {
        /// <summary>
        /// Stages the removal of an entry's projection, if it has one, without saving — so it
        /// lands in the same transaction as the delete that caused it.
        /// </summary>
        /// <remarks>
        /// Derived data follows its source. An <c>AnalyzableEntry</c> is keyed by the id of the
        /// session or context it projects but carries no foreign key to that row, so nothing at
        /// the database level removes it: a deleted entry's summary and vector would go on
        /// answering retrieval queries indefinitely.
        /// </remarks>
        public async Task StageProjectionRemovalAsync(Guid entryId, CancellationToken ct)
        {
            var projection = await context.AnalyzableEntries
                .FirstOrDefaultAsync(entry => entry.EntryId == entryId, ct);

            if (projection is not null) context.AnalyzableEntries.Remove(projection);
        }
    }
}
