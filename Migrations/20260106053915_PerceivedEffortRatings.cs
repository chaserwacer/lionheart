using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class PerceivedEffortRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityPerceivedEffortRatings");

            migrationBuilder.DropTable(
                name: "TrainingSessionPerceivedEffortRatings");

            migrationBuilder.AddColumn<int>(
                name: "PerceivedEffortRatings_AccumulatedFatigue",
                table: "TrainingSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerceivedEffortRatings_DifficultyRating",
                table: "TrainingSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerceivedEffortRatings_EngagementRating",
                table: "TrainingSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerceivedEffortRatings_ExternalVariablesRating",
                table: "TrainingSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PerceivedEffortRatings_RecordedAt",
                table: "TrainingSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TrainingSessionID",
                table: "InjuryEvents",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "PerceivedEffortRatings_AccumulatedFatigue",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerceivedEffortRatings_DifficultyRating",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerceivedEffortRatings_EngagementRating",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerceivedEffortRatings_ExternalVariablesRating",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PerceivedEffortRatings_RecordedAt",
                table: "Activities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_AccumulatedFatigue",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_DifficultyRating",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_EngagementRating",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_ExternalVariablesRating",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_RecordedAt",
                table: "TrainingSessions");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_AccumulatedFatigue",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_DifficultyRating",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_EngagementRating",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_ExternalVariablesRating",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "PerceivedEffortRatings_RecordedAt",
                table: "Activities");

            migrationBuilder.AlterColumn<Guid>(
                name: "TrainingSessionID",
                table: "InjuryEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

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
        }
    }
}
