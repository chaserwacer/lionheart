using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class DataRemodelCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalyzableEntries",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Period = table.Column<string>(type: "TEXT", nullable: false),
                    Facets = table.Column<string>(type: "TEXT", nullable: false),
                    Valence = table.Column<int>(type: "INTEGER", nullable: false),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SummaryText = table.Column<string>(type: "TEXT", nullable: false),
                    DecoratorVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyzableEntries", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "Contexts",
                columns: table => new
                {
                    ContextId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Period = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Decoration = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Effort = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contexts", x => x.ContextId);
                    table.ForeignKey(
                        name: "FK_Contexts_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacetValues",
                columns: table => new
                {
                    Axis = table.Column<int>(type: "INTEGER", nullable: false),
                    Canonical = table.Column<string>(type: "TEXT", nullable: false),
                    Aliases = table.Column<string>(type: "TEXT", nullable: false),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacetValues", x => new { x.Axis, x.Canonical });
                });

            migrationBuilder.CreateTable(
                name: "Programs",
                columns: table => new
                {
                    TrainingProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    Lifecycle = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Programs", x => x.TrainingProgramId);
                    table.ForeignKey(
                        name: "FK_Programs_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    TrainingSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Provenance = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainingProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Lifecycle = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sets = table.Column<string>(type: "TEXT", nullable: true),
                    Period = table.Column<string>(type: "TEXT", nullable: true),
                    Effort = table.Column<string>(type: "TEXT", nullable: true),
                    Decoration = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlannedFor = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Prescriptions = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.TrainingSessionId);
                    table.ForeignKey(
                        name: "FK_Sessions_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyzableEntries_Kind",
                table: "AnalyzableEntries",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyzableEntries_UserId_StartTime",
                table: "AnalyzableEntries",
                columns: new[] { "UserId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Contexts_UserId_StartTime",
                table: "Contexts",
                columns: new[] { "UserId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Programs_UserId",
                table: "Programs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StartTime",
                table: "Sessions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TrainingProgramId",
                table: "Sessions",
                column: "TrainingProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                table: "Sessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyzableEntries");

            migrationBuilder.DropTable(
                name: "Contexts");

            migrationBuilder.DropTable(
                name: "FacetValues");

            migrationBuilder.DropTable(
                name: "Programs");

            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
