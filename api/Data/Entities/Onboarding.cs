namespace Mercantec.Tech.Api.Data.Entities;

public sealed class OnboardingStep
{
    public const string Student = "student";
    public const string Teacher = "teacher";

    public int Id { get; set; }
    public string Audience { get; set; } = "";
    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class OnboardingDismissal
{
    public Guid UserSub { get; set; }
}
