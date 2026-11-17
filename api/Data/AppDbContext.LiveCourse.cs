using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<LiveCourseModule> LiveCourseModules => Set<LiveCourseModule>();
    public DbSet<LiveCourseSession> LiveCourseSessions => Set<LiveCourseSession>();
    public DbSet<LiveCourseRound> LiveCourseRounds => Set<LiveCourseRound>();
    public DbSet<LiveCourseEntry> LiveCourseEntries => Set<LiveCourseEntry>();
    public DbSet<LiveCourseVote> LiveCourseVotes => Set<LiveCourseVote>();
    public DbSet<LiveCourseReactionRecord> LiveCourseReactions => Set<LiveCourseReactionRecord>();

    private static void ConfigureLiveCourse(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LiveCourseModule>(e =>
        {
            e.ToTable("live_course_modules");
            e.HasKey(m => m.Id);
            e.Property(m => m.BlockId).HasMaxLength(128);
            e.Property(m => m.Kind).HasMaxLength(16);
            e.Property(m => m.PollType).HasMaxLength(12);
            e.Property(m => m.Prompt).HasMaxLength(200);
            e.HasIndex(m => new { m.PageId, m.BlockId });
            e.HasOne<NotionPage>().WithMany().HasForeignKey(m => m.PageId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LiveCourseSession>(e =>
        {
            e.ToTable("live_course_sessions");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.PageId).IsUnique().HasFilter("\"EndedAtUtc\" IS NULL");
            e.HasIndex(s => new { s.PageId, s.StartedAtUtc });
            e.HasOne<NotionPage>().WithMany().HasForeignKey(s => s.PageId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LiveCourseRound>(e =>
        {
            e.ToTable("live_course_rounds");
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.SessionId, r.OpenedAtUtc });
            e.HasOne<LiveCourseSession>().WithMany().HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Module).WithMany().HasForeignKey(r => r.ModuleId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<LiveCourseEntry>(e =>
        {
            e.ToTable("live_course_entries");
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.RoundId, a.UserSub, a.Slot }).IsUnique();
            e.HasIndex(a => a.UserSub);
            e.Property(a => a.Text).HasMaxLength(280);
            e.HasOne<LiveCourseRound>().WithMany().HasForeignKey(a => a.RoundId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LiveCourseVote>(e =>
        {
            e.ToTable("live_course_votes");
            e.HasKey(v => new { v.EntryId, v.UserSub });
            e.HasIndex(v => v.UserSub);
            e.HasOne<LiveCourseEntry>().WithMany().HasForeignKey(v => v.EntryId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LiveCourseReactionRecord>(e =>
        {
            e.ToTable("live_course_reactions");
            e.HasKey(r => new { r.SessionId, r.UserSub });
            e.HasIndex(r => new { r.SessionId, r.UpdatedAtUtc });
            e.HasIndex(r => r.UserSub);
            e.Property(r => r.Emoji).HasMaxLength(12);
            e.HasOne<LiveCourseSession>().WithMany().HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
