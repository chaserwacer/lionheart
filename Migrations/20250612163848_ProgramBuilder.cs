using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class ProgramBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovementBases",
                columns: table => new
                {
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementBases", x => x.MovementBaseID);
                });

            migrationBuilder.CreateTable(
                name: "TrainingPrograms",
                columns: table => new
                {
                    TrainingProgramID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    NextTrainingSessionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false)
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
                    TrainingProgramID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.TrainingSessionID);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_TrainingPrograms_TrainingProgramID",
                        column: x => x.TrainingProgramID,
                        principalTable: "TrainingPrograms",
                        principalColumn: "TrainingProgramID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingSessionID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementModifier_Name = table.Column<string>(type: "TEXT", nullable: false),
                    MovementModifier_Equipment = table.Column<string>(type: "TEXT", nullable: false),
                    MovementModifier_Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.MovementID);
                    table.ForeignKey(
                        name: "FK_Movements_MovementBases_MovementBaseID",
                        column: x => x.MovementBaseID,
                        principalTable: "MovementBases",
                        principalColumn: "MovementBaseID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_TrainingSessions_TrainingSessionID",
                        column: x => x.TrainingSessionID,
                        principalTable: "TrainingSessions",
                        principalColumn: "TrainingSessionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetEntries",
                columns: table => new
                {
                    SetEntryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecommendedReps = table.Column<int>(type: "INTEGER", nullable: false),
                    RecommendedWeight = table.Column<double>(type: "REAL", nullable: false),
                    RecommendedRPE = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualReps = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualWeight = table.Column<double>(type: "REAL", nullable: false),
                    ActualRPE = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_Movements_MovementBaseID",
                table: "Movements",
                column: "MovementBaseID");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_TrainingSessionID",
                table: "Movements",
                column: "TrainingSessionID");

            migrationBuilder.CreateIndex(
                name: "IX_SetEntries_MovementID",
                table: "SetEntries",
                column: "MovementID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPrograms_UserID",
                table: "TrainingPrograms",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_TrainingProgramID",
                table: "TrainingSessions",
                column: "TrainingProgramID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetEntries");

            migrationBuilder.DropTable(
                name: "Movements");

            migrationBuilder.DropTable(
                name: "MovementBases");

            migrationBuilder.DropTable(
                name: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "TrainingPrograms");
        }
    }
}
