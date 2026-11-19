using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotebookPageIcons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IconEmojiId",
                table: "notion_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedIconEmojiId",
                table: "notion_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IconEmojiId",
                table: "notebook_documents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconEmojiId",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedIconEmojiId",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "IconEmojiId",
                table: "notebook_documents");
        }
    }
}
