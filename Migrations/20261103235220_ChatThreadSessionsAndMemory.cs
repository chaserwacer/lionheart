using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ChatThreadSessionsAndMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMemories",
                columns: table => new
                {
                    MemoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    Standing = table.Column<string>(type: "TEXT", nullable: false),
                    Vector = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CurrentVersion = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMemories", x => x.MemoryId);
                    table.ForeignKey(
                        name: "FK_ChatMemories_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatThreadSessions",
                columns: table => new
                {
                    ChatConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Consolidation = table.Column<string>(type: "TEXT", nullable: false),
                    Consolidated = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatThreadSessions", x => x.ChatConversationId);
                    table.ForeignKey(
                        name: "FK_ChatThreadSessions_ChatConversations_ChatConversationId",
                        column: x => x.ChatConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatThreadSessions_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMemories_LineageId",
                table: "ChatMemories",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMemories_UserId_CurrentVersion",
                table: "ChatMemories",
                columns: new[] { "UserId", "CurrentVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreadSessions_Consolidated",
                table: "ChatThreadSessions",
                column: "Consolidated");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreadSessions_UserId_OpenedAt",
                table: "ChatThreadSessions",
                columns: new[] { "UserId", "OpenedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMemories");

            migrationBuilder.DropTable(
                name: "ChatThreadSessions");
        }
    }
}
