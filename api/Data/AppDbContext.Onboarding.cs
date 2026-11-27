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
                },
                new OnboardingStep
                {
                    Id = -3,
                    Audience = OnboardingStep.Student,
                    SortOrder = 2,
                    Title = "Find dine opgaver",
                    Text = "Åbn et hold og vælg Quests og afleveringer. Her kan du læse opgaven og aflevere dit arbejde. Kig i Mine handlinger for at følge op på feedback."
                },
                new OnboardingStep
                {
                    Id = -4,
                    Audience = OnboardingStep.Student,
                    SortOrder = 3,
                    Title = "Saml dine noter",
                    Text = "Åbn Din notesbog fra forsiden for at samle faglige noter. Brug Skriv logbog til at fortælle, hvad du har arbejdet med, og hvad du har lært."
                },
                new OnboardingStep
                {
                    Id = -5,
                    Audience = OnboardingStep.Student,
                    SortOrder = 4,
                    Title = "Gør LUP til dit eget",
                    Text = "Brug Favoritter på forsiden til dine vigtigste sider. I avatarmenuen finder du din profil og Hjælp, når du har brug for at finde vej igen."
                },
                new OnboardingStep
                {
                    Id = -6,
                    Audience = OnboardingStep.Teacher,
                    SortOrder = 2,
                    Title = "Planlæg og del undervisning",
                    Text = "Åbn et hold for at finde forløb, quests, materialer og kalender. I Notesbøger og forløbseditor kan du skrive og udgive undervisningssider."
                },
                new OnboardingStep
                {
                    Id = -7,
                    Audience = OnboardingStep.Teacher,
                    SortOrder = 3,
                    Title = "Følg elevernes arbejde",
                    Text = "Brug Til opfølgning til at finde afleveringer, der venter på feedback. I Elevnotesbøger kan du læse og kommentere elevernes noter og logbøger."
                },
                new OnboardingStep
                {
                    Id = -8,
                    Audience = OnboardingStep.Teacher,
                    SortOrder = 4,
                    Title = "Find værktøjerne igen",
                    Text = "Åbn Underviserværktøjer fra avatarmenuen for at finde kalender, afstemninger og videnstjek. Her kan du også genåbne denne guide og se elevernes onboarding."
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
