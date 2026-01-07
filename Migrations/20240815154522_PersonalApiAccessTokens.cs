using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class PersonalApiAccessTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiAccessTokens",
                columns: table => new
                {
                    ObjectID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationName = table.Column<string>(type: "TEXT", nullable: false),
                    PersonalAccessToken = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiAccessTokens", x => x.ObjectID);
                    table.ForeignKey(
                        name: "FK_ApiAccessTokens_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiAccessTokens_UserID",
                table: "ApiAccessTokens",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiAccessTokens");
        }
    }
}
