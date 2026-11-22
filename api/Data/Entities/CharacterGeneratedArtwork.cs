namespace Mercantec.Tech.Api.Data.Entities;

public sealed class CharacterGeneratedArtwork
{
    public Guid Id { get; set; }
    public string CharacterSlug { get; set; } = "";
    public string Kind { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public Guid OriginalAssetId { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? NotionPageId { get; set; }
    public Guid? NotebookId { get; set; }
    public Guid? NotebookPageId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] Image { get; set; } = [];
    public byte[] Thumbnail { get; set; } = [];
}
