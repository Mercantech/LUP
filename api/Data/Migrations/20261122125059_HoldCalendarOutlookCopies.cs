using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HoldCalendarOutlookCopies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hold_calendar_outlook_copies",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mailbox = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GraphEventId = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresUnixSeconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hold_calendar_outlook_copies", x => new { x.EventId, x.Mailbox });
                    table.ForeignKey(
                        name: "FK_hold_calendar_outlook_copies_hold_calendar_events_EventId",
                        column: x => x.EventId,
                        principalTable: "hold_calendar_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hold_calendar_outlook_copies_Mailbox",
                table: "hold_calendar_outlook_copies",
                column: "Mailbox",
                filter: "\"GraphEventId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hold_calendar_outlook_copies");
        }
    }
}
