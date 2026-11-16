using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HoldCalendarPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttendanceMode",
                table: "hold_calendar_events",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuestId",
                table: "hold_calendar_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hold_calendar_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    QuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hold_calendar_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hold_calendar_changes_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hold_calendar_events_QuestId",
                table: "hold_calendar_events",
                column: "QuestId");

            migrationBuilder.CreateIndex(
                name: "IX_hold_calendar_changes_EventId_EventVersion",
                table: "hold_calendar_changes",
                columns: new[] { "EventId", "EventVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_hold_calendar_changes_HoldId_ChangedAtUtc",
                table: "hold_calendar_changes",
                columns: new[] { "HoldId", "ChangedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_hold_calendar_events_learning_quests_QuestId",
                table: "hold_calendar_events",
                column: "QuestId",
                principalTable: "learning_quests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hold_calendar_events_learning_quests_QuestId",
                table: "hold_calendar_events");

            migrationBuilder.DropTable(
                name: "hold_calendar_changes");

            migrationBuilder.DropIndex(
                name: "IX_hold_calendar_events_QuestId",
                table: "hold_calendar_events");

            migrationBuilder.DropColumn(
                name: "AttendanceMode",
                table: "hold_calendar_events");

            migrationBuilder.DropColumn(
                name: "QuestId",
                table: "hold_calendar_events");
        }
    }
}
