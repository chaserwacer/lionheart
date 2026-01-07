using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class Equipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MovementModifier_Equipment",
                table: "Movements",
                newName: "MovementModifier_EquipmentID");

            migrationBuilder.CreateTable(
                name: "Equipments",
                columns: table => new
                {
                    EquipmentID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipments", x => x.EquipmentID);
                    table.ForeignKey(
                        name: "FK_Equipments_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_MovementModifier_EquipmentID",
                table: "Movements",
                column: "MovementModifier_EquipmentID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementBases_UserID",
                table: "MovementBases",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Equipments_UserID",
                table: "Equipments",
                column: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_MovementBases_LionheartUsers_UserID",
                table: "MovementBases",
                column: "UserID",
                principalTable: "LionheartUsers",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_Equipments_MovementModifier_EquipmentID",
                table: "Movements",
                column: "MovementModifier_EquipmentID",
                principalTable: "Equipments",
                principalColumn: "EquipmentID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovementBases_LionheartUsers_UserID",
                table: "MovementBases");

            migrationBuilder.DropForeignKey(
                name: "FK_Movements_Equipments_MovementModifier_EquipmentID",
                table: "Movements");

            migrationBuilder.DropTable(
                name: "Equipments");

            migrationBuilder.DropIndex(
                name: "IX_Movements_MovementModifier_EquipmentID",
                table: "Movements");

            migrationBuilder.DropIndex(
                name: "IX_MovementBases_UserID",
                table: "MovementBases");

            migrationBuilder.RenameColumn(
                name: "MovementModifier_EquipmentID",
                table: "Movements",
                newName: "MovementModifier_Equipment");
        }
    }
}
