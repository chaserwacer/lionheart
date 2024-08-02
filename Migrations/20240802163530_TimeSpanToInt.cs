using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class TimeSpanToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AveragePace",
                table: "RunWalkDetails");

            migrationBuilder.DropColumn(
                name: "TimeSpan",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "MileSplits",
                table: "RunWalkDetails",
                newName: "MileSplitsInSeconds");

            migrationBuilder.AddColumn<int>(
                name: "AveragePaceInSeconds",
                table: "RunWalkDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimeInMinutes",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AveragePaceInSeconds",
                table: "RunWalkDetails");

            migrationBuilder.DropColumn(
                name: "TimeInMinutes",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "MileSplitsInSeconds",
                table: "RunWalkDetails",
                newName: "MileSplits");

            migrationBuilder.AddColumn<string>(
                name: "AveragePace",
                table: "RunWalkDetails",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TimeSpan",
                table: "Activities",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
