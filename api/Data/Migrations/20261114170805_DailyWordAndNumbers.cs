using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DailyWordAndNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_game_puzzles",
                columns: table => new
                {
                    Game = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_game_puzzles", x => new { x.Game, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "daily_game_schedule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_game_schedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "daily_game_plays",
                columns: table => new
                {
                    Game = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_game_plays", x => new { x.UserSub, x.Game, x.Date });
                    table.ForeignKey(
                        name: "FK_daily_game_plays_daily_game_puzzles_Game_Date",
                        columns: x => new { x.Game, x.Date },
                        principalTable: "daily_game_puzzles",
                        principalColumns: new[] { "Game", "Date" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_game_plays_Game_Date",
                table: "daily_game_plays",
                columns: new[] { "Game", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_game_plays");

            migrationBuilder.DropTable(
                name: "daily_game_schedule");

            migrationBuilder.DropTable(
                name: "daily_game_puzzles");
        }
    }
}
