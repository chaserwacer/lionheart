using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ProfileCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityUserId",
                table: "LionheartUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LionheartUsers_IdentityUserId",
                table: "LionheartUsers",
                column: "IdentityUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_LionheartUsers_Users_IdentityUserId",
                table: "LionheartUsers",
                column: "IdentityUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LionheartUsers_Users_IdentityUserId",
                table: "LionheartUsers");

            migrationBuilder.DropIndex(
                name: "IX_LionheartUsers_IdentityUserId",
                table: "LionheartUsers");

            migrationBuilder.DropColumn(
                name: "IdentityUserId",
                table: "LionheartUsers");
        }
    }
}
