using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyTrainingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonalRecords_LiftSetEntries_SourceLiftSetEntryID",
                table: "PersonalRecords");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "DTSetEntries");

            migrationBuilder.DropTable(
                name: "LiftSetEntries");

            migrationBuilder.DropTable(
                name: "Movements");

            migrationBuilder.DropTable(
                name: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "TrainingPrograms");

            migrationBuilder.DropIndex(
                name: "IX_PersonalRecords_SourceLiftSetEntryID",
                table: "PersonalRecords");

            migrationBuilder.DropColumn(
                name: "SourceLiftSetEntryID",
                table: "PersonalRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceLiftSetEntryID",
                table: "PersonalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    ActivityID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaloriesBurned = table.Column<int>(type: "INTEGER", nullable: false),
                    DateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TimeInMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserSummary = table.Column<string>(type: "TEXT", nullable: false),
                    PerceivedEffortRatings_AccumulatedFatigue = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_DifficultyRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_EngagementRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_ExternalVariablesRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.ActivityID);
                    table.ForeignKey(
                        name: "FK_Activities_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingPrograms",
                columns: table => new
                {
                    TrainingProgramID = table.Column<Guid>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPrograms", x => x.TrainingProgramID);
                    table.ForeignKey(
                        name: "FK_TrainingPrograms_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessions",
                columns: table => new
                {
                    TrainingSessionID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingProgramID = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    PerceivedEffortRatings_AccumulatedFatigue = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_DifficultyRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_EngagementRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_ExternalVariablesRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.TrainingSessionID);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_TrainingPrograms_TrainingProgramID",
                        column: x => x.TrainingProgramID,
                        principalTable: "TrainingPrograms",
                        principalColumn: "TrainingProgramID");
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementDataID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingSessionID = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordering = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.MovementID);
                    table.ForeignKey(
                        name: "FK_Movements_MovementDatas_MovementDataID",
                        column: x => x.MovementDataID,
                        principalTable: "MovementDatas",
                        principalColumn: "MovementDataID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_TrainingSessions_TrainingSessionID",
                        column: x => x.TrainingSessionID,
                        principalTable: "TrainingSessions",
                        principalColumn: "TrainingSessionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DTSetEntries",
                columns: table => new
                {
                    SetEntryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActualDistance = table.Column<double>(type: "REAL", nullable: false),
                    ActualDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ActualPace = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ActualRPE = table.Column<double>(type: "REAL", nullable: false),
                    ActualRest = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    DistanceUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    IntervalType = table.Column<int>(type: "INTEGER", nullable: false),
                    RecommendedDistance = table.Column<double>(type: "REAL", nullable: false),
                    RecommendedDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    RecommendedRest = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    TargetPace = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DTSetEntries", x => x.SetEntryID);
                    table.ForeignKey(
                        name: "FK_DTSetEntries_Movements_MovementID",
                        column: x => x.MovementID,
                        principalTable: "Movements",
                        principalColumn: "MovementID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiftSetEntries",
                columns: table => new
                {
                    SetEntryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActualRPE = table.Column<double>(type: "REAL", nullable: false),
                    ActualReps = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualWeight = table.Column<double>(type: "REAL", nullable: false),
                    RecommendedRPE = table.Column<double>(type: "REAL", nullable: true),
                    RecommendedReps = table.Column<int>(type: "INTEGER", nullable: true),
                    RecommendedWeight = table.Column<double>(type: "REAL", nullable: true),
                    WeightUnit = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiftSetEntries", x => x.SetEntryID);
                    table.ForeignKey(
                        name: "FK_LiftSetEntries_Movements_MovementID",
                        column: x => x.MovementID,
                        principalTable: "Movements",
                        principalColumn: "MovementID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_SourceLiftSetEntryID",
                table: "PersonalRecords",
                column: "SourceLiftSetEntryID");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserID",
                table: "Activities",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_DTSetEntries_MovementID",
                table: "DTSetEntries",
                column: "MovementID");

            migrationBuilder.CreateIndex(
                name: "IX_LiftSetEntries_MovementID",
                table: "LiftSetEntries",
                column: "MovementID");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_MovementDataID",
                table: "Movements",
                column: "MovementDataID");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_TrainingSessionID",
                table: "Movements",
                column: "TrainingSessionID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPrograms_UserID",
                table: "TrainingPrograms",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_TrainingProgramID",
                table: "TrainingSessions",
                column: "TrainingProgramID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_UserID",
                table: "TrainingSessions",
                column: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalRecords_LiftSetEntries_SourceLiftSetEntryID",
                table: "PersonalRecords",
                column: "SourceLiftSetEntryID",
                principalTable: "LiftSetEntries",
                principalColumn: "SetEntryID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
