using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class AppleHealthMeasures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthDailyMeasures",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Metric = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Device = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    Maximum = table.Column<double>(type: "REAL", nullable: false),
                    Mean = table.Column<double>(type: "REAL", nullable: false),
                    Minimum = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthDailyMeasures", x => new { x.UserId, x.Metric, x.Device, x.Day });
                    table.ForeignKey(
                        name: "FK_HealthDailyMeasures_LionheartUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HealthDailyMeasures");
        }
    }
}
