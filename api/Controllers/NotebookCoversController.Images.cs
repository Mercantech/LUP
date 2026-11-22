using System.Net;
using System.Text.Json;
using Amazon.S3;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class NotebookCoversController
{
    [HttpPost("generate-image")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting(NotebookCoverGenerator.RateLimitPolicy)]
    public async Task<IActionResult> GenerateImage(string kind, Guid id, GenerateNotebookImageRequest request, CancellationToken ct)
    {
        var failure = Guard(required: true);
        if (failure is not null) return failure;
        if (kind is not (NotionPage or NotebookPage)) return NotFound();
        var target = await FindEditable(kind, id, ct);
        if (target is null || kind == NotebookPage && target.NotebookId is null) return NotFound();
        if (!TryCharacter(request.CharacterSlug, out var character)) return BadRequest(new { error = "invalid_character" });
        var description = request.Description?.Trim() ?? "";
        if (description.Length > NotebookCoverGenerator.MaxIllustrationDescriptionLength)
            return BadRequest(new { error = "invalid_image_description" });
        if (!generator.IsConfigured) return StatusCode(503, new { error = "image_generation_unavailable" });
        if (storage?.IsConfigured != true) return StatusCode(503, new { error = "object_storage_disabled" });

        try
        {
            var source = await ReadSource(target, ct);
            var art = artwork is null ? null : await artwork.ResolveAsync(kind, target.Title, source, ct, character, description);
            var image = await generator.GenerateAsync(target.Title, source.Excerpts, ct, art, illustrationDescription: description);
            return await SavePageImage(target, image, art?.CharacterSlug, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return StatusCode(429, new { error = "image_generation_busy" });
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or AmazonS3Exception
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return StatusCode(502, new { error = "image_generation_failed" });
        }
    }

    private async Task<IActionResult> SavePageImage(CoverTarget target, byte[] image, string? character, CancellationToken ct)
    {
        var repository = storage!;
        var assetId = Guid.NewGuid();
        const string contentType = "image/webp";
        var teaching = target.Kind == NotionPage;
        var folder = teaching ? "notion-private" : "notebook-private";
        var key = $"{folder}/{target.Id:N}/{assetId:N}";
        var filename = character is null ? "ai-illustration.webp" : $"ai-illustration-{character}.webp";
        var subject = UserClaims.GetSub(User)!.Value;
        var now = DateTimeOffset.UtcNow;

        using var stream = new MemoryStream(image, writable: false);
        await repository.PutAsync(key, stream, contentType, ct);
        var committing = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (teaching)
                await NotebookPageTree.LockTeachingAsync(db, target.CollectionId, target.HoldId, ct);
            else if (target.NotebookId is Guid notebookId)
                await NotebookPageTree.LockPersonalAsync(db, notebookId, ct);

            var current = await FindEditable(target.Kind, target.Id, ct);
            if (current is null || current.LifecycleVersion != target.LifecycleVersion)
                return Conflict(new { error = "page_changed" });

            if (teaching)
                db.NotionAssets.Add(new NotionAsset
                {
                    Id = assetId, PageId = target.Id, Key = key, OriginalName = filename, ContentType = contentType,
                    Kind = "image", SizeBytes = image.Length, UploadedBySub = subject, CreatedAtUtc = now
                });
            else
                db.NotebookAssets.Add(new NotebookAsset
                {
                    Id = assetId, DocumentId = target.Id, Key = key, OriginalName = filename, ContentType = contentType,
                    Kind = "image", SizeBytes = image.Length, UploadedBySub = subject, CreatedAtUtc = now
                });

            if (character is not null)
                characterLibrary.RegisterPageImage(character, target.Kind, target.Id, assetId, image);

            await db.SaveChangesAsync(ct);
            ct.ThrowIfCancellationRequested();
            // Once commit starts, a lost acknowledgement must not delete a possibly referenced object.
            committing = true;
            await transaction.CommitAsync(CancellationToken.None);
        }
        finally
        {
            if (!committing)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                try { await repository.DeleteAsync(key, cleanup.Token); }
                catch (Exception ex) when (ex is HttpRequestException or IOException or AmazonS3Exception or OperationCanceledException) { }
            }
        }

        return Ok(new NotionAssetView(assetId, filename, contentType, "image", image.Length));
    }
}
