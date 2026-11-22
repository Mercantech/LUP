namespace Mercantec.Tech.Contracts;

public sealed record CharacterGeneratedArtItem(Guid Id, string CharacterSlug, string Kind, string SourceKind,
    Guid SourceId, string NotebookTitle, string? PageTitle, string SourceHref, DateTimeOffset CreatedAtUtc, string ImageUrl);

public sealed record CharacterGeneratedArtPage(CharacterGeneratedArtItem[] Items, int? NextOffset);
