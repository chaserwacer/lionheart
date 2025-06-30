using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class MovementWeightUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeightUnit",
                table: "SetEntries");

            migrationBuilder.AddColumn<int>(
                name: "WeightUnit",
                table: "Movements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeightUnit",
                table: "Movements");

            migrationBuilder.AddColumn<int>(
                name: "WeightUnit",
                table: "SetEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
