using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ChatMessageDiscriminator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessageItems_ChatConversationID",
                table: "ChatMessageItems");

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "ChatMessageItems",
                type: "TEXT",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageItems_ChatConversationID_CreationTime",
                table: "ChatMessageItems",
                columns: new[] { "ChatConversationID", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessageItems_ChatConversationID_CreationTime",
                table: "ChatMessageItems");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "ChatMessageItems");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageItems_ChatConversationID",
                table: "ChatMessageItems",
                column: "ChatConversationID");
        }
    }
}
