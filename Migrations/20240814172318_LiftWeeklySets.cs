using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class LiftWeeklySets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackSets",
                table: "LiftDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BicepSets",
                table: "LiftDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChestSets",
                table: "LiftDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HamstringSets",
                table: "LiftDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuadSets",
                table: "LiftDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShoulderSets",
                table: "LiftDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TricepSets",
                table: "LiftDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackSets",
                table: "LiftDetails");

            migrationBuilder.DropColumn(
                name: "BicepSets",
                table: "LiftDetails");

            migrationBuilder.DropColumn(
                name: "ChestSets",
                table: "LiftDetails");

            migrationBuilder.DropColumn(
                name: "HamstringSets",
                table: "LiftDetails");

            migrationBuilder.DropColumn(
                name: "QuadSets",
                table: "LiftDetails");

            migrationBuilder.DropColumn(
                name: "ShoulderSets",
                table: "LiftDetails");

            migrationBuilder.DropColumn(
                name: "TricepSets",
                table: "LiftDetails");
        }
    }
}
