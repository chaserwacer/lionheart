using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class LionheartTrainingSetEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainingSessions_TrainingPrograms_TrainingProgramID",
                table: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "DailyOuraInfos");

            migrationBuilder.DropTable(
                name: "LiftDetails");

            migrationBuilder.DropTable(
                name: "RideDetails");

            migrationBuilder.DropTable(
                name: "RunWalkDetails");

            // migrationBuilder.DropTable(
            //     name: "SetEntries");

            migrationBuilder.DropColumn(
                name: "MovementModifier_Duration",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "WeightUnit",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "AccumulatedFatigue",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "DifficultyRating",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "EngagementRating",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ExternalVariablesRating",
                table: "Activities");

            migrationBuilder.AlterColumn<Guid>(
                name: "TrainingProgramID",
                table: "TrainingSessions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            // migrationBuilder.AddColumn<string>(
            //     name: "Description",
            //     table: "MovementBases",
            //     type: "TEXT",
            //     nullable: false,
            //     defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MovementIDs",
                table: "InjuryEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ActivityPerceivedEffortRatings",
                columns: table => new
                {
                    ActivityID = table.Column<Guid>(type: "TEXT", nullable: false),
                    PerceivedEffortRatings_AccumulatedFatigue = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_DifficultyRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_EngagementRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_ExternalVariablesRating = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityPerceivedEffortRatings", x => x.ActivityID);
                    table.ForeignKey(
                        name: "FK_ActivityPerceivedEffortRatings_Activities_ActivityID",
                        column: x => x.ActivityID,
                        principalTable: "Activities",
                        principalColumn: "ActivityID");
                });

            migrationBuilder.CreateTable(
                name: "DailyOuraDatas",
                columns: table => new
                {
                    ObjectID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SyncDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ResilienceData_SleepRecovery = table.Column<double>(type: "REAL", nullable: false),
                    ResilienceData_DaytimeRecovery = table.Column<double>(type: "REAL", nullable: false),
                    ResilienceData_Stress = table.Column<double>(type: "REAL", nullable: false),
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
                    ReadinessData_TemperatureDeviation = table.Column<double>(type: "REAL", nullable: false),
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
                    table.PrimaryKey("PK_DailyOuraDatas", x => x.ObjectID);
                    table.ForeignKey(
                        name: "FK_DailyOuraDatas_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            // migrationBuilder.CreateTable(
            //     name: "DTSetEntries",
            //     columns: table => new
            //     {
            //         SetEntryID = table.Column<Guid>(type: "TEXT", nullable: false),
            //         MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
            //         RecommendedDistance = table.Column<double>(type: "REAL", nullable: false),
            //         ActualDistance = table.Column<double>(type: "REAL", nullable: false),
            //         IntervalDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
            //         TargetPace = table.Column<TimeSpan>(type: "TEXT", nullable: false),
            //         ActualPace = table.Column<TimeSpan>(type: "TEXT", nullable: false),
            //         RecommendedDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
            //         ActualDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
            //         RecommendedRest = table.Column<TimeSpan>(type: "TEXT", nullable: false),
            //         ActualRest = table.Column<TimeSpan>(type: "TEXT", nullable: false),
            //         IntervalType = table.Column<int>(type: "INTEGER", nullable: false),
            //         DistanceUnit = table.Column<int>(type: "INTEGER", nullable: false),
            //         ActualRPE = table.Column<double>(type: "REAL", nullable: false)
            //     },
            //     constraints: table =>
            //     {
            //         table.PrimaryKey("PK_DTSetEntries", x => x.SetEntryID);
            //         table.ForeignKey(
            //             name: "FK_DTSetEntries_Movements_MovementID",
            //             column: x => x.MovementID,
            //             principalTable: "Movements",
            //             principalColumn: "MovementID",
            //             onDelete: ReferentialAction.Cascade);
            //     });

            // migrationBuilder.CreateTable(
            //     name: "LiftSetEntries",
            //     columns: table => new
            //     {
            //         SetEntryID = table.Column<Guid>(type: "TEXT", nullable: false),
            //         MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
            //         RecommendedReps = table.Column<int>(type: "INTEGER", nullable: false),
            //         RecommendedWeight = table.Column<double>(type: "REAL", nullable: false),
            //         RecommendedRPE = table.Column<double>(type: "REAL", nullable: false),
            //         ActualReps = table.Column<int>(type: "INTEGER", nullable: false),
            //         ActualWeight = table.Column<double>(type: "REAL", nullable: false),
            //         ActualRPE = table.Column<double>(type: "REAL", nullable: false),
            //         WeightUnit = table.Column<int>(type: "INTEGER", nullable: false)
            //     },
            //     constraints: table =>
            //     {
            //         table.PrimaryKey("PK_LiftSetEntries", x => x.SetEntryID);
            //         table.ForeignKey(
            //             name: "FK_LiftSetEntries_Movements_MovementID",
            //             column: x => x.MovementID,
            //             principalTable: "Movements",
            //             principalColumn: "MovementID",
            //             onDelete: ReferentialAction.Cascade);
            //     });

            migrationBuilder.CreateTable(
                name: "MuscleGroups",
                columns: table => new
                {
                    MuscleGroupID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuscleGroups", x => x.MuscleGroupID);
                });

            migrationBuilder.CreateTable(
                name: "TrainedMuscle",
                columns: table => new
                {
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    MuscleGroupID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContributionPercentage = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainedMuscle", x => new { x.MovementBaseID, x.Id });
                    table.ForeignKey(
                        name: "FK_TrainedMuscle_MovementBases_MovementBaseID",
                        column: x => x.MovementBaseID,
                        principalTable: "MovementBases",
                        principalColumn: "MovementBaseID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessionPerceivedEffortRatings",
                columns: table => new
                {
                    TrainingSessionID = table.Column<Guid>(type: "TEXT", nullable: false),
                    PerceivedEffortRatings_AccumulatedFatigue = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_DifficultyRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_EngagementRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_ExternalVariablesRating = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessionPerceivedEffortRatings", x => x.TrainingSessionID);
                    table.ForeignKey(
                        name: "FK_TrainingSessionPerceivedEffortRatings_TrainingSessions_TrainingSessionID",
                        column: x => x.TrainingSessionID,
                        principalTable: "TrainingSessions",
                        principalColumn: "TrainingSessionID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyOuraDatas_UserID",
                table: "DailyOuraDatas",
                column: "UserID");

            // migrationBuilder.CreateIndex(
            //     name: "IX_DTSetEntries_MovementID",
            //     table: "DTSetEntries",
            //     column: "MovementID");

            // migrationBuilder.CreateIndex(
            //     name: "IX_LiftSetEntries_MovementID",
            //     table: "LiftSetEntries",
            //     column: "MovementID");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingSessions_TrainingPrograms_TrainingProgramID",
                table: "TrainingSessions",
                column: "TrainingProgramID",
                principalTable: "TrainingPrograms",
                principalColumn: "TrainingProgramID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainingSessions_TrainingPrograms_TrainingProgramID",
                table: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "ActivityPerceivedEffortRatings");

            migrationBuilder.DropTable(
                name: "DailyOuraDatas");

            migrationBuilder.DropTable(
                name: "DTSetEntries");

            migrationBuilder.DropTable(
                name: "LiftSetEntries");

            migrationBuilder.DropTable(
                name: "MuscleGroups");

            migrationBuilder.DropTable(
                name: "TrainedMuscle");

            migrationBuilder.DropTable(
                name: "TrainingSessionPerceivedEffortRatings");

            // migrationBuilder.DropColumn(
            //     name: "Description",
            //     table: "MovementBases");

            migrationBuilder.DropColumn(
                name: "MovementIDs",
                table: "InjuryEvents");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "Equipments");

            migrationBuilder.AlterColumn<Guid>(
                name: "TrainingProgramID",
                table: "TrainingSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MovementModifier_Duration",
                table: "Movements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeightUnit",
                table: "Movements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AccumulatedFatigue",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DifficultyRating",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EngagementRating",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExternalVariablesRating",
                table: "Activities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DailyOuraInfos",
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
                    table.PrimaryKey("PK_DailyOuraInfos", x => x.ObjectID);
                    table.ForeignKey(
                        name: "FK_DailyOuraInfos_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiftDetails",
                columns: table => new
                {
                    ActivityID = table.Column<Guid>(type: "TEXT", nullable: false),
                    BackSets = table.Column<int>(type: "INTEGER", nullable: false),
                    BicepSets = table.Column<int>(type: "INTEGER", nullable: false),
                    ChestSets = table.Column<int>(type: "INTEGER", nullable: false),
                    HamstringSets = table.Column<int>(type: "INTEGER", nullable: false),
                    LiftFocus = table.Column<string>(type: "TEXT", nullable: false),
                    LiftType = table.Column<string>(type: "TEXT", nullable: false),
                    QuadSets = table.Column<int>(type: "INTEGER", nullable: false),
                    ShoulderSets = table.Column<int>(type: "INTEGER", nullable: false),
                    Tonnage = table.Column<int>(type: "INTEGER", nullable: false),
                    TricepSets = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiftDetails", x => x.ActivityID);
                    table.ForeignKey(
                        name: "FK_LiftDetails_Activities_ActivityID",
                        column: x => x.ActivityID,
                        principalTable: "Activities",
                        principalColumn: "ActivityID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RideDetails",
                columns: table => new
                {
                    ActivityID = table.Column<Guid>(type: "TEXT", nullable: false),
                    AveragePower = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageSpeed = table.Column<double>(type: "REAL", nullable: false),
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    ElevationGain = table.Column<int>(type: "INTEGER", nullable: false),
                    RideType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideDetails", x => x.ActivityID);
                    table.ForeignKey(
                        name: "FK_RideDetails_Activities_ActivityID",
                        column: x => x.ActivityID,
                        principalTable: "Activities",
                        principalColumn: "ActivityID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunWalkDetails",
                columns: table => new
                {
                    ActivityID = table.Column<Guid>(type: "TEXT", nullable: false),
                    AveragePaceInSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    ElevationGain = table.Column<int>(type: "INTEGER", nullable: false),
                    MileSplitsInSeconds = table.Column<string>(type: "TEXT", nullable: false),
                    RunType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunWalkDetails", x => x.ActivityID);
                    table.ForeignKey(
                        name: "FK_RunWalkDetails_Activities_ActivityID",
                        column: x => x.ActivityID,
                        principalTable: "Activities",
                        principalColumn: "ActivityID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetEntries",
                columns: table => new
                {
                    SetEntryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActualRPE = table.Column<double>(type: "REAL", nullable: false),
                    ActualReps = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualWeight = table.Column<double>(type: "REAL", nullable: false),
                    RecommendedRPE = table.Column<double>(type: "REAL", nullable: false),
                    RecommendedReps = table.Column<int>(type: "INTEGER", nullable: false),
                    RecommendedWeight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetEntries", x => x.SetEntryID);
                    table.ForeignKey(
                        name: "FK_SetEntries_Movements_MovementID",
                        column: x => x.MovementID,
                        principalTable: "Movements",
                        principalColumn: "MovementID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyOuraInfos_UserID",
                table: "DailyOuraInfos",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_SetEntries_MovementID",
                table: "SetEntries",
                column: "MovementID");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingSessions_TrainingPrograms_TrainingProgramID",
                table: "TrainingSessions",
                column: "TrainingProgramID",
                principalTable: "TrainingPrograms",
                principalColumn: "TrainingProgramID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
