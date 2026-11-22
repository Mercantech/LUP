namespace Mercantec.Tech.Contracts;

public sealed record NotionCollectionView(Guid Id, Guid? RootPageId, string Title, Guid? HoldId,
    string? HoldName, bool CanManage, DateTimeOffset UpdatedAtUtc,
    Guid? IconEmojiId = null, string? LastEditedByName = null, string? Slug = null, Guid? CoverId = null,
    bool WholePublished = false);
public sealed record CreateNotionCollection(string Title, Guid? HoldId = null, Guid? IconEmojiId = null, string? Slug = null);
public sealed record SetNotionCollectionSlug(string? Slug);
public sealed record SetNotebookIcon(Guid? IconEmojiId);
public sealed record NotionCollectionPublicationResult(int PageCount, bool WholePublished);
