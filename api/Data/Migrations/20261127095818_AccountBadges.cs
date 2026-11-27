using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mercantec.Tech.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountBadges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_badges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSub = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: true),
                    Evidence = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    ReviewerSub = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NoticeRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_badges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_badges_HoldId_Status",
                table: "account_badges",
                columns: new[] { "HoldId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_account_badges_UserSub_BadgeId_Rank",
                table: "account_badges",
                columns: new[] { "UserSub", "BadgeId", "Rank" },
                unique: true);

            // Record the award in the same transaction as the qualifying work.
            migrationBuilder.Sql("""
                CREATE FUNCTION lup_record_badge(owner_sub uuid, badge text, evidence text, earned_at timestamptz)
                RETURNS void LANGUAGE sql AS $$
                    INSERT INTO account_badges ("Id","UserSub","BadgeId","Rank","Status","Evidence","UpdatedUtc","NoticeRead")
                    VALUES (gen_random_uuid(), owner_sub, badge, 1, 'earned', evidence, earned_at, false)
                    ON CONFLICT ("UserSub","BadgeId","Rank") DO NOTHING;
                $$;
                CREATE FUNCTION lup_submission_badge() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM lup_record_badge(NEW."SubmittedBySub", 'first-submission', 'Du afleverede dit arbejde på en quest.', NEW."SubmittedAtUtc");
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER lup_submission_badge AFTER INSERT ON submission_revisions
                    FOR EACH ROW EXECUTE FUNCTION lup_submission_badge();
                CREATE FUNCTION lup_wordle_badge() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM lup_record_badge(NEW."UserSub", 'wordle-win', 'Du løste dagens Wordle.', now());
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER lup_wordle_badge AFTER INSERT OR UPDATE OF "Status" ON wordle_plays
                    FOR EACH ROW WHEN (NEW."Status" = 1) EXECUTE FUNCTION lup_wordle_badge();

                SELECT lup_record_badge("SubmittedBySub", 'first-submission', 'Du afleverede dit arbejde på en quest.', min("SubmittedAtUtc"))
                FROM submission_revisions GROUP BY "SubmittedBySub";
                SELECT lup_record_badge("UserSub", 'wordle-win', 'Du løste dagens Wordle.', min("Date")::timestamp AT TIME ZONE 'Europe/Copenhagen')
                FROM wordle_plays WHERE "Status" = 1 GROUP BY "UserSub";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER lup_wordle_badge ON wordle_plays;
                DROP TRIGGER lup_submission_badge ON submission_revisions;
                DROP FUNCTION lup_wordle_badge(), lup_submission_badge(), lup_record_badge(uuid,text,text,timestamptz);
                """);
            migrationBuilder.DropTable(
                name: "account_badges");
        }
    }
}
