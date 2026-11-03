using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class RoutinesBodyweightAndMovementSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoutineId",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "AddsToBodyweight",
                table: "MovementDatas",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SetupNotes",
                table: "MovementDatas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BodyweightMeasurements",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyweightMeasurements", x => new { x.UserId, x.Day });
                    table.ForeignKey(
                        name: "FK_BodyweightMeasurements_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Routines",
                columns: table => new
                {
                    RoutineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Movements = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routines", x => x.RoutineId);
                    table.ForeignKey(
                        name: "FK_Routines_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_RoutineId",
                table: "Sessions",
                column: "RoutineId");

            migrationBuilder.CreateIndex(
                name: "IX_Routines_UserId_CreatedAt",
                table: "Routines",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BodyweightMeasurements");

            migrationBuilder.DropTable(
                name: "Routines");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_RoutineId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RoutineId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "AddsToBodyweight",
                table: "MovementDatas");

            migrationBuilder.DropColumn(
                name: "SetupNotes",
                table: "MovementDatas");
        }
    }
}
