using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotesCollectionSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "notion_collections",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notion_collections_Slug",
                table: "notion_collections",
                column: "Slug",
                unique: true);

            migrationBuilder.UpdateData(
                table: "notion_collections",
                keyColumn: "Id",
                keyValue: new Guid("7a18edfe-0c7a-496e-884d-18a29595adae"),
                column: "Slug",
                value: "ml2027");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notion_collections_Slug",
                table: "notion_collections");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "notion_collections");
        }
    }
}
