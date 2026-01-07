using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class PRTrackingUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movements_Equipments_MovementModifier_EquipmentID",
                table: "Movements");

            migrationBuilder.DropForeignKey(
                name: "FK_Movements_MovementBases_MovementBaseID",
                table: "Movements");

            migrationBuilder.DropIndex(
                name: "IX_Movements_MovementBaseID",
                table: "Movements");

            migrationBuilder.DropIndex(
                name: "IX_Movements_MovementModifier_EquipmentID",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "MovementBaseID",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "MovementModifier_EquipmentID",
                table: "Movements");

            migrationBuilder.RenameColumn(
                name: "MovementModifier_Name",
                table: "Movements",
                newName: "MovementDataID");

            migrationBuilder.CreateTable(
                name: "MovementModifiers",
                columns: table => new
                {
                    MovementModifierID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementModifiers", x => x.MovementModifierID);
                    table.ForeignKey(
                        name: "FK_MovementModifiers_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovementDatas",
                columns: table => new
                {
                    MovementDataID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    EquipmentID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementModifierID = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementDatas", x => x.MovementDataID);
                    table.ForeignKey(
                        name: "FK_MovementDatas_Equipments_EquipmentID",
                        column: x => x.EquipmentID,
                        principalTable: "Equipments",
                        principalColumn: "EquipmentID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementDatas_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovementDatas_MovementBases_MovementBaseID",
                        column: x => x.MovementBaseID,
                        principalTable: "MovementBases",
                        principalColumn: "MovementBaseID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementDatas_MovementModifiers_MovementModifierID",
                        column: x => x.MovementModifierID,
                        principalTable: "MovementModifiers",
                        principalColumn: "MovementModifierID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalRecords",
                columns: table => new
                {
                    PersonalRecordID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementDataID = table.Column<Guid>(type: "TEXT", nullable: false),
                    PRType = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false),
                    WeightUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    Reps = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PreviousPRCreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousPersonalRecordID = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceLiftSetEntryID = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalRecords", x => x.PersonalRecordID);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_LiftSetEntries_SourceLiftSetEntryID",
                        column: x => x.SourceLiftSetEntryID,
                        principalTable: "LiftSetEntries",
                        principalColumn: "SetEntryID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_MovementDatas_MovementDataID",
                        column: x => x.MovementDataID,
                        principalTable: "MovementDatas",
                        principalColumn: "MovementDataID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_PersonalRecords_PreviousPersonalRecordID",
                        column: x => x.PreviousPersonalRecordID,
                        principalTable: "PersonalRecords",
                        principalColumn: "PersonalRecordID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_MovementDataID",
                table: "Movements",
                column: "MovementDataID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementDatas_EquipmentID",
                table: "MovementDatas",
                column: "EquipmentID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementDatas_MovementBaseID",
                table: "MovementDatas",
                column: "MovementBaseID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementDatas_MovementModifierID",
                table: "MovementDatas",
                column: "MovementModifierID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementDatas_UserID_EquipmentID_MovementBaseID_MovementModifierID",
                table: "MovementDatas",
                columns: new[] { "UserID", "EquipmentID", "MovementBaseID", "MovementModifierID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovementModifiers_UserID",
                table: "MovementModifiers",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_MovementDataID",
                table: "PersonalRecords",
                column: "MovementDataID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_PreviousPersonalRecordID",
                table: "PersonalRecords",
                column: "PreviousPersonalRecordID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_SourceLiftSetEntryID",
                table: "PersonalRecords",
                column: "SourceLiftSetEntryID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_UserID_MovementDataID_PRType_IsActive",
                table: "PersonalRecords",
                columns: new[] { "UserID", "MovementDataID", "PRType", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_MovementDatas_MovementDataID",
                table: "Movements",
                column: "MovementDataID",
                principalTable: "MovementDatas",
                principalColumn: "MovementDataID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movements_MovementDatas_MovementDataID",
                table: "Movements");

            migrationBuilder.DropTable(
                name: "PersonalRecords");

            migrationBuilder.DropTable(
                name: "MovementDatas");

            migrationBuilder.DropTable(
                name: "MovementModifiers");

            migrationBuilder.DropIndex(
                name: "IX_Movements_MovementDataID",
                table: "Movements");

            migrationBuilder.RenameColumn(
                name: "MovementDataID",
                table: "Movements",
                newName: "MovementModifier_Name");

            migrationBuilder.AddColumn<Guid>(
                name: "MovementBaseID",
                table: "Movements",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "MovementModifier_EquipmentID",
                table: "Movements",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Movements_MovementBaseID",
                table: "Movements",
                column: "MovementBaseID");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_MovementModifier_EquipmentID",
                table: "Movements",
                column: "MovementModifier_EquipmentID");

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_Equipments_MovementModifier_EquipmentID",
                table: "Movements",
                column: "MovementModifier_EquipmentID",
                principalTable: "Equipments",
                principalColumn: "EquipmentID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_MovementBases_MovementBaseID",
                table: "Movements",
                column: "MovementBaseID",
                principalTable: "MovementBases",
                principalColumn: "MovementBaseID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
