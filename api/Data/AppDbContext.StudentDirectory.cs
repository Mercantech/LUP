using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<StudentDirectoryDetails> StudentDirectoryDetails => Set<StudentDirectoryDetails>();

    private static void ConfigureStudentDirectory(ModelBuilder model)
    {
        model.Entity<StudentDirectoryDetails>(e =>
        {
            e.ToTable("student_directory_details");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(80);
            e.Property(x => x.LectioId).HasMaxLength(64);
            e.Property(x => x.Placement).HasMaxLength(12);
            e.Property(x => x.CurrentSchoolPeriod).HasMaxLength(160);
            e.Property(x => x.WorkplaceName).HasMaxLength(200);
            e.Property(x => x.LastPassedSchoolPeriod).HasMaxLength(120);
            e.HasIndex(x => x.LectioId);
            e.HasIndex(x => x.UserSub);
        });
    }
}
