namespace Mercantec.Tech.Contracts;

public sealed record OnboardingStepDto(string Title, string Text);
public sealed record DismissOnboardingRequest(Guid ExpectedSubject);
