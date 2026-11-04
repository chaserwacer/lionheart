using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class AppleHealthWorkouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HealthWorkoutId",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"$type\":\"athleteLog\"}");

            migrationBuilder.CreateTable(
                name: "WorkoutRecordings",
                columns: table => new
                {
                    TrainingSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Route = table.Column<string>(type: "TEXT", nullable: false),
                    HeartRate = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutRecordings", x => x.TrainingSessionId);
                    table.ForeignKey(
                        name: "FK_WorkoutRecordings_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId_HealthWorkoutId",
                table: "Sessions",
                columns: new[] { "UserId", "HealthWorkoutId" },
                unique: true,
                filter: "\"HealthWorkoutId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutRecordings_UserId",
                table: "WorkoutRecordings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkoutRecordings");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_UserId_HealthWorkoutId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "HealthWorkoutId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Sessions");
        }
    }
}
