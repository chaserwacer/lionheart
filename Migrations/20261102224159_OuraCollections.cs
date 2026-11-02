using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class OuraCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyOuraDatas");

            migrationBuilder.CreateTable(
                name: "OuraDailyActivity",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraDailyActivity", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraDailyActivity_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraDailyReadiness",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraDailyReadiness", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraDailyReadiness_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraDailyResilience",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraDailyResilience", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraDailyResilience_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraDailySleep",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraDailySleep", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraDailySleep_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraDailySpo2",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraDailySpo2", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraDailySpo2_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraDailyStress",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraDailyStress", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraDailyStress_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraSessions",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraSessions", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraSessions_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraSleepPeriods",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraSleepPeriods", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraSleepPeriods_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraTags",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraTags", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraTags_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OuraWorkouts",
                columns: table => new
                {
                    OuraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Reading = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OuraWorkouts", x => x.OuraId);
                    table.ForeignKey(
                        name: "FK_OuraWorkouts_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OuraDailyActivity_UserId_Day",
                table: "OuraDailyActivity",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraDailyReadiness_UserId_Day",
                table: "OuraDailyReadiness",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraDailyResilience_UserId_Day",
                table: "OuraDailyResilience",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraDailySleep_UserId_Day",
                table: "OuraDailySleep",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraDailySpo2_UserId_Day",
                table: "OuraDailySpo2",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraDailyStress_UserId_Day",
                table: "OuraDailyStress",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraSessions_UserId_Day",
                table: "OuraSessions",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraSessions_UserId_StartTime",
                table: "OuraSessions",
                columns: new[] { "UserId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraSleepPeriods_UserId_Day",
                table: "OuraSleepPeriods",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraSleepPeriods_UserId_StartTime",
                table: "OuraSleepPeriods",
                columns: new[] { "UserId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraTags_UserId_Day",
                table: "OuraTags",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraTags_UserId_StartTime",
                table: "OuraTags",
                columns: new[] { "UserId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraWorkouts_UserId_Day",
                table: "OuraWorkouts",
                columns: new[] { "UserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_OuraWorkouts_UserId_StartTime",
                table: "OuraWorkouts",
                columns: new[] { "UserId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OuraDailyActivity");

            migrationBuilder.DropTable(
                name: "OuraDailyReadiness");

            migrationBuilder.DropTable(
                name: "OuraDailyResilience");

            migrationBuilder.DropTable(
                name: "OuraDailySleep");

            migrationBuilder.DropTable(
                name: "OuraDailySpo2");

            migrationBuilder.DropTable(
                name: "OuraDailyStress");

            migrationBuilder.DropTable(
                name: "OuraSessions");

            migrationBuilder.DropTable(
                name: "OuraSleepPeriods");

            migrationBuilder.DropTable(
                name: "OuraTags");

            migrationBuilder.DropTable(
                name: "OuraWorkouts");

            migrationBuilder.CreateTable(
                name: "DailyOuraDatas",
                columns: table => new
                {
                    ObjectID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityJson = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ReadinessJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResilienceJson = table.Column<string>(type: "TEXT", nullable: false),
                    SleepJson = table.Column<string>(type: "TEXT", nullable: false),
                    SyncDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityData_ActiveCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_ActivityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_MeetDailyTargets = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_MoveEveryHour = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_RecoveryTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_StayActive = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_Steps = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TargetCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TotalCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TrainingFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TrainingVolume = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_ActivityBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_BodyTemperature = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_HrvBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_PreviousDayActivity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_PreviousNight = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_ReadinessScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_RecoveryIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_RestingHeartRate = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_SleepBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_TemperatureDeviation = table.Column<double>(type: "REAL", nullable: false),
                    ResilienceData_DaytimeRecovery = table.Column<double>(type: "REAL", nullable: false),
                    ResilienceData_ResilienceLevel = table.Column<string>(type: "TEXT", nullable: false),
                    ResilienceData_SleepRecovery = table.Column<double>(type: "REAL", nullable: false),
                    ResilienceData_Stress = table.Column<double>(type: "REAL", nullable: false),
                    SleepData_DeepSleep = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Efficiency = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Latency = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_RemSleep = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Restfulness = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_SleepScore = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Timing = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_TotalSleep = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyOuraDatas", x => x.ObjectID);
                    table.ForeignKey(
                        name: "FK_DailyOuraDatas_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyOuraDatas_UserID",
                table: "DailyOuraDatas",
                column: "UserID");
        }
    }
}
