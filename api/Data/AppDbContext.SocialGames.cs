using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<SocialGameRoom> SocialGameRooms => Set<SocialGameRoom>();

    private static void ConfigureSocialGames(ModelBuilder model)
    {
        model.Entity<SocialGameRoom>(entity =>
        {
            entity.ToTable("social_game_rooms");
            entity.Property(room => room.Code).HasMaxLength(6);
            entity.Property(room => room.Revision).IsConcurrencyToken();
            entity.HasIndex(room => room.Code).IsUnique();
            entity.HasIndex(room => room.NextDeadlineUtc);
            entity.HasIndex(room => room.ExpiresAtUtc);
        });
    }
}
