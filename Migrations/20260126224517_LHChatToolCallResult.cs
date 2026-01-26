using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class LHChatToolCallResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatToolCall");

            migrationBuilder.DropColumn(
                name: "ToolCallID",
                table: "ToolChatMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ToolCallID",
                table: "ToolChatMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

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
                name: "IX_ChatToolCall_LHModelChatMessageChatMessageItemID",
                table: "ChatToolCall",
                column: "LHModelChatMessageChatMessageItemID");
        }
    }
}
