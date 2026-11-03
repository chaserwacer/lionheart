using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class CascadeAnalyzableEntriesToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_AnalyzableEntries_LionheartUsers_UserId",
                table: "AnalyzableEntries",
                column: "UserId",
                principalTable: "LionheartUsers",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalyzableEntries_LionheartUsers_UserId",
                table: "AnalyzableEntries");
        }
    }
}
