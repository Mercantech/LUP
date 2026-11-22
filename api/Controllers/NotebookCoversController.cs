using System.Net;
using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Notion;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notebook-covers/{kind}/{id:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotebookCoversController(AppDbContext db, CourseAccess courses, NotebookAccess notebooks,
    NotionAiProjectionClient projection, INotebookCoverGenerator generator) : ControllerBase
{
    private const string Collection = "collection";
    private const string NotionPage = "notion-page";
    private const string Notebook = "notebook";
    private const string NotebookPage = "notebook-page";
    private const int MaxSourcePages = 4;
    private const int MaxSourceStateBytes = 1_500_000;
    private const int MaxExcerptLength = 500;
    private const int MaxUploadRequestBytes = NotebookCoverRules.MaxUploadBytes + 64 * 1024;
    private readonly NotionAccess notion = new(db, courses);

    [HttpGet]
    public async Task<IActionResult> Get(string kind, Guid id, CancellationToken ct, bool publishedOnly = false,
        Guid? version = null)
    {
        var failure = Guard(required: false);
        if (failure is not null) return failure;

        var visible = kind switch
        {
            Collection => notion.ReadableCollections(User, publishedOnly).Where(c => c.Id == id).Select(c => c.CoverId),
            NotionPage => ReadableNotionCover(id, publishedOnly),
            Notebook => notebooks.ReadableWholeNotebooks(User).Where(n => n.Id == id).Select(n => n.CoverId),
            NotebookPage => notebooks.ReadableDocuments(User).Where(d => d.Id == id).Select(d => d.CoverId),
            _ => null
        };
        if (visible is null) return NotFound();

        // Access, version selection and byte read form a single query. Even a known draft
        // version cannot be used to read unpublished content via a reader's URL.
        var image = await OwnedAssets(kind, id).Where(asset => (version == null || asset.Id == version)
            && visible.Contains(asset.Id)).Select(asset => asset.Image).SingleOrDefaultAsync(ct);
        if (image is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(image, "image/webp");
    }

    [HttpPut]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    public async Task<IActionResult> Upload(string kind, Guid id, [FromForm(Name = "file")] IFormFile? file,
        CancellationToken ct)
    {
        var failure = Guard(required: true);
        if (failure is not null) return failure;
        var target = await FindEditable(kind, id, ct);
        if (target is null) return NotFound();
        if (file is null || file.Length == 0) return BadRequest(new { error = "invalid_cover_image" });
        if (file.Length > NotebookCoverRules.MaxUploadBytes) return StatusCode(413, new { error = "cover_too_large" });

        using var upload = new MemoryStream((int)file.Length);
        await file.CopyToAsync(upload, ct);
        var image = NotebookCoverImage.Normalize(upload.ToArray());
        return image is null ? BadRequest(new { error = "invalid_cover_image" }) : await Save(target, image, ct);
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(string kind, Guid id, CancellationToken ct)
    {
        var failure = Guard(required: true);
        if (failure is not null) return failure;
        var target = await FindEditable(kind, id, ct);
        if (target is null) return NotFound();
        return target.CoverId is null ? Ok(new NotebookCoverView(null)) : await Save(target, null, ct);
    }

    [HttpPost("generate")]
    [EnableRateLimiting(NotebookCoverGenerator.RateLimitPolicy)]
    public async Task<IActionResult> Generate(string kind, Guid id, CancellationToken ct)
    {
        var failure = Guard(required: true);
        if (failure is not null) return failure;
        var target = await FindEditable(kind, id, ct);
        if (target is null) return NotFound();
        if (!generator.IsConfigured) return StatusCode(503, new { error = "cover_generation_unavailable" });

        try
        {
            var excerpts = await ReadExcerpts(target, ct);
            var image = await generator.GenerateAsync(target.Title, excerpts, ct);
            return await Save(target, image, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return StatusCode(429, new { error = "cover_generation_busy" });
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or JsonException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return StatusCode(502, new { error = "cover_generation_failed" });
        }
    }

    private IActionResult? Guard(bool required)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        var hasHeader = Request.Headers.TryGetValue(NotebookCoverRules.SubjectHeader, out var header);
        if (hasHeader ? Guid.TryParse(header.ToString(), out var expected) && expected == subject : !required)
            return null;
        return Conflict(new { error = "account_changed" });
    }

    private IQueryable<Guid?> ReadableNotionCover(Guid id, bool publishedOnly)
    {
        var pages = notion.ReadablePages(User, publishedOnly).Where(p => p.Id == id);
        if (publishedOnly) return pages.Select(p => p.PublishedCoverId);
        var manageable = notion.ManageablePages(User).Select(p => p.Id);
        return pages.Select(p => manageable.Contains(p.Id) ? p.CoverId : p.PublishedCoverId);
    }

    private IQueryable<NotebookCoverAsset> OwnedAssets(string kind, Guid id) =>
        kind switch
        {
            Collection => db.NotebookCoverAssets.Where(asset => asset.CollectionId == id),
            NotionPage => db.NotebookCoverAssets.Where(asset => asset.NotionPageId == id),
            Notebook => db.NotebookCoverAssets.Where(asset => asset.NotebookId == id),
            NotebookPage => db.NotebookCoverAssets.Where(asset => asset.NotebookPageId == id),
            _ => db.NotebookCoverAssets.Where(asset => false)
        };

    private IQueryable<Notebook> OwnedNotebooks(Guid id)
    {
        var subject = UserClaims.GetSub(User);
        return db.Notebooks.Where(n => n.Id == id && n.OwnerSub == subject);
    }

    private Task<CoverTarget?> FindEditable(string kind, Guid id, CancellationToken ct)
    {
        var editablePages = notion.ManageablePages(User);
        return kind switch
        {
            Collection => notion.ManageableCollections(User).Where(c => c.Id == id)
                .Select(c => new CoverTarget(kind, c.Id, c.CoverId,
                    editablePages.Where(p => p.Id == c.Id).Select(p => p.Title).FirstOrDefault()
                        ?? c.Title ?? "Undervisningsnoter", 0)).SingleOrDefaultAsync(ct),
            NotionPage => editablePages.Where(p => p.Id == id)
                .Select(p => new CoverTarget(kind, p.Id, p.CoverId, p.Title, p.LifecycleVersion)).SingleOrDefaultAsync(ct),
            Notebook => OwnedNotebooks(id)
                .Select(n => new CoverTarget(kind, n.Id, n.CoverId, "Min notesbog", 0)).SingleOrDefaultAsync(ct),
            NotebookPage => notebooks.EditableDocuments(User).Where(d => d.Id == id)
                .Select(d => new CoverTarget(kind, d.Id, d.CoverId, d.Title, d.LifecycleVersion)).SingleOrDefaultAsync(ct),
            _ => Task.FromResult<CoverTarget?>(null)
        };
    }

    private async Task<IActionResult> Save(CoverTarget target, byte[]? image, CancellationToken ct)
    {
        Guid? coverId = image is null ? null : Guid.NewGuid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await SetCover(target, coverId, ct);
        if (changed != 1) return Conflict(new { error = "cover_changed" });

        if (image is not null)
        {
            db.NotebookCoverAssets.Add(new NotebookCoverAsset
            {
                Id = coverId!.Value,
                CollectionId = target.Kind == Collection ? target.Id : null,
                NotionPageId = target.Kind == NotionPage ? target.Id : null,
                NotebookId = target.Kind == Notebook ? target.Id : null,
                NotebookPageId = target.Kind == NotebookPage ? target.Id : null,
                Image = image
            });
            await db.SaveChangesAsync(ct);
        }

        if (target.Kind == NotionPage)
            await db.DeleteUnreferencedNotionCoversAsync(target.Id, ct);
        else if (target.CoverId is Guid previous)
            await OwnedAssets(target.Kind, target.Id).Where(asset => asset.Id == previous).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
        return Ok(new NotebookCoverView(coverId));
    }

    private Task<int> SetCover(CoverTarget target, Guid? newCoverId, CancellationToken ct)
    {
        var editor = UserClaims.EditorName(User);
        var now = DateTimeOffset.UtcNow;
        return target.Kind switch
        {
            Collection => notion.ManageableCollections(User).Where(c => c.Id == target.Id && c.CoverId == target.CoverId)
                .ExecuteUpdateAsync(update => update.SetProperty(c => c.CoverId, newCoverId), ct),
            NotionPage => notion.ManageablePages(User).Where(p => p.Id == target.Id && p.CoverId == target.CoverId
                && p.LifecycleVersion == target.LifecycleVersion)
                .ExecuteUpdateAsync(update => update.SetProperty(p => p.CoverId, newCoverId)
                    .SetProperty(p => p.LastEditedByName, editor).SetProperty(p => p.UpdatedAtUtc, now), ct),
            Notebook => OwnedNotebooks(target.Id).Where(n => n.CoverId == target.CoverId)
                .ExecuteUpdateAsync(update => update.SetProperty(n => n.CoverId, newCoverId), ct),
            NotebookPage => notebooks.EditableDocuments(User).Where(d => d.Id == target.Id && d.CoverId == target.CoverId
                && d.LifecycleVersion == target.LifecycleVersion)
                .ExecuteUpdateAsync(update => update.SetProperty(d => d.CoverId, newCoverId)
                    .SetProperty(d => d.LastEditedByName, editor).SetProperty(d => d.UpdatedAtUtc, now), ct),
            _ => Task.FromResult(0)
        };
    }

    private async Task<string[]> ReadExcerpts(CoverTarget target, CancellationToken ct)
    {
        var parts = new List<string>();
        switch (target.Kind)
        {
            case Collection:
            case NotionPage:
                var pages = notion.ManageablePages(User).AsNoTracking().Where(p => p.YjsState != null
                    && p.YjsState.Length <= MaxSourceStateBytes);
                pages = target.Kind == Collection ? pages.Where(p => p.CollectionId == target.Id) : pages.Where(p => p.Id == target.Id);
                var notionPages = await pages.OrderByDescending(p => p.Id == target.Id).ThenBy(p => p.ParentPageId != null)
                    .ThenBy(p => p.SortOrder ?? int.MaxValue).ThenBy(p => p.Id).Take(MaxSourcePages)
                    .Select(p => new { p.Title, State = p.YjsState! }).ToArrayAsync(ct);
                foreach (var page in notionPages)
                {
                    if (page.State.Length == 0) continue;
                    var content = await projection.ProjectAsync(page.State, ct);
                    if (target.Kind == Collection) parts.Add(Excerpt(page.Title, content.PlainText));
                    else parts.Add(Excerpt(null, content.PlainText));
                }
                break;
            case Notebook:
            case NotebookPage:
                var documents = notebooks.EditableDocuments(User).AsNoTracking();
                // An overall notebook cover samples ordinary notes. Dedicated journal covers
                // can use their own text when the owner chooses to generate one explicitly.
                documents = target.Kind == Notebook
                    ? documents.Where(d => d.NotebookId == target.Id && d.Kind == "note")
                    : documents.Where(d => d.Id == target.Id);
                var notes = await documents.OrderBy(d => d.ParentPageId != null).ThenBy(d => d.SortOrder ?? int.MaxValue)
                    .ThenBy(d => d.Id).Take(MaxSourcePages)
                    .Select(d => new { d.Title, Text = d.SearchText.Substring(0, MaxExcerptLength) }).ToArrayAsync(ct);
                foreach (var note in notes)
                    parts.Add(Excerpt(target.Kind == Notebook ? note.Title : null, note.Text));
                break;
        }

        return parts.Where(part => part.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string Excerpt(string? title, string text)
    {
        var source = title is null ? text : $"{title}\n{text}";
        var sliced = source[..Math.Min(source.Length, MaxExcerptLength)];
        if (sliced.Length > 0 && char.IsHighSurrogate(sliced[^1])) sliced = sliced[..^1];
        return string.Join(' ', sliced.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record CoverTarget(string Kind, Guid Id, Guid? CoverId, string Title, long LifecycleVersion);
}
