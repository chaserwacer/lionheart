using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ChatConversationUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatMessageRole",
                table: "ChatMessageItems");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "ChatMessageItems",
                newName: "ChatMessageJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChatMessageJson",
                table: "ChatMessageItems",
                newName: "Content");

            migrationBuilder.AddColumn<int>(
                name: "ChatMessageRole",
                table: "ChatMessageItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
