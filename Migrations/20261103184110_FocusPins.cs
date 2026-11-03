using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class FocusPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FocusPins",
                columns: table => new
                {
                    FocusPinId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementDataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Terms = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    PinnedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusPins", x => x.FocusPinId);
                    table.ForeignKey(
                        name: "FK_FocusPins_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FocusPins_MovementDatas_MovementDataId",
                        column: x => x.MovementDataId,
                        principalTable: "MovementDatas",
                        principalColumn: "MovementDataID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FocusPins_MovementDataId",
                table: "FocusPins",
                column: "MovementDataId");

            migrationBuilder.CreateIndex(
                name: "IX_FocusPins_UserId",
                table: "FocusPins",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FocusPins");
        }
    }
}
