using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftKnowledgeChecksJson",
                table: "notion_pages",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "PublishedKnowledgeChecksJson",
                table: "notion_pages",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "knowledge_check_quizzes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerSub = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Topic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    QuestionsJson = table.Column<string>(type: "text", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_check_quizzes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_check_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Topic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    QuestionsJson = table.Column<string>(type: "text", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    DraftRevision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_check_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_check_versions_knowledge_check_quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "knowledge_check_quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_check_publications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: true),
                    FeedbackMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FeedbackReleased = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_check_publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_check_publications_hold_HoldId",
                        column: x => x.HoldId,
                        principalTable: "hold",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_check_publications_knowledge_check_versions_Versi~",
                        column: x => x.VersionId,
                        principalTable: "knowledge_check_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_check_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    QuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    AnswersJson = table.Column<string>(type: "text", nullable: false),
                    GradesJson = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_check_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_check_attempts_knowledge_check_publications_Publi~",
                        column: x => x.PublicationId,
                        principalTable: "knowledge_check_publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_check_attempts_PublicationId_SubmittedAtUtc",
                table: "knowledge_check_attempts",
                columns: new[] { "PublicationId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_check_attempts_PublicationId_UserSub",
                table: "knowledge_check_attempts",
                columns: new[] { "PublicationId", "UserSub" },
                unique: true,
                filter: "\"Status\" = 'started'");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_check_attempts_PublicationId_UserSub_RequestId",
                table: "knowledge_check_attempts",
                columns: new[] { "PublicationId", "UserSub", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_check_attempts_UserSub_StartedAtUtc",
                table: "knowledge_check_attempts",
                columns: new[] { "UserSub", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_check_publications_HoldId_IsActive_CreatedAtUtc",
                table: "knowledge_check_publications",
                columns: new[] { "HoldId", "IsActive", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_check_publications_VersionId_RequestId",
                table: "knowledge_check_publications",
                columns: new[] { "VersionId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_check_quizzes_OwnerSub_UpdatedAtUtc",
                table: "knowledge_check_quizzes",
                columns: new[] { "OwnerSub", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_check_versions_QuizId_DraftRevision",
                table: "knowledge_check_versions",
                columns: new[] { "QuizId", "DraftRevision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_check_attempts");

            migrationBuilder.DropTable(
                name: "knowledge_check_publications");

            migrationBuilder.DropTable(
                name: "knowledge_check_versions");

            migrationBuilder.DropTable(
                name: "knowledge_check_quizzes");

            migrationBuilder.DropColumn(
                name: "DraftKnowledgeChecksJson",
                table: "notion_pages");

            migrationBuilder.DropColumn(
                name: "PublishedKnowledgeChecksJson",
                table: "notion_pages");
        }
    }
}
