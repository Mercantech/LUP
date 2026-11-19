using System.Net.Http.Headers;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Api.Services;
using Mercantec.Tech.Api.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

/// <summary>
/// Proxy til Lectio-Collector. Udgående kald bruger den Bearer, JWT-middleware allerede har valideret.
/// Rolle og sub læses kun fra den principal — aldrig fra body eller query.
/// </summary>
[ApiController]
[Route("api/lectio")]
[Authorize]
public class LectioController(LectioCollectorClient lectio, MembershipClaimService claims) : ControllerBase
{
    private static readonly SemaphoreSlim PhotoNormalization = new(2, 2);
    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken ct) =>
        ProxyAsync(() => lectio.GetMeAsync(RequireBearer(), ct), ct);

    [HttpGet("me/student")]
    public Task<IActionResult> MyStudent(CancellationToken ct)
    {
        if (UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(BadRequest(new
            {
                error = "teacher_use_list",
                message = "Brug GET /api/lectio/programmeringselever."
            }));

        return ProxyAsync(() => lectio.GetMyStudentAsync(RequireBearer(), ct), ct);
    }

    [HttpGet("me/lectio-candidates")]
    public Task<IActionResult> LectioCandidates(CancellationToken ct) =>
        ProxyAsync(() => lectio.GetLectioCandidatesAsync(RequireBearer(), ct), ct);

    [HttpGet("programmeringselever")]
    public Task<IActionResult> Programmeringselever(CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync(() => lectio.GetProgrammeringseleverAsync(RequireBearer(), ct), ct);
    }

    [HttpGet("students/{elevId}/studieplan")]
    public async Task<IActionResult> Studieplan(string elevId, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
        {
            string? ownId;
            try
            {
                ownId = await ResolveOwnLectioIdAsync(ct);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { error = "missing_bearer" });
            }
            catch (LectioCollectorException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }

            if (string.IsNullOrWhiteSpace(ownId)
                || !string.Equals(ownId, elevId, StringComparison.OrdinalIgnoreCase))
                return Forbid();
        }

        return await ProxyAsync(() => lectio.GetStudieplanAsync(elevId, RequireBearer(), ct), ct);
    }

    [HttpGet("students/{elevId}/photo")]
    public async Task<IActionResult> StudentPhoto(string elevId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (elevId.Length is 0 or > 40 || elevId.Any(c => c is < '0' or > '9'))
            return BadRequest(new { error = "invalid_lectio_id" });
        try
        {
            var bearer = RequireBearer();
            if (!UserClaims.IsTeacher(User))
            {
                var ownId = await ResolveOwnLectioIdAsync(ct);
                if (!string.Equals(ownId, elevId, StringComparison.Ordinal)) return Forbid();
            }
            var source = await lectio.GetStudentPhotoAsync(elevId, bearer, ct);
            if (source is null) return NotFound();
            await PhotoNormalization.WaitAsync(ct);
            byte[]? image;
            try { image = await ProfilePhotoImages.NormalizeAsync(source, ct); }
            finally { PhotoNormalization.Release(); }
            if (image is null) return StatusCode(502, new { error = "invalid_lectio_photo" });
            return File(image, "image/jpeg");
        }
        catch (UnauthorizedAccessException) { return Unauthorized(new { error = "missing_bearer" }); }
        catch (LectioCollectorException ex)
        {
            return ex.StatusCode switch
            {
                401 => Unauthorized(),
                403 => Forbid(),
                404 => NotFound(),
                _ => StatusCode(503, new { error = "lectio_photo_unavailable" })
            };
        }
        catch (HttpRequestException) { return StatusCode(503, new { error = "lectio_photo_unavailable" }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(503, new { error = "lectio_photo_unavailable" });
        }
    }

    [HttpGet("holds/tracked")]
    public Task<IActionResult> TrackedHolds(CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync(() => lectio.GetTrackedHoldsAsync(RequireBearer(), ct), ct);
    }

    [HttpGet("holds/saved")]
    public Task<IActionResult> SavedHolds(CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync(() => lectio.GetSavedHoldsAsync(RequireBearer(), ct), ct);
    }

    [HttpGet("holds/by-lectio/{lectioHoldKey}")]
    public Task<IActionResult> HoldByLectio(string lectioHoldKey, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync(() => lectio.GetHoldByLectioKeyAsync(lectioHoldKey, RequireBearer(), ct), ct);
    }

    public sealed record LinkBody(string LectioId, Guid? AuthSub);

    public sealed record ResolveAuthLinksBody(string[]? LectioIds);

    /// <summary>
    /// Underviser: slå Auth↔Lectio-koblinger op for en liste af Lectio-id'er (Collector auth-links/resolve).
    /// </summary>
    [HttpPost("auth-links/resolve")]
    public async Task<IActionResult> ResolveAuthLinks([FromBody] ResolveAuthLinksBody body, CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        try
        {
            var ids = body.LectioIds ?? [];
            var map = await lectio.ResolveAuthLinksAsync(ids, RequireBearer(), ct);
            var links = map.Select(kv => new { lectioId = kv.Key, authSub = kv.Value }).ToList();
            return Ok(new { links });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "missing_bearer" });
        }
        catch (LectioCollectorException ex)
        {
            return StatusCode(ex.StatusCode, ex.Body);
        }
    }

    [HttpPut("me/lectio-link")]
    public async Task<IActionResult> Link([FromBody] LinkBody body, CancellationToken ct)
    {
        try
        {
            var isTeacher = UserClaims.IsTeacher(User);
            Guid? authSub = isTeacher && body.AuthSub is Guid overrideSub && overrideSub != Guid.Empty
                ? overrideSub
                : null;
            await lectio.LinkAsync(body.LectioId, RequireBearer(), authSub, ct);

            var sub = authSub ?? UserClaims.GetSub(User);
            var claimed = 0;
            if (sub is Guid s)
                claimed = await claims.ClaimByLectioIdAsync(s, body.LectioId, ct);

            return Ok(new
            {
                linked = true,
                lectioId = body.LectioId,
                authSub = sub,
                claimedMemberships = claimed
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "missing_bearer" });
        }
        catch (LectioCollectorException ex)
        {
            return StatusCode(ex.StatusCode, ex.Body);
        }
    }

    [HttpPost("sync/memberships/{holdId:guid}")]
    public async Task<IActionResult> SyncMemberships(
        Guid holdId,
        [FromQuery] string lectioHoldKey,
        [FromServices] ILectioMembershipSync sync,
        CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var result = await sync.SyncHoldMembershipsAsync(holdId, lectioHoldKey, RequireBearer(), ct);
        return Ok(result);
    }

    private async Task<IActionResult> ProxyAsync(
        Func<Task<System.Text.Json.JsonElement>> call,
        CancellationToken ct)
    {
        try
        {
            var json = await call();
            return Content(json.GetRawText(), "application/json");
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "missing_bearer" });
        }
        catch (LectioCollectorException ex)
        {
            return StatusCode(ex.StatusCode, ex.Body);
        }
    }

    private async Task<string?> ResolveOwnLectioIdAsync(CancellationToken ct)
    {
        var me = await lectio.GetMeAsync(RequireBearer(), ct);
        if (!me.TryGetProperty("lectio", out var lectioEl)
            || lectioEl.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (lectioEl.ValueKind != JsonValueKind.Object
            || !lectioEl.TryGetProperty("lectioId", out var id)
            || id.ValueKind != JsonValueKind.String)
            return null;
        var value = id.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string RequireBearer()
    {
        var header = Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(header, out var value)
            && value.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(value.Parameter))
        {
            return value.Parameter;
        }

        throw new UnauthorizedAccessException("missing_bearer");
    }
}
