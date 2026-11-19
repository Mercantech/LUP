using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Api.Students;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize]
public sealed class ProfilePhotosController(AppDbContext db, IObjectStorage storage,
    ILogger<ProfilePhotosController> logger) : ControllerBase
{
    [HttpGet("api/me/profile-photo")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var photo = await db.ProfilePhotos.AsNoTracking().SingleOrDefaultAsync(p => p.UserSub == sub, ct);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new ProfilePhotoView(photo is not null, photo?.UpdatedAtUtc));
    }

    [HttpGet("api/student-photos/{userSub:guid}")]
    public async Task<IActionResult> Read(Guid userSub, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (userSub != sub && !UserClaims.IsTeacher(User)) return Forbid();
        var photo = await db.ProfilePhotos.AsNoTracking().SingleOrDefaultAsync(p => p.UserSub == userSub, ct);
        if (photo is null) return NotFound();
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        var obj = await storage.GetAsync(photo.StorageKey, ct);
        if (obj is null) return NotFound();
        Response.RegisterForDisposeAsync(obj);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(obj.Content, photo.ContentType);
    }

    [HttpPost("api/me/profile-photo")]
    [RequestSizeLimit(ProfilePhotoRules.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfilePhotoRules.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct,
        [FromHeader(Name = ProfileNameRules.SubjectHeader)] string? expectedSubject = null)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!Guid.TryParse(expectedSubject, out var expected) || expected != sub)
            return Conflict(new { error = "account_changed" });
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        if (file is null || file.Length == 0) return BadRequest(new { error = "missing_file" });
        if (file.Length > ProfilePhotoRules.MaxBytes) return BadRequest(new { error = "file_too_large" });
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > ProfilePhotoRules.MaxBytes) return BadRequest(new { error = "file_too_large" });
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (buffer.Length != file.Length) return BadRequest(new { error = "incomplete_upload" });
        var image = await ProfilePhotoImages.NormalizeAsync(buffer.ToArray(), ct);
        if (image is null) return BadRequest(new { error = "invalid_photo" });

        var photo = await db.ProfilePhotos.SingleOrDefaultAsync(p => p.UserSub == sub, ct);
        var oldKey = photo?.StorageKey;
        var key = $"profile-photos/{sub:N}/{Guid.NewGuid():N}.jpg";
        using var normalized = new MemoryStream(image, writable: false);
        try { await storage.PutAsync(key, normalized, "image/jpeg", ct); }
        catch
        {
            // A storage timeout can arrive after the object was written.
            await CleanupAsync(key);
            throw;
        }
        if (photo is null)
        {
            photo = new ProfilePhoto { UserSub = sub };
            db.ProfilePhotos.Add(photo);
        }
        photo.StorageKey = key;
        photo.ContentType = "image/jpeg";
        photo.SizeBytes = image.Length;
        photo.UpdatedAtUtc = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            await CleanupUnreferencedAsync(key);
            return Conflict(new { error = "photo_changed" });
        }
        catch
        {
            await CleanupUnreferencedAsync(key);
            throw;
        }
        if (oldKey is not null) await CleanupAsync(oldKey);
        return Ok(new ProfilePhotoView(true, photo.UpdatedAtUtc));
    }

    [HttpDelete("api/me/profile-photo")]
    public async Task<IActionResult> Delete(CancellationToken ct,
        [FromHeader(Name = ProfileNameRules.SubjectHeader)] string? expectedSubject = null)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!Guid.TryParse(expectedSubject, out var expected) || expected != sub)
            return Conflict(new { error = "account_changed" });
        var photo = await db.ProfilePhotos.SingleOrDefaultAsync(p => p.UserSub == sub, ct);
        if (photo is null) return Ok(new ProfilePhotoView(false, null));
        if (!storage.IsConfigured) return StatusCode(503, new { error = "object_storage_disabled" });
        db.ProfilePhotos.Remove(photo);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "photo_changed" }); }
        await CleanupAsync(photo.StorageKey);
        return Ok(new ProfilePhotoView(false, null));
    }

    private async Task CleanupUnreferencedAsync(string key)
    {
        // A failed/canceled commit has an ambiguous outcome. Never delete a live photo.
        try
        {
            if (!await db.ProfilePhotos.AsNoTracking().AnyAsync(p => p.StorageKey == key, CancellationToken.None))
                await CleanupAsync(key);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Could not check interrupted profile photo upload for cleanup"); }
    }

    private async Task CleanupAsync(string key)
    {
        try { await storage.DeleteAsync(key, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Profile photo object cleanup failed"); }
    }
}
