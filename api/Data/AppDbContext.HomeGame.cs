using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.HomeGame;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<HomeGameProfileRow> HomeGameProfiles => Set<HomeGameProfileRow>();
    public DbSet<HomeBuildReceiptRow> HomeGameBuildReceipts => Set<HomeBuildReceiptRow>();
    public DbSet<HomeProgramRow> HomeGamePrograms => Set<HomeProgramRow>();
    public DbSet<HomeRunRow> HomeGameRuns => Set<HomeRunRow>();
    public DbSet<HomeClassroomRow> HomeGameClassrooms => Set<HomeClassroomRow>();
    public DbSet<HomeHelpRow> HomeGameHelpRequests => Set<HomeHelpRow>();

    private static void ConfigureHomeGame(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HomeGameProfileRow>(entity =>
        {
            entity.ToTable("home_game_profiles", table =>
                table.HasCheckConstraint("CK_home_game_profiles_revision", "\"Revision\" >= 0"));
            entity.HasKey(row => row.UserSub);
            entity.Property(row => row.UserSub).ValueGeneratedNever();
        });
        modelBuilder.Entity<HomeBuildReceiptRow>(entity =>
        {
            entity.ToTable("home_game_build_receipts");
            entity.HasKey(row => new { row.UserSub, row.OperationId });
            entity.HasOne<HomeGameProfileRow>().WithMany().HasForeignKey(row => row.UserSub)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<HomeProgramRow>(entity =>
        {
            entity.ToTable("home_game_programs");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.HasOne<HomeGameProfileRow>().WithMany().HasForeignKey(row => row.UserSub)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<HomeRunRow>(entity =>
        {
            entity.ToTable("home_game_runs");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.HasOne<HomeGameProfileRow>().WithMany().HasForeignKey(row => row.UserSub)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(row => new { row.UserSub, row.StartedAtUtc });
            entity.HasIndex(row => row.UserSub).HasDatabaseName("IX_home_game_runs_active")
                .IsUnique().HasFilter("\"Active\"");
        });
        modelBuilder.Entity<HomeClassroomRow>(entity =>
        {
            entity.ToTable("home_game_classrooms");
            entity.HasKey(row => row.HoldId);
            entity.Property(row => row.HoldId).ValueGeneratedNever();
            entity.HasOne<Hold>().WithMany().HasForeignKey(row => row.HoldId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<HomeHelpRow>(entity =>
        {
            entity.ToTable("home_game_help_requests");
            entity.HasKey(row => new { row.UserSub, row.Id });
            entity.HasOne<Hold>().WithMany().HasForeignKey(row => row.HoldId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(row => row.HoldId);
        });
    }
}
