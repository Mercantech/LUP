using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingGuideSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "onboarding_steps",
                columns: new[] { "Id", "Audience", "SortOrder", "Text", "Title" },
                values: new object[,]
                {
                    { -8, "teacher", 4, "Åbn Underviserværktøjer fra avatarmenuen for at finde kalender, afstemninger og videnstjek. Her kan du også genåbne denne guide og se elevernes onboarding.", "Find værktøjerne igen" },
                    { -7, "teacher", 3, "Brug Til opfølgning til at finde afleveringer, der venter på feedback. I Elevnotesbøger kan du læse og kommentere elevernes noter og logbøger.", "Følg elevernes arbejde" },
                    { -6, "teacher", 2, "Åbn et hold for at finde forløb, quests, materialer og kalender. I Notesbøger og forløbseditor kan du skrive og udgive undervisningssider.", "Planlæg og del undervisning" },
                    { -5, "student", 4, "Brug Favoritter på forsiden til dine vigtigste sider. I avatarmenuen finder du din profil og Hjælp, når du har brug for at finde vej igen.", "Gør LUP til dit eget" },
                    { -4, "student", 3, "Åbn Din notesbog fra forsiden for at samle faglige noter. Brug Skriv logbog til at fortælle, hvad du har arbejdet med, og hvad du har lært.", "Saml dine noter" },
                    { -3, "student", 2, "Åbn et hold og vælg Quests og afleveringer. Her kan du læse opgaven og aflevere dit arbejde. Kig i Mine handlinger for at følge op på feedback.", "Find dine opgaver" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "onboarding_steps",
                keyColumn: "Id",
                keyValue: -8);

            migrationBuilder.DeleteData(
                table: "onboarding_steps",
                keyColumn: "Id",
                keyValue: -7);

            migrationBuilder.DeleteData(
                table: "onboarding_steps",
                keyColumn: "Id",
                keyValue: -6);

            migrationBuilder.DeleteData(
                table: "onboarding_steps",
                keyColumn: "Id",
                keyValue: -5);

            migrationBuilder.DeleteData(
                table: "onboarding_steps",
                keyColumn: "Id",
                keyValue: -4);

            migrationBuilder.DeleteData(
                table: "onboarding_steps",
                keyColumn: "Id",
                keyValue: -3);
        }
    }
}
