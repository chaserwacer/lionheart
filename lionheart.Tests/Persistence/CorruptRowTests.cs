using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// JSON columns are plain TEXT, so nothing at the database level stops a bad payload getting
/// in — an older writer, a hand-edit, a partial restore. Rows are planted with raw SQL (which
/// bypasses every domain guard) and then read through EF: the contract is that reading throws,
/// never that a half-built entity reaches the caller looking legitimate.
/// </summary>
public sealed class CorruptRowTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private void PlantCompletedSession(string setsJson, string periodJson = """{"$type":"instant","At":"2026-07-18T09:30:00Z"}""")
    {
        _db.ExecuteNonQuery($$"""
            INSERT INTO Sessions
                (TrainingSessionId, UserId, Notes, Provenance, TrainingProgramId, RoutineId, CreatedAt,
                 Lifecycle, Sets, Period, Effort, Decoration, StartTime)
            VALUES
                ('{{RemodelDatabase.SqlGuid(Guid.NewGuid())}}', '{{RemodelDatabase.SqlGuid(_db.UserId)}}', 'planted', 0, '{{RemodelDatabase.SqlGuid(Guid.Empty)}}', '{{RemodelDatabase.SqlGuid(Guid.Empty)}}', '2026-07-18 09:30:00',
                 'completed', '{{setsJson.Replace("'", "''")}}', '{{periodJson.Replace("'", "''")}}',
                 '{"$type":"none"}', '{"$type":"none"}', '2026-07-18 09:30:00');
            """);
    }

    private Exception ReadSessions()
    {
        using var context = _db.CreateContext();
        return Assert.ThrowsAny<Exception>(() => context.Sessions.ToList());
    }

    [Fact]
    public void Unknown_set_discriminator_in_a_stored_row_throws_on_read()
    {
        PlantCompletedSession("""[{"$type":"swimSet","Distance":100}]""");

        ReadSessions();
    }

    [Fact]
    public void Truncated_sets_json_throws_on_read()
    {
        PlantCompletedSession("""[{"$type":"lift","Reps":5,""");

        ReadSessions();
    }

    [Fact]
    public void Set_missing_a_required_member_throws_on_read()
    {
        PlantCompletedSession("""[{"$type":"lift","MovementDataId":"11111111-2222-3333-4444-555555555555","Reps":5,"Weight":225,"Unit":1,"Rpe":8.5}]""");

        ReadSessions();
    }

    [Fact]
    public void Facet_axis_outside_the_closed_enum_throws_on_read()
    {
        PlantCompletedSession("""[{"$type":"freeform","Source":{"$type":"formed"},"Facets":[{"Axis":99,"Value":"nutrition"}]}]""");

        ReadSessions();
    }

    [Fact]
    public void Rpe_outside_its_range_throws_on_read()
    {
        PlantCompletedSession("""[{"$type":"lift","MovementDataId":"11111111-2222-3333-4444-555555555555","Reps":5,"Weight":225,"Unit":1,"Rpe":42,"Source":{"$type":"formed"},"Facets":[]}]""");

        ReadSessions();
    }

    [Fact]
    public void Interval_with_end_before_start_throws_on_read()
    {
        PlantCompletedSession(
            "[]",
            """{"$type":"interval","Start":"2026-07-18T10:00:00Z","End":"2026-07-18T09:00:00Z"}""");

        ReadSessions();
    }

    [Fact]
    public void Empty_string_in_a_json_column_throws_on_read()
    {
        PlantCompletedSession("");

        ReadSessions();
    }

    /// <summary>
    /// A JSON literal null in a column typed as a non-nullable domain member is the case the
    /// converter's own null guard exists for — it must not hand back a null Sets list.
    /// </summary>
    [Fact]
    public void Json_null_in_a_list_column_throws_on_read()
    {
        PlantCompletedSession("null");

        ReadSessions();
    }

    [Fact]
    public void A_valid_row_planted_the_same_way_still_reads_cleanly()
    {
        PlantCompletedSession("""[{"$type":"freeform","Source":{"$type":"formed"},"Facets":[]}]""");

        using var context = _db.CreateContext();
        var session = Assert.IsType<CompletedTrainingSession>(Assert.Single(context.Sessions.ToList()));

        Assert.Single(session.Sets);
        Assert.Equal("planted", session.Notes);
    }

    /// <summary>
    /// An unknown lifecycle discriminator is a TPH concern rather than a JSON one: EF cannot
    /// pick a CLR type for the row at all.
    /// </summary>
    [Fact]
    public void Unknown_lifecycle_discriminator_throws_on_read()
    {
        _db.ExecuteNonQuery($"""
            INSERT INTO Sessions
                (TrainingSessionId, UserId, Notes, Provenance, TrainingProgramId, RoutineId, CreatedAt, Lifecycle)
            VALUES
                ('{RemodelDatabase.SqlGuid(Guid.NewGuid())}', '{RemodelDatabase.SqlGuid(_db.UserId)}', 'planted', 0, '{RemodelDatabase.SqlGuid(Guid.Empty)}', '{RemodelDatabase.SqlGuid(Guid.Empty)}', '2026-07-18 09:30:00', 'abandoned');
            """);

        ReadSessions();
    }

    [Fact]
    public void Corrupt_decoration_on_a_context_throws_on_read()
    {
        _db.ExecuteNonQuery($$"""
            INSERT INTO Contexts (ContextId, UserId, Period, Notes, Decoration, Kind, StartTime)
            VALUES ('{{RemodelDatabase.SqlGuid(Guid.NewGuid())}}', '{{RemodelDatabase.SqlGuid(_db.UserId)}}',
                    '{"$type":"instant","At":"2026-07-18T09:30:00Z"}', 'planted',
                    '{"$type":"halfway"}', 'state', '2026-07-18 09:30:00');
            """);

        using var context = _db.CreateContext();
        Assert.ThrowsAny<Exception>(() => context.Contexts.ToList());
    }

    /// <summary>
    /// The embedding BLOB decoder slices bytes into floats. A length that is not a whole number
    /// of floats is a truncated write; decoding it anyway would hand analytics a vector of the
    /// wrong arity that still compares as a legitimate embedding.
    /// </summary>
    [Fact]
    public void Embedding_blob_of_a_non_multiple_of_four_length_throws_on_read()
    {
        _db.ExecuteNonQuery($$"""
            INSERT INTO AnalyzableEntries
                (EntryId, Kind, UserId, Period, Facets, Valence, Embedding, SummaryText, DecoratorVersion, StartTime)
            VALUES ('{{RemodelDatabase.SqlGuid(Guid.NewGuid())}}', 0, '{{RemodelDatabase.SqlGuid(_db.UserId)}}',
                    '{"$type":"instant","At":"2026-07-18T09:30:00Z"}', '[]', 0,
                    X'0000803F000080', 'planted', 1, '2026-07-18 09:30:00');
            """);

        using var context = _db.CreateContext();
        var error = Assert.ThrowsAny<Exception>(() => context.AnalyzableEntries.ToList());

        Assert.Contains("Embedding BLOB length 7", error.ToString());
    }

    [Fact]
    public void Whole_float_embedding_blobs_still_decode()
    {
        _db.ExecuteNonQuery($$"""
            INSERT INTO AnalyzableEntries
                (EntryId, Kind, UserId, Period, Facets, Valence, Embedding, SummaryText, DecoratorVersion, StartTime)
            VALUES ('{{RemodelDatabase.SqlGuid(Guid.NewGuid())}}', 0, '{{RemodelDatabase.SqlGuid(_db.UserId)}}',
                    '{"$type":"instant","At":"2026-07-18T09:30:00Z"}', '[]', 0,
                    X'0000803F', 'planted', 1, '2026-07-18 09:30:00');
            """);

        using var context = _db.CreateContext();

        Assert.Equal([1f], Assert.Single(context.AnalyzableEntries.ToList()).Embedding);
    }
}
