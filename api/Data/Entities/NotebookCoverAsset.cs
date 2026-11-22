namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>An immutable, normalized WebP owned by exactly one coverable resource.</summary>
public sealed class NotebookCoverAsset
{
    public Guid Id { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? NotionPageId { get; set; }
    public Guid? NotebookId { get; set; }
    public Guid? NotebookPageId { get; set; }
    public byte[] Image { get; set; } = [];
}
