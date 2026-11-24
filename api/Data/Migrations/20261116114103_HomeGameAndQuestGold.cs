using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HomeGameAndQuestGold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "home_game_classrooms",
                columns: table => new
                {
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_game_classrooms", x => x.HoldId);
                    table.ForeignKey(
                        name: "FK_home_game_classrooms_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "home_game_help_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_game_help_requests", x => new { x.UserSub, x.Id });
                    table.ForeignKey(
                        name: "FK_home_game_help_requests_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "home_game_profiles",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_game_profiles", x => x.UserSub);
                    table.CheckConstraint("CK_home_game_profiles_revision", "\"Revision\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "quest_gold_awards",
                columns: table => new
                {
                    QuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Gold = table.Column<int>(type: "integer", nullable: false),
                    AwardedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_gold_awards", x => new { x.QuestId, x.UserSub });
                    table.CheckConstraint("CK_quest_gold_awards_gold", "\"Gold\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "home_game_build_receipts",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_game_build_receipts", x => new { x.UserSub, x.OperationId });
                    table.ForeignKey(
                        name: "FK_home_game_build_receipts_home_game_profiles_UserSub",
                        column: x => x.UserSub,
                        principalTable: "home_game_profiles",
                        principalColumn: "UserSub",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "home_game_programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SourceJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_game_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_home_game_programs_home_game_profiles_UserSub",
                        column: x => x.UserSub,
                        principalTable: "home_game_profiles",
                        principalColumn: "UserSub",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "home_game_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    RunJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_game_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_home_game_runs_home_game_profiles_UserSub",
                        column: x => x.UserSub,
                        principalTable: "home_game_profiles",
                        principalColumn: "UserSub",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_home_game_help_requests_HoldId",
                table: "home_game_help_requests",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_home_game_programs_UserSub",
                table: "home_game_programs",
                column: "UserSub");

            migrationBuilder.CreateIndex(
                name: "IX_home_game_runs_active",
                table: "home_game_runs",
                column: "UserSub",
                unique: true,
                filter: "\"Active\"");

            migrationBuilder.CreateIndex(
                name: "IX_home_game_runs_UserSub_StartedAtUtc",
                table: "home_game_runs",
                columns: new[] { "UserSub", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_quest_gold_awards_UserSub",
                table: "quest_gold_awards",
                column: "UserSub");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "home_game_build_receipts");

            migrationBuilder.DropTable(
                name: "home_game_classrooms");

            migrationBuilder.DropTable(
                name: "home_game_help_requests");

            migrationBuilder.DropTable(
                name: "home_game_programs");

            migrationBuilder.DropTable(
                name: "home_game_runs");

            migrationBuilder.DropTable(
                name: "quest_gold_awards");

            migrationBuilder.DropTable(
                name: "home_game_profiles");
        }
    }
}
