using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Api.References;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Notion;

/// <summary>Writes reader snapshots of saved pages under the caller's transaction and page lock.</summary>
internal sealed class NotionPublicationWriter(AppDbContext db, DrawingService? drawings)
{
    private static readonly byte[] EmptyDocument = [0, 0];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static Task<Guid?> WholePublisherAsync(AppDbContext db, Guid pageId, CancellationToken ct) =>
        ForWholeCollection(db, db.NotionPages.Where(p => p.Id == pageId))
            .Select(p => p.Collection!.WholePublishedBySub).SingleOrDefaultAsync(ct);

    internal static IQueryable<NotionPage> ForWholeCollection(AppDbContext db, IQueryable<NotionPage> pages) =>
        pages.Where(p => db.NotionCollections.Any(c => c.Id == p.CollectionId && c.WholePublishedBySub != null));

    internal static async Task SetInitialPublicationAsync(AppDbContext db, NotionPage page, CancellationToken ct)
    {
        if (page.CollectionId is not Guid book || await db.NotionCollections.Where(c => c.Id == book)
            .Select(c => c.WholePublishedBySub).SingleOrDefaultAsync(ct) is not Guid publisher) return;
        page.PublishedState = EmptyDocument;
        page.PublishedTitle = page.Title;
        page.PublishedAtUtc = page.CreatedAtUtc;
        page.PublishedRevision = page.DraftRevision;
        page.PublishedBySub = publisher;
        page.PublishedEditedAtUtc = page.UpdatedAtUtc;
        page.PublishedEditorName = page.LastEditedByName;
        page.PublicationVersion = 1;
    }

    internal async Task<string?> PublishSavedAsync(Guid id, Guid publisher, bool requireReadyDrawings, CancellationToken ct)
    {
        var page = await db.NotionPages.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.YjsState, p.HoldId, p.DraftKnowledgeChecksJson, p.PublishedDrawingsJson }).SingleAsync(ct);
        var state = page.YjsState ?? EmptyDocument;
        Guid[] assets, drawingIds;
        try { (assets, drawingIds) = YjsPageLinks.ReadPublicationMedia(state, id); }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or OverflowException)
        { return "invalid_publication_content"; }
        if (assets.Length > 1000 || (assets.Length > 0 && await db.NotionAssets.CountAsync(a => a.PageId == id && assets.Contains(a.Id), ct) != assets.Length))
            return "invalid_assets";
        if (!await KnowledgeCheckNotebookAccess.CanPublishAsync(db, page.HoldId, page.DraftKnowledgeChecksJson, ct))
            return "invalid_knowledge_check_publication";
        if (drawingIds.Length > 1000) return "invalid_drawing_publication";

        var previous = DrawingAccess.Bindings(page.PublishedDrawingsJson);
        var bindings = drawingIds.Length == 0 ? [] : drawings is not null
            ? await drawings.BindSavedPublicationAsync(id, drawingIds, previous, requireReadyDrawings, ct)
            : requireReadyDrawings ? throw new DrawingException(503, "drawing_service_unavailable", "Tegningerne kunne ikke udgives.") : previous;
        var now = DateTimeOffset.UtcNow;
        var references = new ReferenceService(db);
        await references.BaselineNotionAsync(id, ct);
        await db.NotionPages.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PublishedState, state).SetProperty(p => p.PublishedTitle, p => p.Title)
            .SetProperty(p => p.PublishedIconEmojiId, p => p.IconEmojiId).SetProperty(p => p.PublishedCoverId, p => p.CoverId)
            .SetProperty(p => p.PublishedAtUtc, now).SetProperty(p => p.PublishedBySub, publisher)
            .SetProperty(p => p.PublishedEditorName, p => p.LastEditedByName).SetProperty(p => p.PublishedEditedAtUtc, p => p.UpdatedAtUtc)
            .SetProperty(p => p.PublishedRevision, p => p.DraftRevision).SetProperty(p => p.PublicationVersion, p => p.PublicationVersion + 1)
            .SetProperty(p => p.PublishedAssetIdsJson, JsonSerializer.Serialize(assets))
            .SetProperty(p => p.PublishedDrawingsJson, JsonSerializer.Serialize(bindings, Json))
            .SetProperty(p => p.PublishedKnowledgeChecksJson, p => p.DraftKnowledgeChecksJson), ct);
        await db.DeleteUnreferencedNotionCoversAsync(id, ct);
        await references.RefreshNotionAsync(id, ct, published: true);
        return null;
    }

    internal static Task<int> ClearAsync(IQueryable<NotionPage> pages, CancellationToken ct) =>
        pages.ExecuteUpdateAsync(s => s
            .SetProperty(p => p.PublishedState, (byte[]?)null).SetProperty(p => p.PublishedTitle, (string?)null)
            .SetProperty(p => p.PublishedIconEmojiId, (Guid?)null).SetProperty(p => p.PublishedCoverId, (Guid?)null)
            .SetProperty(p => p.PublishedAtUtc, (DateTimeOffset?)null).SetProperty(p => p.PublishedBySub, (Guid?)null)
            .SetProperty(p => p.PublishedEditorName, (string?)null).SetProperty(p => p.PublishedEditedAtUtc, (DateTimeOffset?)null)
            .SetProperty(p => p.PublishedRevision, (long?)null).SetProperty(p => p.PublishedAssetIdsJson, "[]")
            .SetProperty(p => p.PublishedDrawingsJson, "[]").SetProperty(p => p.PublishedKnowledgeChecksJson, "{}")
            .SetProperty(p => p.PublicationVersion, p => p.PublicationVersion + 1), ct);
}
