using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialGameRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "social_game_rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    HostSub = table.Column<Guid>(type: "uuid", nullable: false),
                    IsStarted = table.Column<bool>(type: "boolean", nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    NextDeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_social_game_rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialGameInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientSub = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialGameInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialGameInvitations_social_game_rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "social_game_rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_social_game_rooms_Code",
                table: "social_game_rooms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_social_game_rooms_ExpiresAtUtc",
                table: "social_game_rooms",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_social_game_rooms_NextDeadlineUtc",
                table: "social_game_rooms",
                column: "NextDeadlineUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SocialGameInvitations_RecipientSub_ReadAtUtc",
                table: "SocialGameInvitations",
                columns: new[] { "RecipientSub", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialGameInvitations_RoomId_RecipientSub",
                table: "SocialGameInvitations",
                columns: new[] { "RoomId", "RecipientSub" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialGameInvitations");

            migrationBuilder.DropTable(
                name: "social_game_rooms");
        }
    }
}
