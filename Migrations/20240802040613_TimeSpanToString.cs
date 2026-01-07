using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class TimeSpanToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    ActivityID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TimeSpan = table.Column<string>(type: "TEXT", nullable: false),
                    CaloriesBurned = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserSummary = table.Column<string>(type: "TEXT", nullable: false),
                    AccumulatedFatigue = table.Column<int>(type: "INTEGER", nullable: false),
                    DifficultyRating = table.Column<int>(type: "INTEGER", nullable: false),
                    EngagementRating = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalVariablesRating = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "LiftDetails",
                columns: table => new
                {
                    ActivityID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tonnage = table.Column<int>(type: "INTEGER", nullable: false),
                    LiftType = table.Column<string>(type: "TEXT", nullable: false),
                    LiftFocus = table.Column<string>(type: "TEXT", nullable: false)
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
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    ElevationGain = table.Column<int>(type: "INTEGER", nullable: false),
                    AveragePower = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageSpeed = table.Column<double>(type: "REAL", nullable: false),
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
                    Distance = table.Column<double>(type: "REAL", nullable: false),
                    ElevationGain = table.Column<int>(type: "INTEGER", nullable: false),
                    AveragePace = table.Column<string>(type: "TEXT", nullable: false),
                    MileSplits = table.Column<string>(type: "TEXT", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserID",
                table: "Activities",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiftDetails");

            migrationBuilder.DropTable(
                name: "RideDetails");

            migrationBuilder.DropTable(
                name: "RunWalkDetails");

            migrationBuilder.DropTable(
                name: "Activities");
        }
    }
}
