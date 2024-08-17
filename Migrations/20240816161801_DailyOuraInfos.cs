using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class DailyOuraInfos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyOuraInfos",
                columns: table => new
                {
                    ObjectID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SyncDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ResilienceData_SleepRecovery = table.Column<int>(type: "INTEGER", nullable: false),
                    ResilienceData_DaytimeRecovery = table.Column<int>(type: "INTEGER", nullable: false),
                    ResilienceData_Stress = table.Column<int>(type: "INTEGER", nullable: false),
                    ResilienceData_ResilienceLevel = table.Column<string>(type: "TEXT", nullable: false),
                    ActivityData_ActivityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_Steps = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_ActiveCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TotalCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TargetCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_MeetDailyTargets = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_MoveEveryHour = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_RecoveryTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_StayActive = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TrainingFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TrainingVolume = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_SleepScore = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_DeepSleep = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Efficiency = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Latency = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_RemSleep = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Restfulness = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Timing = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_TotalSleep = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_ReadinessScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_TemperatureDeviation = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_ActivityBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_BodyTemperature = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_HrvBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_PreviousDayActivity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_PreviousNight = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_RecoveryIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_RestingHeartRate = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_SleepBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResilienceJson = table.Column<string>(type: "TEXT", nullable: false),
                    SleepJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReadinessJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyOuraInfos", x => x.ObjectID);
                    table.ForeignKey(
                        name: "FK_DailyOuraInfos_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyOuraInfos_UserID",
                table: "DailyOuraInfos",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyOuraInfos");
        }
    }
}
