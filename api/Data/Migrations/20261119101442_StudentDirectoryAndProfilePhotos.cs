using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentDirectoryAndProfilePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "profile_photos",
                columns: table => new
                {
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_photos", x => x.UserSub);
                });

            migrationBuilder.CreateTable(
                name: "student_directory_details",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LectioId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: true),
                    Placement = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CurrentSchoolPeriod = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    WorkplaceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastPassedSchoolPeriod = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBySub = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_directory_details", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_directory_details_LectioId",
                table: "student_directory_details",
                column: "LectioId");

            migrationBuilder.CreateIndex(
                name: "IX_student_directory_details_UserSub",
                table: "student_directory_details",
                column: "UserSub");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_photos");

            migrationBuilder.DropTable(
                name: "student_directory_details");
        }
    }
}
