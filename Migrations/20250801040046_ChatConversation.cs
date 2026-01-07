using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ChatConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    ChatConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.ChatConversationId);
                    table.ForeignKey(
                        name: "FK_ChatConversations_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessageItems",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatMessageRole = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageItems", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_ChatMessageItems_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserID",
                table: "ChatConversations",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageItems_ChatConversationID",
                table: "ChatMessageItems",
                column: "ChatConversationID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessageItems");

            migrationBuilder.DropTable(
                name: "ChatConversations");
        }
    }
}
