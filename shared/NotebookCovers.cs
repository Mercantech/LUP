using System.Text.Json.Serialization;

namespace Mercantec.Tech.Contracts;

public sealed record NotebookCoverView(Guid? CoverId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte[]? Image = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CharacterSlug = null,
    int PositionY = NotebookCoverRules.DefaultPositionY);

public sealed record RepositionNotebookCoverRequest(Guid CoverId, int PositionY);

public sealed record GenerateNotebookImageRequest(string? Description = null, string? CharacterSlug = null);

public static class NotebookCoverRules
{
    public const int MaxUploadBytes = 5 * 1024 * 1024;
    public const string SubjectHeader = "X-LUP-Expected-Subject";
    public const string PositionHeader = "X-LUP-Cover-Position-Y";
    public const int DefaultPositionY = 50;
}
