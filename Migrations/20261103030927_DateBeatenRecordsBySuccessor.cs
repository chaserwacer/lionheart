using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <summary>
    /// Dates each migrated beaten record by when it stopped standing — the earliest record that
    /// replaced it — rather than by when it was itself set.
    /// </summary>
    /// <remarks>
    /// <c>PersonalRecordComprehensive</c> stamped a beaten record's end with the record's own date,
    /// because that migration had only the row in hand. The successor's date was always available
    /// by following the lineage back, which is what this does. The legacy chains branch, so the
    /// earliest successor is the one that actually took the record; a record nothing points back at
    /// keeps its own date, the only bound available.
    /// <para>
    /// Only rows still carrying the signature of that stamp are touched — beaten, with an end equal
    /// to their own achievement. The detector dates every record it beats by its successor, so a
    /// row written since is never matched, and a migrated record that was later restored and beaten
    /// again already carries its real end and is left alone. That also makes this safe to run
    /// against a fresh database, where the copy it corrects ran moments earlier.
    /// </para>
    /// </remarks>
    public partial class DateBeatenRecordsBySuccessor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""PersonalRecords""
                SET ""Standing"" = '{""$type"":""beaten"",""At"":""' || replace(COALESCE(
                        (SELECT MIN(successor.""AchievedAt"")
                         FROM ""PersonalRecords"" successor
                         WHERE json_extract(successor.""Lineage"", '$.""$type""') = 'replaced'
                           AND upper(json_extract(successor.""Lineage"", '$.PersonalRecordId'))
                               = upper(""PersonalRecords"".""PersonalRecordId"")),
                        ""PersonalRecords"".""AchievedAt""), ' ', 'T') || '""}'
                WHERE ""Stands"" = 0
                  AND json_extract(""Standing"", '$.""$type""') = 'beaten'
                  AND json_extract(""Standing"", '$.At') = replace(""AchievedAt"", ' ', 'T');");
        }

        /// <summary>Nothing to undo: the end date this corrects was never a fact.</summary>
        /// <remarks>
        /// Writing each record's own date back would be indistinguishable from damaging the rows the
        /// detector has dated correctly since, and nothing reads the value a reverted schema would
        /// need.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
