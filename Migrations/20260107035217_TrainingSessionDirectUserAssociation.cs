using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class TrainingSessionDirectUserAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextTrainingSessionDate",
                table: "TrainingPrograms");

            migrationBuilder.AddColumn<Guid>(
                name: "UserID",
                table: "TrainingSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_UserID",
                table: "TrainingSessions",
                column: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingSessions_LionheartUsers_UserID",
                table: "TrainingSessions",
                column: "UserID",
                principalTable: "LionheartUsers",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainingSessions_LionheartUsers_UserID",
                table: "TrainingSessions");

            migrationBuilder.DropIndex(
                name: "IX_TrainingSessions_UserID",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "UserID",
                table: "TrainingSessions");

            migrationBuilder.AddColumn<DateOnly>(
                name: "NextTrainingSessionDate",
                table: "TrainingPrograms",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));
        }
    }
}
