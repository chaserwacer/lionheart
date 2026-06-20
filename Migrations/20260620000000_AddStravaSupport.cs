using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class AddStravaSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "ApiAccessTokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "ApiAccessTokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StravaActivities",
                columns: table => new
                {
                    ObjectID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    StravaActivityID = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SportType = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartDateLocal = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ElapsedTimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    MovingTimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DistanceMeters = table.Column<double>(type: "REAL", nullable: false),
                    TotalElevationGainMeters = table.Column<double>(type: "REAL", nullable: false),
                    AverageSpeed = table.Column<double>(type: "REAL", nullable: false),
                    MaxSpeed = table.Column<double>(type: "REAL", nullable: false),
                    AverageHeartrate = table.Column<double>(type: "REAL", nullable: true),
                    MaxHeartrate = table.Column<double>(type: "REAL", nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StravaActivities", x => x.ObjectID);
                    table.ForeignKey(
                        name: "FK_StravaActivities_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StravaActivities_UserID_StravaActivityID",
                table: "StravaActivities",
                columns: new[] { "UserID", "StravaActivityID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StravaActivities");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "ApiAccessTokens");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "ApiAccessTokens");
        }
    }
}
