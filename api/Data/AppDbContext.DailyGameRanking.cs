using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<DailyGameRankingPreference> DailyGameRankingPreferences => Set<DailyGameRankingPreference>();

    private static void ConfigureDailyGameRanking(ModelBuilder model)
    {
        model.Entity<DailyGameRankingPreference>(entity =>
        {
            entity.ToTable("daily_game_ranking_preferences");
            entity.HasKey(preference => preference.UserSub);
            entity.Property(preference => preference.UserSub).ValueGeneratedNever();
            entity.HasOne<ProfileName>().WithOne().HasForeignKey<DailyGameRankingPreference>(preference => preference.UserSub);
        });
    }
}
