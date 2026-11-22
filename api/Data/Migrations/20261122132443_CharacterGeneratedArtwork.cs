using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharacterGeneratedArtwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_generated_artwork",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OriginalAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotionPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotebookId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotebookPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    Thumbnail = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_generated_artwork", x => x.Id);
                    table.CheckConstraint("CK_character_generated_artwork_source", "(CASE WHEN \"CollectionId\" IS NULL THEN 0 ELSE 1 END)\n+ (CASE WHEN \"NotionPageId\" IS NULL THEN 0 ELSE 1 END)\n+ (CASE WHEN \"NotebookId\" IS NULL THEN 0 ELSE 1 END)\n+ (CASE WHEN \"NotebookPageId\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_character_generated_artwork_notebook_documents_NotebookPage~",
                        column: x => x.NotebookPageId,
                        principalTable: "notebook_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_character_generated_artwork_notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_character_generated_artwork_notion_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "notion_collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_character_generated_artwork_notion_pages_NotionPageId",
                        column: x => x.NotionPageId,
                        principalTable: "notion_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_generated_artwork_CharacterSlug_CreatedAtUtc_Id",
                table: "character_generated_artwork",
                columns: new[] { "CharacterSlug", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_character_generated_artwork_CharacterSlug_SourceKind_Origin~",
                table: "character_generated_artwork",
                columns: new[] { "CharacterSlug", "SourceKind", "OriginalAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_generated_artwork_CollectionId",
                table: "character_generated_artwork",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_character_generated_artwork_CreatedAtUtc_Id",
                table: "character_generated_artwork",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_character_generated_artwork_NotebookId",
                table: "character_generated_artwork",
                column: "NotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_character_generated_artwork_NotebookPageId",
                table: "character_generated_artwork",
                column: "NotebookPageId");

            migrationBuilder.CreateIndex(
                name: "IX_character_generated_artwork_NotionPageId",
                table: "character_generated_artwork",
                column: "NotionPageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_generated_artwork");
        }
    }
}
