using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class HealthWorkoutMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Metrics",
                table: "WorkoutRecordings",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.CreateTable(
                name: "DeletedHealthWorkouts",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkoutId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedHealthWorkouts", x => new { x.UserId, x.WorkoutId });
                    table.ForeignKey(
                        name: "FK_DeletedHealthWorkouts_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedHealthWorkouts");

            migrationBuilder.DropColumn(
                name: "Metrics",
                table: "WorkoutRecordings");
        }
    }
}
