using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Drawings;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Api.References;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class NotionCollectionsController
{
    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> PublishCollection(Guid id, CancellationToken ct) => SetCollectionPublication(id, true, ct);

    [HttpPost("{id:guid}/unpublish")]
    public Task<IActionResult> UnpublishCollection(Guid id, CancellationToken ct) => SetCollectionPublication(id, false, ct);

    private async Task<IActionResult> SetCollectionPublication(Guid id, bool publish, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var collection = await access.ManageableCollections(User).AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await NotebookPageTree.LockTeachingAsync(db, id, collection.HoldId, ct);

        var scope = db.NotionCollections.Where(c => c.Id == id);
        var publisher = await scope.Select(c => c.WholePublishedBySub).SingleAsync(ct);
        var active = db.NotionPages.Where(p => p.CollectionId == id);
        var pages = await active.OrderBy(p => p.Id).Select(p => new { p.Id, p.Title }).ToArrayAsync(ct);
        if ((publisher is not null) == publish) return Ok(new NotionCollectionPublicationResult(pages.Length, publish));
        if (publish && pages.Length == 0) return Conflict(new { error = "empty_collection" });

        var writer = new NotionPublicationWriter(db, drawings);
        var references = new ReferenceService(db);
        foreach (var page in pages)
        {
            // Take the page lock before reading its content so saves during publication are included.
            await active.Where(p => p.Id == page.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.PublicationVersion, p => p.PublicationVersion), ct);
            if (!publish)
            {
                await references.BaselineNotionAsync(page.Id, ct);
                await NotionPublicationWriter.ClearAsync(active.Where(p => p.Id == page.Id), ct);
                await db.DeleteUnreferencedNotionCoversAsync(page.Id, ct);
                await references.RefreshNotionAsync(page.Id, ct, published: true);
                continue;
            }
            try
            {
                var error = await writer.PublishSavedAsync(page.Id, sub, requireReadyDrawings: true, ct);
                if (error is not null) return Conflict(new { error, pageTitle = page.Title });
            }
            catch (DrawingException ex)
            { return StatusCode(ex.Status, new { error = ex.Code, message = ex.Message, pageTitle = page.Title }); }
            catch (HttpRequestException)
            { return StatusCode(503, new { error = "drawing_service_unavailable", pageTitle = page.Title }); }
        }
        if (!publish)
            await NotionPublicationWriter.ClearAsync(db.NotionPages.IgnoreQueryFilters().Where(p => p.CollectionId == id
                && p.DeletedAtUtc != null && p.PublishedAtUtc != null), ct);

        await scope.ExecuteUpdateAsync(s => s.SetProperty(c => c.WholePublishedBySub, publish ? sub : (Guid?)null)
            .SetProperty(c => c.PublishedTitle, c => publish ? c.Title : null), ct);
        await transaction.CommitAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new NotionCollectionPublicationResult(pages.Length, publish));
    }
}
