using System.Text.Json.Serialization;

namespace Mercantec.Tech.Contracts;

public sealed record NotebookCoverView(Guid? CoverId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte[]? Image = null);

public static class NotebookCoverRules
{
    public const int MaxUploadBytes = 5 * 1024 * 1024;
    public const string SubjectHeader = "X-LUP-Expected-Subject";
}
