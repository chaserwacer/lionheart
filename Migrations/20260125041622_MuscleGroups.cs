using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class MuscleGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainedMuscle");

            migrationBuilder.AddColumn<Guid>(
                name: "MovementBaseID",
                table: "MuscleGroups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuscleGroups_MovementBaseID",
                table: "MuscleGroups",
                column: "MovementBaseID");

            migrationBuilder.AddForeignKey(
                name: "FK_MuscleGroups_MovementBases_MovementBaseID",
                table: "MuscleGroups",
                column: "MovementBaseID",
                principalTable: "MovementBases",
                principalColumn: "MovementBaseID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MuscleGroups_MovementBases_MovementBaseID",
                table: "MuscleGroups");

            migrationBuilder.DropIndex(
                name: "IX_MuscleGroups_MovementBaseID",
                table: "MuscleGroups");

            migrationBuilder.DropColumn(
                name: "MovementBaseID",
                table: "MuscleGroups");

            migrationBuilder.CreateTable(
                name: "TrainedMuscle",
                columns: table => new
                {
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ContributionPercentage = table.Column<double>(type: "REAL", nullable: false),
                    MuscleGroupID = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainedMuscle", x => new { x.MovementBaseID, x.Id });
                    table.ForeignKey(
                        name: "FK_TrainedMuscle_MovementBases_MovementBaseID",
                        column: x => x.MovementBaseID,
                        principalTable: "MovementBases",
                        principalColumn: "MovementBaseID",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
