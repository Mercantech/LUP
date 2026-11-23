using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HomeOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "onboarding_dismissals",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_dismissals", x => x.UserSub);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_steps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Audience = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_steps", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "onboarding_steps",
                columns: new[] { "Id", "Audience", "SortOrder", "Text", "Title" },
                values: new object[,]
                {
                    { -1, "student", 1, "Start i Mine hold. Her finder du dine forløb, quests og afleveringer. Under Mine handlinger kan du se, hvad der venter på dig.", "Velkommen til LUP" },
                    { -2, "teacher", 1, "Start i Mine hold for at åbne undervisning, quests og afstemninger. Under Til opfølgning finder du afleveringer, der venter på feedback.", "Velkommen til LUP" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_dismissals");

            migrationBuilder.DropTable(
                name: "onboarding_steps");
        }
    }
}
