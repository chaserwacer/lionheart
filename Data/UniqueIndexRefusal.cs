using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Data;

/// <summary>
/// Whether a failed save was one particular unique index refusing a second row — the answer a
/// command turns into a domain outcome rather than letting the exception escape.
/// </summary>
/// <remarks>
/// SQLite names a unique refusal by the columns it covers ("UNIQUE constraint failed: T.A, T.B")
/// rather than by the index, so each index keeps its column list as a constant beside its mapping
/// and the command matches on that. Matching on the error code alone would map every unique index
/// added later onto whichever refusal was written first.
/// </remarks>
internal static class UniqueIndexRefusal
{
    /// <summary>SQLite's SQLITE_CONSTRAINT, which a unique index's refusal raises.</summary>
    private const int SqliteConstraint = 19;

    extension(DbUpdateException exception)
    {
        public bool IsRefusedBy(string indexColumns) =>
            exception.InnerException is SqliteException { SqliteErrorCode: SqliteConstraint } sqlite
            && sqlite.Message.Contains(indexColumns);
    }
}
