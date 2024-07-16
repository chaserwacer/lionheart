using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class OverallWellnessScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WrittenDescription",
                table: "WellnessStates");

            migrationBuilder.AddColumn<int>(
                name: "OverallScore",
                table: "WellnessStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverallScore",
                table: "WellnessStates");

            migrationBuilder.AddColumn<string>(
                name: "WrittenDescription",
                table: "WellnessStates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
