using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ChatConversationOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LionheartUserUserID = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.ChatConversationID);
                    table.ForeignKey(
                        name: "FK_ChatConversations_LionheartUsers_LionheartUserUserID",
                        column: x => x.LionheartUserUserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "ModelChatMessages",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelChatMessages", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_ModelChatMessages_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemChatMessages",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemChatMessages", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_SystemChatMessages_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolChatMessages",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolCallID = table.Column<string>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolChatMessages", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_ToolChatMessages_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserChatMessages",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChatMessages", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_UserChatMessages_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatToolCall",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    LHModelChatMessageChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatToolCall", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatToolCall_ModelChatMessages_LHModelChatMessageChatMessageItemID",
                        column: x => x.LHModelChatMessageChatMessageItemID,
                        principalTable: "ModelChatMessages",
                        principalColumn: "ChatMessageItemID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_LionheartUserUserID",
                table: "ChatConversations",
                column: "LionheartUserUserID");

            migrationBuilder.CreateIndex(
                name: "IX_ChatToolCall_LHModelChatMessageChatMessageItemID",
                table: "ChatToolCall",
                column: "LHModelChatMessageChatMessageItemID");

            migrationBuilder.CreateIndex(
                name: "IX_ModelChatMessages_ChatConversationID",
                table: "ModelChatMessages",
                column: "ChatConversationID");

            migrationBuilder.CreateIndex(
                name: "IX_SystemChatMessages_ChatConversationID",
                table: "SystemChatMessages",
                column: "ChatConversationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolChatMessages_ChatConversationID",
                table: "ToolChatMessages",
                column: "ChatConversationID");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatMessages_ChatConversationID",
                table: "UserChatMessages",
                column: "ChatConversationID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatToolCall");

            migrationBuilder.DropTable(
                name: "SystemChatMessages");

            migrationBuilder.DropTable(
                name: "ToolChatMessages");

            migrationBuilder.DropTable(
                name: "UserChatMessages");

            migrationBuilder.DropTable(
                name: "ModelChatMessages");

            migrationBuilder.DropTable(
                name: "ChatConversations");

            migrationBuilder.CreateTable(
                name: "ChatConversation",
                columns: table => new
                {
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LionheartUserUserID = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false)
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
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false)
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
    }
}
