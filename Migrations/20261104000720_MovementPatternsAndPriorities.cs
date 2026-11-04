using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class MovementPatternsAndPriorities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The muscle-group rows hung each group off a single base, so no two movements could
            // share one; what a movement trains is now its classification, below.
            migrationBuilder.DropTable(
                name: "MuscleGroups");

            // Every existing base starts unstated, so the shipped map answers for it.
            migrationBuilder.AddColumn<string>(
                name: "Classification",
                table: "MovementBases",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"$type\":\"unstated\"}");

            // Every existing pin was made for emphasis, before priorities existed.
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "FocusPins",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"$type\":\"emphasis\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Classification",
                table: "MovementBases");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "FocusPins");

            migrationBuilder.CreateTable(
                name: "MuscleGroups",
                columns: table => new
                {
                    MuscleGroupID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuscleGroups", x => x.MuscleGroupID);
                    table.ForeignKey(
                        name: "FK_MuscleGroups_MovementBases_MovementBaseID",
                        column: x => x.MovementBaseID,
                        principalTable: "MovementBases",
                        principalColumn: "MovementBaseID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuscleGroups_MovementBaseID",
                table: "MuscleGroups",
                column: "MovementBaseID");
        }
    }
}
