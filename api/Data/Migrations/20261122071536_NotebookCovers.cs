using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotebookCovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoverId",
                table: "notion_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedCoverId",
                table: "notion_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoverId",
                table: "notion_collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoverId",
                table: "notebooks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoverId",
                table: "notebook_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notebook_cover_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotionPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotebookId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotebookPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebook_cover_assets", x => x.Id);
                    table.CheckConstraint("CK_notebook_cover_assets_owner", "(CASE WHEN \"CollectionId\" IS NULL THEN 0 ELSE 1 END)\n+ (CASE WHEN \"NotionPageId\" IS NULL THEN 0 ELSE 1 END)\n+ (CASE WHEN \"NotebookId\" IS NULL THEN 0 ELSE 1 END)\n+ (CASE WHEN \"NotebookPageId\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_notebook_cover_assets_notebook_documents_NotebookPageId",
                        column: x => x.NotebookPageId,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notebook_cover_assets_notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notebook_cover_assets_notion_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "notion_collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notebook_cover_assets_notion_pages_NotionPageId",
                        column: x => x.NotionPageId,
                        principalTable: "notion_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notebook_cover_assets_CollectionId",
                table: "notebook_cover_assets",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_cover_assets_NotebookId",
                table: "notebook_cover_assets",
                column: "NotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_cover_assets_NotebookPageId",
                table: "notebook_cover_assets",
                column: "NotebookPageId");

            migrationBuilder.CreateIndex(
                name: "IX_notebook_cover_assets_NotionPageId",
                table: "notebook_cover_assets",
                column: "NotionPageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notebook_cover_assets");

            migrationBuilder.DropColumn(
                name: "CoverId",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedCoverId",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "CoverId",
                table: "notion_collections");

            migrationBuilder.DropColumn(
                name: "CoverId",
                table: "notebooks");

            migrationBuilder.DropColumn(
                name: "CoverId",
                table: "notebook_documents");
        }
    }
}
