using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <summary>
    /// Remodels PersonalRecords from the pre-remodel weight-and-reps row into the subject,
    /// achievement, standing and lineage the comprehensive record system is built on.
    /// </summary>
    /// <remarks>
    /// Written by hand. The scaffolded version shuffled columns — WeightUnit into Stands, Reps
    /// into KindBracket, CreatedAt into Standing — which keeps the row count and destroys every
    /// value in it. This copies the existing records into the new shape instead, so the athlete's
    /// standing numbers and the chains behind them survive:
    /// <list type="bullet">
    /// <item>a Strength row becomes a <c>heaviestSet</c> achievement, which is the same
    /// comparison under its proper name — most weight in a set, at any rep count;</item>
    /// <item>a Volume row becomes a <c>bestSetVolume</c> achievement, likewise — weight times
    /// reps in one set;</item>
    /// <item><c>IsActive</c> becomes <c>standing</c> or <c>beaten</c>. Nothing migrates as
    /// retracted: the legacy flag could not tell a record the athlete beat from one they
    /// withdrew, and reading every inactive row as withdrawn would libel the progression;</item>
    /// <item>the previous-record pointer and its date become the lineage, so "how long the last
    /// one stood" still answers;</item>
    /// <item><c>CreatedAt</c> becomes <c>AchievedAt</c>. It is the row's write instant rather
    /// than a session's, which is the only date these records ever carried.</item>
    /// </list>
    /// The five new kinds — rep maxes, session volume and the three distance records — are not
    /// derivable from a legacy row and are simply not created by this migration; they appear as
    /// the athlete completes sessions.
    /// </remarks>
    public partial class PersonalRecordComprehensive : Migration
    {
        private const string PreRemodel = "PersonalRecordsPreRemodel";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old table steps aside with its indexes rather than being dropped, so the rows
            // are still there to copy from. SQLite rewrites its self-reference to the new name.
            migrationBuilder.Sql($@"ALTER TABLE ""PersonalRecords"" RENAME TO ""{PreRemodel}"";");

            migrationBuilder.CreateTable(
                name: "PersonalRecords",
                columns: table => new
                {
                    PersonalRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementDataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Achievement = table.Column<string>(type: "TEXT", nullable: false),
                    AchievedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Standing = table.Column<string>(type: "TEXT", nullable: false),
                    Lineage = table.Column<string>(type: "TEXT", nullable: false),
                    KindName = table.Column<string>(type: "TEXT", nullable: false),
                    KindBracket = table.Column<int>(type: "INTEGER", nullable: false),
                    Stands = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalRecords", x => x.PersonalRecordId);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_UserId_Stands",
                table: "PersonalRecords",
                columns: new[] { "UserId", "Stands" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_UserId_KindName_KindBracket",
                table: "PersonalRecords",
                columns: new[] { "UserId", "KindName", "KindBracket" });

            // The JSON is spelled out rather than built by the serializer, because a migration has
            // no application to call. What keeps the two in step is a test asserting that these
            // rows deserialize into the records the serializer would have written.
            migrationBuilder.Sql($@"
                INSERT INTO ""PersonalRecords"" (
                    ""PersonalRecordId"", ""UserId"", ""MovementDataId"", ""Achievement"",
                    ""AchievedAt"", ""Standing"", ""Lineage"", ""KindName"", ""KindBracket"",
                    ""Stands"")
                SELECT
                    legacy.""PersonalRecordID"",
                    legacy.""UserID"",
                    legacy.""MovementDataID"",
                    '{{""$type"":""' || (CASE legacy.""PRType"" WHEN 0 THEN 'heaviestSet' ELSE 'bestSetVolume' END)
                        || '"",""Lift"":{{""Weight"":' || legacy.""Weight""
                        || ',""Unit"":' || legacy.""WeightUnit""
                        || ',""Reps"":' || legacy.""Reps"" || '}}}}',
                    legacy.""CreatedAt"",
                    CASE WHEN legacy.""IsActive"" = 1
                        THEN '{{""$type"":""standing""}}'
                        -- A record stopped standing when the one that took it was achieved, which
                        -- is the earliest row pointing back at it. The legacy chains branch, so
                        -- the earliest successor is the one that actually took it; where nothing
                        -- points back at all, which is a branch the legacy writer abandoned, the
                        -- record's own date is the only bound available.
                        ELSE '{{""$type"":""beaten"",""At"":""' || replace(COALESCE(
                                (SELECT MIN(successor.""CreatedAt"") FROM ""{PreRemodel}"" successor
                                 WHERE successor.""PreviousPersonalRecordID"" = legacy.""PersonalRecordID""),
                                legacy.""CreatedAt""), ' ', 'T') || '""}}'
                    END,
                    CASE WHEN legacy.""PreviousPersonalRecordID"" IS NULL
                        THEN '{{""$type"":""first""}}'
                        ELSE '{{""$type"":""replaced"",""PersonalRecordId"":""'
                             || lower(legacy.""PreviousPersonalRecordID"")
                             || '"",""AchievedAt"":""'
                             || replace(legacy.""PreviousPRCreatedAt"", ' ', 'T') || '""}}'
                    END,
                    CASE legacy.""PRType"" WHEN 0 THEN 'heaviestSet' ELSE 'bestSetVolume' END,
                    0,
                    legacy.""IsActive""
                FROM ""{PreRemodel}"" legacy;");

            migrationBuilder.Sql($@"DROP TABLE ""{PreRemodel}"";");
        }

        /// <summary>
        /// Puts the pre-remodel table back and converts the two kinds that have a legacy shape.
        /// </summary>
        /// <remarks>
        /// Rep maxes, session volumes and the three distance records have nowhere to go in a row
        /// with one weight, one unit and one rep count, so they are dropped rather than flattened
        /// into something that would read as a strength record.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"ALTER TABLE ""PersonalRecords"" RENAME TO ""{PreRemodel}"";");

            migrationBuilder.CreateTable(
                name: "PersonalRecords",
                columns: table => new
                {
                    PersonalRecordID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    MovementDataID = table.Column<Guid>(type: "TEXT", nullable: false),
                    PRType = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousPRCreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousPersonalRecordID = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reps = table.Column<int>(type: "INTEGER", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false),
                    WeightUnit = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalRecords", x => x.PersonalRecordID);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_MovementDatas_MovementDataID",
                        column: x => x.MovementDataID,
                        principalTable: "MovementDatas",
                        principalColumn: "MovementDataID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_PersonalRecords_PreviousPersonalRecordID",
                        column: x => x.PreviousPersonalRecordID,
                        principalTable: "PersonalRecords",
                        principalColumn: "PersonalRecordID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.Sql($@"
                INSERT INTO ""PersonalRecords"" (
                    ""PersonalRecordID"", ""CreatedAt"", ""IsActive"", ""MovementDataID"",
                    ""PRType"", ""PreviousPRCreatedAt"", ""PreviousPersonalRecordID"", ""Reps"",
                    ""UserID"", ""Weight"", ""WeightUnit"")
                SELECT
                    remodelled.""PersonalRecordId"",
                    remodelled.""AchievedAt"",
                    remodelled.""Stands"",
                    remodelled.""MovementDataId"",
                    CASE remodelled.""KindName"" WHEN 'heaviestSet' THEN 0 ELSE 1 END,
                    CASE WHEN json_extract(remodelled.""Lineage"", '$.""$type""') = 'replaced'
                        THEN replace(json_extract(remodelled.""Lineage"", '$.AchievedAt'), 'T', ' ')
                    END,
                    CASE WHEN json_extract(remodelled.""Lineage"", '$.""$type""') = 'replaced'
                        THEN upper(json_extract(remodelled.""Lineage"", '$.PersonalRecordId'))
                    END,
                    json_extract(remodelled.""Achievement"", '$.Lift.Reps'),
                    remodelled.""UserId"",
                    json_extract(remodelled.""Achievement"", '$.Lift.Weight'),
                    json_extract(remodelled.""Achievement"", '$.Lift.Unit')
                FROM ""{PreRemodel}"" remodelled
                WHERE remodelled.""KindName"" IN ('heaviestSet', 'bestSetVolume')
                  AND remodelled.""MovementDataId"" <> '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql($@"DROP TABLE ""{PreRemodel}"";");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_MovementDataID",
                table: "PersonalRecords",
                column: "MovementDataID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_PreviousPersonalRecordID",
                table: "PersonalRecords",
                column: "PreviousPersonalRecordID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_UserID_MovementDataID_PRType_IsActive",
                table: "PersonalRecords",
                columns: new[] { "UserID", "MovementDataID", "PRType", "IsActive" });
        }
    }
}
