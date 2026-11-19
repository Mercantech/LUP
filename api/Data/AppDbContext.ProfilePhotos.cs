using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<ProfilePhoto> ProfilePhotos => Set<ProfilePhoto>();

    private static void ConfigureProfilePhotos(ModelBuilder model)
    {
        model.Entity<ProfilePhoto>(e =>
        {
            e.ToTable("profile_photos");
            e.HasKey(x => x.UserSub);
            e.Property(x => x.UserSub).ValueGeneratedNever();
            e.Property(x => x.StorageKey).HasMaxLength(200).IsRequired().IsConcurrencyToken();
            e.Property(x => x.ContentType).HasMaxLength(64).IsRequired();
            // Optimistic concurrency also covers replacing and removing the same photo in two tabs.
        });
    }
}
