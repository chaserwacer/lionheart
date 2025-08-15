using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class InjuryPropertyUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsResolved",
                table: "Injuries",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Injuries",
                newName: "Notes");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Injuries",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Injuries");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "Injuries",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Injuries",
                newName: "IsResolved");
        }
    }
}
