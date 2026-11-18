using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<Elective> Electives => Set<Elective>();
    public DbSet<ElectiveEnrollment> ElectiveEnrollments => Set<ElectiveEnrollment>();

    // Serialize roster changes across application instances, including account linking.
    public Task<int> LockElectiveAsync(Guid id, CancellationToken ct) =>
        Electives.Where(e => e.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Version, e => e.Version), ct);

    private static void ConfigureElectives(ModelBuilder model)
    {
        model.Entity<Elective>(e =>
        {
            e.ToTable("electives", table =>
            {
                table.HasCheckConstraint("CK_electives_status", "\"Status\" IN ('template', 'draft', 'open', 'closed', 'archived')");
                table.HasCheckConstraint("CK_electives_seats", "\"ReservedSeats\" >= 0 AND (\"Capacity\" IS NULL OR (\"Capacity\" > 0 AND \"Capacity\" >= \"ReservedSeats\"))");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(20000).IsRequired();
            e.Property(x => x.Schedule).HasMaxLength(8000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(12).IsRequired();
            e.Property(x => x.SourceUrl).HasMaxLength(2048);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Elective>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ElectiveEnrollment>(e =>
        {
            e.ToTable("elective_enrollments", table =>
                table.HasCheckConstraint("CK_elective_enrollments_identity", "\"UserSub\" IS NOT NULL OR \"LectioId\" IS NOT NULL"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.LectioId).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.UserSub);
            e.HasIndex(x => x.LectioId);
            e.HasIndex(x => new { x.ElectiveId, x.UserSub }).IsUnique();
            e.HasIndex(x => new { x.ElectiveId, x.LectioId }).IsUnique();
            e.HasOne(x => x.Elective).WithMany(x => x.Enrollments).HasForeignKey(x => x.ElectiveId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
