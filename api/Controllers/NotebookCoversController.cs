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
    NotionAiProjectionClient projection, INotebookCoverGenerator generator, NotebookCoverArtwork? artwork = null) : ControllerBase
{
    private const string Collection = "collection";
    private const string NotionPage = "notion-page";
    private const string Notebook = "notebook";
    private const string NotebookPage = "notebook-page";
    private const int MaxSourcePages = 4;
    private const int MaxSourceStateBytes = 1_500_000;
    private const int MaxExcerptLength = 500;
    private const int MaxUploadRequestBytes = NotebookCoverRules.MaxUploadBytes + 64 * 1024;
    private static readonly TimeSpan ExcerptProjectionTimeout = TimeSpan.FromSeconds(6);
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
        CancellationToken ct, [FromQuery] bool includeImage = false)
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
        return image is null ? BadRequest(new { error = "invalid_cover_image" }) : await Save(target, image, ct, includeImage);
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
    public async Task<IActionResult> Generate(string kind, Guid id, CancellationToken ct, [FromQuery] bool includeImage = false)
    {
        var failure = Guard(required: true);
        if (failure is not null) return failure;
        var target = await FindEditable(kind, id, ct);
        if (target is null) return NotFound();
        if (!generator.IsConfigured) return StatusCode(503, new { error = "cover_generation_unavailable" });

        try
        {
            var source = await ReadSource(target, ct);
            var art = artwork is null ? null : await artwork.ResolveAsync(target.Kind, target.Title, source, ct);
            ct.ThrowIfCancellationRequested();
            var image = await generator.GenerateAsync(target.Title, source.Excerpts, ct, art);
            return await Save(target, image, ct, includeImage);
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
                        ?? c.Title ?? "Undervisningsnoter", 0, c.IconEmojiId, c.HoldId)).SingleOrDefaultAsync(ct),
            NotionPage => editablePages.Where(p => p.Id == id)
                .Select(p => new CoverTarget(kind, p.Id, p.CoverId, p.Title, p.LifecycleVersion, p.IconEmojiId, p.HoldId)).SingleOrDefaultAsync(ct),
            Notebook => OwnedNotebooks(id)
                .Select(n => new CoverTarget(kind, n.Id, n.CoverId, "Min notesbog", 0, n.IconEmojiId, null)).SingleOrDefaultAsync(ct),
            NotebookPage => notebooks.EditableDocuments(User).Where(d => d.Id == id)
                .Select(d => new CoverTarget(kind, d.Id, d.CoverId, d.Title, d.LifecycleVersion, d.IconEmojiId, d.HoldId)).SingleOrDefaultAsync(ct),
            _ => Task.FromResult<CoverTarget?>(null)
        };
    }

    private async Task<IActionResult> Save(CoverTarget target, byte[]? image, CancellationToken ct, bool includeImage = false)
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
        return Ok(new NotebookCoverView(coverId, includeImage ? image : null));
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

    private async Task<NotebookCoverSource> ReadSource(CoverTarget target, CancellationToken ct)
    {
        var includePageTitles = target.Kind is Collection or Notebook;
        CoverDocument[] documents;
        if (target.Kind is Collection or NotionPage)
        {
            var pages = notion.ManageablePages(User).AsNoTracking();
            pages = includePageTitles ? pages.Where(p => p.CollectionId == target.Id) : pages.Where(p => p.Id == target.Id);
            documents = await pages.OrderByDescending(p => p.Id == target.Id).ThenBy(p => p.ParentPageId != null)
                .ThenBy(p => p.SortOrder ?? int.MaxValue).ThenBy(p => p.Id).Take(MaxSourcePages)
                .Select(p => new CoverDocument(p.Title, p.IconEmojiId, null,
                    p.YjsState != null && p.YjsState.Length <= MaxSourceStateBytes ? p.YjsState : null)).ToArrayAsync(ct);
        }
        else
        {
            var notes = notebooks.EditableDocuments(User).AsNoTracking();
            // An overall notebook cover samples ordinary notes. Dedicated journal covers use
            // their own text only when the owner chooses to generate one explicitly.
            notes = includePageTitles
                ? notes.Where(d => d.NotebookId == target.Id && d.Kind == "note")
                : notes.Where(d => d.Id == target.Id);
            documents = await notes.OrderBy(d => d.ParentPageId != null).ThenBy(d => d.SortOrder ?? int.MaxValue)
                .ThenBy(d => d.Id).Take(MaxSourcePages)
                .Select(d => new CoverDocument(d.Title, d.IconEmojiId, d.SearchText.Substring(0, NotebookCoverEmojiSignals.MaxTextLength),
                    d.YjsState != null && d.YjsState.Length <= MaxSourceStateBytes ? d.YjsState : null)).ToArrayAsync(ct);
        }

        string? courseTitle = null;
        if (target.HoldId is Guid holdId)
            courseTitle = await courses.AccessibleHolds(User).Where(hold => hold.Id == holdId)
                .Select(hold => hold.Forloeb.Name).SingleOrDefaultAsync(ct);

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(ExcerptProjectionTimeout);
        var projections = await Task.WhenAll(documents.Select(doc => ReadCoverProjection(doc.State, budget.Token, ct)));
        var excerpts = new List<string>(documents.Length);
        var emojis = new List<NotebookCoverEmoji>();
        if (target.IconEmojiId is Guid icon) emojis.Add(new(icon, null));
        emojis.AddRange(NotebookCoverEmojiSignals.InText(target.Title));

        foreach (var (document, content) in documents.Zip(projections))
        {
            var text = document.SearchText is { Length: > 0 } savedText ? savedText : content?.PlainText ?? "";
            excerpts.Add(Excerpt(includePageTitles ? document.Title : null, text));
            if (document.IconEmojiId is Guid pageIcon) emojis.Add(new(pageIcon, null));
            emojis.AddRange(NotebookCoverEmojiSignals.InText(document.Title));
            emojis.AddRange(content?.Emojis ?? NotebookCoverEmojiSignals.InText(text));
        }

        return new(excerpts.Where(part => part.Length > 0).Distinct(StringComparer.Ordinal).ToArray(),
            NotebookCoverEmojiSignals.Unique(emojis), courseTitle);
    }

    private async Task<NotebookCoverProjection?> ReadCoverProjection(byte[]? state, CancellationToken budget, CancellationToken request)
    {
        if (state is not { Length: > 0 }) return null;
        try { return await projection.ProjectCoverAsync(state, budget); }
        catch (Exception ex) when (!request.IsCancellationRequested &&
            ex is HttpRequestException or JsonException or InvalidDataException or OperationCanceledException)
        {
            // Other saved sources and personal search text remain available during a projection failure.
            return null;
        }
    }

    private static string Excerpt(string? title, string text)
    {
        var source = title is null ? text : $"{title}\n{text}";
        var sliced = source[..Math.Min(source.Length, MaxExcerptLength)];
        if (sliced.Length > 0 && char.IsHighSurrogate(sliced[^1])) sliced = sliced[..^1];
        return string.Join(' ', sliced.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record CoverTarget(string Kind, Guid Id, Guid? CoverId, string Title, long LifecycleVersion,
        Guid? IconEmojiId, Guid? HoldId);
    private sealed record CoverDocument(string Title, Guid? IconEmojiId, string? SearchText, byte[]? State);
}
