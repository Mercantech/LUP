using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<DailyGameSchedule> DailyGameSchedules => Set<DailyGameSchedule>();
    public DbSet<DailyGamePuzzle> DailyGamePuzzles => Set<DailyGamePuzzle>();
    public DbSet<DailyGamePlay> DailyGamePlays => Set<DailyGamePlay>();

    private static void ConfigureDailyGames(ModelBuilder model)
    {
        model.Entity<DailyGameSchedule>(entity =>
        {
            entity.ToTable("daily_game_schedule");
            entity.HasKey(schedule => schedule.Id);
            entity.Property(schedule => schedule.Id).ValueGeneratedNever();
            entity.Property(schedule => schedule.Key).HasMaxLength(64).IsRequired();
        });

        model.Entity<DailyGamePuzzle>(entity =>
        {
            entity.ToTable("daily_game_puzzles");
            entity.HasKey(puzzle => new { puzzle.Game, puzzle.Date });
            entity.Property(puzzle => puzzle.Game).HasMaxLength(24);
        });

        model.Entity<DailyGamePlay>(entity =>
        {
            entity.ToTable("daily_game_plays");
            entity.HasKey(play => new { play.UserSub, play.Game, play.Date });
            entity.Property(play => play.Game).HasMaxLength(24);
            entity.Property(play => play.Version).IsConcurrencyToken();
            entity.HasOne<DailyGamePuzzle>().WithMany().HasForeignKey(play => new { play.Game, play.Date });
        });
    }
}
