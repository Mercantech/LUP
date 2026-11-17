using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<HoldCalendarEvent> HoldCalendarEvents => Set<HoldCalendarEvent>();
    public DbSet<HoldCalendarChange> HoldCalendarChanges => Set<HoldCalendarChange>();

    private static void ConfigureHoldCalendar(ModelBuilder model)
    {
        model.Entity<HoldCalendarEvent>(e =>
        {
            e.ToTable("hold_calendar_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Location).HasMaxLength(200).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.Property(x => x.AttendanceMode).HasMaxLength(16);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.HoldId, x.StartUtc });
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<LearningQuest>().WithMany().HasForeignKey(x => x.QuestId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<HoldCalendarChange>(e =>
        {
            e.ToTable("hold_calendar_changes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.HasIndex(x => new { x.HoldId, x.ChangedAtUtc });
            e.HasIndex(x => new { x.EventId, x.EventVersion });
            e.HasOne<Hold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
