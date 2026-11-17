using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiveCoursePresentations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_course_modules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PollType = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    Prompt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    CorrectChoice = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_course_modules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_course_modules_notion_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "notion_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_course_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedRevision = table.Column<long>(type: "bigint", nullable: false),
                    PublicationVersion = table.Column<long>(type: "bigint", nullable: false),
                    SlideCount = table.Column<int>(type: "integer", nullable: false),
                    SlideIndex = table.Column<int>(type: "integer", nullable: false),
                    BindingsJson = table.Column<string>(type: "text", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActiveRoundId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RankingVisible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_course_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_course_sessions_notion_pages_PageId",
                        column: x => x.PageId,
                        principalTable: "notion_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_course_reactions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_course_reactions", x => new { x.SessionId, x.UserSub });
                    table.ForeignKey(
                        name: "FK_live_course_reactions_live_course_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "live_course_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_course_rounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShowResults = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_course_rounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_course_rounds_live_course_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "live_course_modules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_live_course_rounds_live_course_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "live_course_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_course_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    Choice = table.Column<int>(type: "integer", nullable: true),
                    Approved = table.Column<bool>(type: "boolean", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_course_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_live_course_entries_live_course_rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "live_course_rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_course_votes",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_course_votes", x => new { x.EntryId, x.UserSub });
                    table.ForeignKey(
                        name: "FK_live_course_votes_live_course_entries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "live_course_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_course_entries_RoundId_UserSub_Slot",
                table: "live_course_entries",
                columns: new[] { "RoundId", "UserSub", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_course_entries_UserSub",
                table: "live_course_entries",
                column: "UserSub");

            migrationBuilder.CreateIndex(
                name: "IX_live_course_modules_PageId_BlockId",
                table: "live_course_modules",
                columns: new[] { "PageId", "BlockId" });

            migrationBuilder.CreateIndex(
                name: "IX_live_course_reactions_SessionId_UpdatedAtUtc",
                table: "live_course_reactions",
                columns: new[] { "SessionId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_live_course_reactions_UserSub",
                table: "live_course_reactions",
                column: "UserSub");

            migrationBuilder.CreateIndex(
                name: "IX_live_course_rounds_ModuleId",
                table: "live_course_rounds",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_live_course_rounds_SessionId_OpenedAtUtc",
                table: "live_course_rounds",
                columns: new[] { "SessionId", "OpenedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_live_course_sessions_PageId",
                table: "live_course_sessions",
                column: "PageId",
                unique: true,
                filter: "\"EndedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_live_course_sessions_PageId_StartedAtUtc",
                table: "live_course_sessions",
                columns: new[] { "PageId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_live_course_votes_UserSub",
                table: "live_course_votes",
                column: "UserSub");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_course_reactions");

            migrationBuilder.DropTable(
                name: "live_course_votes");

            migrationBuilder.DropTable(
                name: "live_course_entries");

            migrationBuilder.DropTable(
                name: "live_course_rounds");

            migrationBuilder.DropTable(
                name: "live_course_modules");

            migrationBuilder.DropTable(
                name: "live_course_sessions");
        }
    }
}
