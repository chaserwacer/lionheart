using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ReogranizationChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessageItems");

            migrationBuilder.DropTable(
                name: "ChatConversations");

            migrationBuilder.CreateTable(
                name: "ChatConversation",
                columns: table => new
                {
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    LionheartUserUserID = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversation", x => x.ChatConversationID);
                    table.ForeignKey(
                        name: "FK_ChatConversation_LionheartUsers_LionheartUserUserID",
                        column: x => x.LionheartUserUserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "LionheartUserChatMessage",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LionheartUserChatMessage", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_LionheartUserChatMessage_ChatConversation_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversation",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelChatMessage",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelChatMessage", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_ModelChatMessage_ChatConversation_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversation",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelSystemChatMessage",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelSystemChatMessage", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_ModelSystemChatMessage_ChatConversation_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversation",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversation_LionheartUserUserID",
                table: "ChatConversation",
                column: "LionheartUserUserID");

            migrationBuilder.CreateIndex(
                name: "IX_LionheartUserChatMessage_ChatConversationID",
                table: "LionheartUserChatMessage",
                column: "ChatConversationID");

            migrationBuilder.CreateIndex(
                name: "IX_ModelChatMessage_ChatConversationID",
                table: "ModelChatMessage",
                column: "ChatConversationID");

            migrationBuilder.CreateIndex(
                name: "IX_ModelSystemChatMessage_ChatConversationID",
                table: "ModelSystemChatMessage",
                column: "ChatConversationID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LionheartUserChatMessage");

            migrationBuilder.DropTable(
                name: "ModelChatMessage");

            migrationBuilder.DropTable(
                name: "ModelSystemChatMessage");

            migrationBuilder.DropTable(
                name: "ChatConversation");

            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    ChatConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false)
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
                    ChatMessageJson = table.Column<string>(type: "TEXT", nullable: false),
                    ChatMessageRole = table.Column<int>(type: "INTEGER", nullable: false),
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
                name: "IX_ChatMessageItems_ChatConversationID_CreationTime",
                table: "ChatMessageItems",
                columns: new[] { "ChatConversationID", "CreationTime" });
        }
    }
}
