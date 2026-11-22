using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotebookCoverPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArtworkSourceId",
                table: "notebook_cover_assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionY",
                table: "notebook_cover_assets",
                type: "integer",
                nullable: false,
                defaultValue: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtworkSourceId",
                table: "notebook_cover_assets");

            migrationBuilder.DropColumn(
                name: "PositionY",
                table: "notebook_cover_assets");
        }
    }
}
