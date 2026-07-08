using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class UserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AthleteContextCards",
                columns: table => new
                {
                    CardID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    GeneratorVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    StableProfileText = table.Column<string>(type: "TEXT", nullable: false),
                    StableProfileVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    StableProfileAsOf = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecentStateText = table.Column<string>(type: "TEXT", nullable: false),
                    RecentStateVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    RecentStateAsOf = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CoverageManifest = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteContextCards", x => x.CardID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteContextCards_UserID",
                table: "AthleteContextCards",
                column: "UserID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AthleteContextCards");
        }
    }
}
