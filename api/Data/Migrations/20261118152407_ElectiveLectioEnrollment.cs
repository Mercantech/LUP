using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ElectiveLectioEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_elective_enrollments",
                table: "elective_enrollments");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserSub",
                table: "elective_enrollments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "elective_enrollments",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "LectioId",
                table: "elective_enrollments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_elective_enrollments",
                table: "elective_enrollments",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_elective_enrollments_ElectiveId_LectioId",
                table: "elective_enrollments",
                columns: new[] { "ElectiveId", "LectioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_elective_enrollments_ElectiveId_UserSub",
                table: "elective_enrollments",
                columns: new[] { "ElectiveId", "UserSub" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_elective_enrollments_LectioId",
                table: "elective_enrollments",
                column: "LectioId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_elective_enrollments_identity",
                table: "elective_enrollments",
                sql: "\"UserSub\" IS NOT NULL OR \"LectioId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The former schema cannot represent students who have not linked their LUP account.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM elective_enrollments WHERE "UserSub" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade while elective enrollments without a linked account exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_elective_enrollments",
                table: "elective_enrollments");

            migrationBuilder.DropIndex(
                name: "IX_elective_enrollments_ElectiveId_LectioId",
                table: "elective_enrollments");

            migrationBuilder.DropIndex(
                name: "IX_elective_enrollments_ElectiveId_UserSub",
                table: "elective_enrollments");

            migrationBuilder.DropIndex(
                name: "IX_elective_enrollments_LectioId",
                table: "elective_enrollments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_elective_enrollments_identity",
                table: "elective_enrollments");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "elective_enrollments");

            migrationBuilder.DropColumn(
                name: "LectioId",
                table: "elective_enrollments");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserSub",
                table: "elective_enrollments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_elective_enrollments",
                table: "elective_enrollments",
                columns: new[] { "ElectiveId", "UserSub" });
        }
    }
}
