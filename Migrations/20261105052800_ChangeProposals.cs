using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ChangeProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeProposals",
                columns: table => new
                {
                    ChangeProposalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Change = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeProposals", x => x.ChangeProposalId);
                    table.ForeignKey(
                        name: "FK_ChangeProposals_ChatConversations_ChatConversationId",
                        column: x => x.ChatConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeProposals_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeProposals_ChatConversationId",
                table: "ChangeProposals",
                column: "ChatConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeProposals_UserId_ProposedAt",
                table: "ChangeProposals",
                columns: new[] { "UserId", "ProposedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeProposals");
        }
    }
}
