using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class InjuryFamilies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Families",
                table: "Injuries",
                type: "TEXT",
                nullable: false,
                // "[]", not "": every injury already stored named no family, and an empty string
                // is not a list the JSON conversion can read back.
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Families",
                table: "Injuries");
        }
    }
}
