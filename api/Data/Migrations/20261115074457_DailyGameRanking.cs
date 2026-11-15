using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DailyGameRanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_game_ranking_preferences",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_game_ranking_preferences", x => x.UserSub);
                    table.ForeignKey(
                        name: "FK_daily_game_ranking_preferences_profile_names_UserSub",
                        column: x => x.UserSub,
                        principalTable: "profile_names",
                        principalColumn: "UserSub",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_game_ranking_preferences");
        }
    }
}
