using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<KnowledgeCheckQuiz> KnowledgeCheckQuizzes => Set<KnowledgeCheckQuiz>();
    public DbSet<KnowledgeCheckVersion> KnowledgeCheckVersions => Set<KnowledgeCheckVersion>();
    public DbSet<KnowledgeCheckPublication> KnowledgeCheckPublications => Set<KnowledgeCheckPublication>();
    public DbSet<KnowledgeCheckAttempt> KnowledgeCheckAttempts => Set<KnowledgeCheckAttempt>();
    public DbSet<LiveKnowledgeCheck> LiveKnowledgeChecks => Set<LiveKnowledgeCheck>();

    private static void ConfigureKnowledgeChecks(ModelBuilder model)
    {
        model.Entity<LiveKnowledgeCheck>(entity =>
        {
            entity.ToTable("live_knowledge_checks");
            entity.Property(x => x.Code).HasMaxLength(6);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.ExpiresAtUtc);
        });
        model.Entity<KnowledgeCheckQuiz>(entity =>
        {
            entity.ToTable("knowledge_check_quizzes");
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Topic).HasMaxLength(80);
            entity.HasIndex(x => new { x.OwnerSub, x.UpdatedAtUtc });
        });
        model.Entity<KnowledgeCheckVersion>(entity =>
        {
            entity.ToTable("knowledge_check_versions");
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Topic).HasMaxLength(80);
            entity.HasIndex(x => new { x.QuizId, x.DraftRevision }).IsUnique();
            entity.HasOne(x => x.Quiz).WithMany().HasForeignKey(x => x.QuizId);
        });
        model.Entity<KnowledgeCheckPublication>(entity =>
        {
            entity.ToTable("knowledge_check_publications");
            entity.Property(x => x.FeedbackMode).HasMaxLength(16);
            entity.HasIndex(x => new { x.HoldId, x.IsActive, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.VersionId, x.RequestId }).IsUnique();
            entity.HasOne(x => x.Version).WithMany().HasForeignKey(x => x.VersionId);
            entity.HasOne(x => x.Hold).WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<KnowledgeCheckAttempt>(entity =>
        {
            entity.ToTable("knowledge_check_attempts");
            entity.Property(x => x.Status).HasMaxLength(12);
            entity.HasIndex(x => new { x.PublicationId, x.UserSub })
                .HasFilter("\"Status\" = 'started'").IsUnique();
            entity.HasIndex(x => new { x.PublicationId, x.UserSub, x.RequestId }).IsUnique();
            entity.HasIndex(x => new { x.PublicationId, x.SubmittedAtUtc });
            entity.HasIndex(x => new { x.UserSub, x.StartedAtUtc });
            entity.HasOne(x => x.Publication).WithMany().HasForeignKey(x => x.PublicationId);
        });
    }
}
