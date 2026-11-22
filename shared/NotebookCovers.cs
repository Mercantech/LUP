namespace Mercantec.Tech.Contracts;

public sealed record NotebookCoverView(Guid? CoverId);

public static class NotebookCoverRules
{
    public const int MaxUploadBytes = 5 * 1024 * 1024;
    public const string SubjectHeader = "X-LUP-Expected-Subject";
}
