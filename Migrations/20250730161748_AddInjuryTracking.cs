using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class AddInjuryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MovementModifier_Duration",
                table: "Movements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Injuries",
                columns: table => new
                {
                    InjuryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    InjuryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Injuries", x => x.InjuryID);
                    table.ForeignKey(
                        name: "FK_Injuries_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InjuryEvents",
                columns: table => new
                {
                    InjuryEventID = table.Column<Guid>(type: "TEXT", nullable: false),
                    InjuryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingSessionID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    PainLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    InjuryType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InjuryEvents", x => x.InjuryEventID);
                    table.ForeignKey(
                        name: "FK_InjuryEvents_Injuries_InjuryID",
                        column: x => x.InjuryID,
                        principalTable: "Injuries",
                        principalColumn: "InjuryID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Injuries_UserID",
                table: "Injuries",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_InjuryEvents_InjuryID",
                table: "InjuryEvents",
                column: "InjuryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InjuryEvents");

            migrationBuilder.DropTable(
                name: "Injuries");

            migrationBuilder.AlterColumn<int>(
                name: "MovementModifier_Duration",
                table: "Movements",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }


    }
    
}
