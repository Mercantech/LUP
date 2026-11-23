using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Data;

public partial class AppDbContext
{
    public DbSet<OnboardingStep> OnboardingSteps => Set<OnboardingStep>();
    public DbSet<OnboardingDismissal> OnboardingDismissals => Set<OnboardingDismissal>();

    private static void ConfigureOnboarding(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OnboardingStep>(step =>
        {
            step.ToTable("onboarding_steps");
            step.HasKey(x => x.Id);
            step.Property(x => x.Audience).HasMaxLength(12).IsRequired();
            step.Property(x => x.Title).HasMaxLength(120).IsRequired();
            step.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            step.HasData(
                new OnboardingStep
                {
                    Id = -1,
                    Audience = OnboardingStep.Student,
                    SortOrder = 1,
                    Title = "Velkommen til LUP",
                    Text = "Start i Mine hold. Her finder du dine forløb, quests og afleveringer. Under Mine handlinger kan du se, hvad der venter på dig."
                },
                new OnboardingStep
                {
                    Id = -2,
                    Audience = OnboardingStep.Teacher,
                    SortOrder = 1,
                    Title = "Velkommen til LUP",
                    Text = "Start i Mine hold for at åbne undervisning, quests og afstemninger. Under Til opfølgning finder du afleveringer, der venter på feedback."
                });
        });

        modelBuilder.Entity<OnboardingDismissal>(dismissal =>
        {
            dismissal.ToTable("onboarding_dismissals");
            dismissal.HasKey(x => x.UserSub);
            dismissal.Property(x => x.UserSub).ValueGeneratedNever();
        });
    }
}
