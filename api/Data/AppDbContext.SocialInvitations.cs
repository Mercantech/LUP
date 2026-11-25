using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<SocialGameInvitation> SocialGameInvitations => Set<SocialGameInvitation>();

    private static void ConfigureSocialInvitations(ModelBuilder modelBuilder)
    {
        var invite = modelBuilder.Entity<SocialGameInvitation>();
        invite.HasKey(i => i.Id);
        invite.HasIndex(i => new { i.RoomId, i.RecipientSub }).IsUnique();
        invite.HasIndex(i => new { i.RecipientSub, i.ReadAtUtc });
        invite.Property(i => i.SenderName).HasMaxLength(200);
        invite.HasOne(i => i.Room).WithMany().HasForeignKey(i => i.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}
