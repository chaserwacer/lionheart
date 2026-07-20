using lionheart.Data;
using lionheart.Model.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// A per-test SQLite in-memory database carrying the full ModelContext schema, seeded with
/// one user (the remodeled tables all FK to LionheartUser). The open connection is the
/// database's lifetime; each CreateContext() sees the same store, so tests rehydrate
/// through a fresh context instead of reading back the change tracker.
/// </summary>
public sealed class RemodelDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public Guid UserId { get; } = Guid.NewGuid();

    public RemodelDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();

        var identity = new IdentityUser("tester@lionheart.local");
        context.Users.Add(identity);
        context.LionheartUsers.Add(new LionheartUser { UserID = UserId, IdentityUser = identity, Name = "Tester" });
        context.SaveChanges();
    }

    public ModelContext CreateContext() =>
        new(new DbContextOptionsBuilder<ModelContext>().UseSqlite(_connection).Options);

    /// <summary>
    /// Raw column read, for asserting on storage representation (sentinels, stamped columns).
    /// A SQL NULL comes back as null rather than DBNull.Value, which is non-null in C# and
    /// would quietly satisfy an Assert.NotNull on a column that is in fact empty.
    /// </summary>
    public object? ExecuteScalar(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value == DBNull.Value ? null : value;
    }

    /// <summary>
    /// EF's SQLite provider writes Guid TEXT columns uppercase, and SQLite compares TEXT
    /// byte-for-byte — a lowercase literal in hand-written SQL silently matches nothing
    /// (or fails a foreign key that is actually satisfied).
    /// </summary>
    public static string SqlGuid(Guid id) => id.ToString().ToUpperInvariant();

    /// <summary>Raw write, for planting rows EF would never produce (corrupt JSON, orphan FKs).</summary>
    public int ExecuteNonQuery(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }

    public DateTime ReadDateTimeColumn(string sql) =>
        DateTime.Parse((string)ExecuteScalar(sql)!, System.Globalization.CultureInfo.InvariantCulture);

    public void Dispose() => _connection.Dispose();
}
