using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/favorite-shortcuts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FavoriteShortcutsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FavoriteShortcutsDto>> Get(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true || UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();
        if (!MatchesExpectedSubject(subject, required: false))
            return AccountChanged();

        var list = await db.FavoriteShortcutLists.AsNoTracking()
            .SingleOrDefaultAsync(list => list.UserSub == subject, ct);
        return Ok(list is null ? new FavoriteShortcutsDto(null, []) : new FavoriteShortcutsDto(list.Revision, list.ReadItems()));
    }

    [HttpPut]
    [RequestSizeLimit(384 * 1024)]
    public async Task<ActionResult<FavoriteShortcutsDto>> Update(FavoriteShortcutsDto request, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true || UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty)
            return Unauthorized();
        if (!MatchesExpectedSubject(subject, required: true))
            return AccountChanged();
        if (!FavoriteShortcutRules.IsValidList(request.Items))
            return BadRequest(new { error = "invalid_favorite_shortcuts" });

        var revision = Guid.NewGuid();
        var itemsJson = JsonSerializer.Serialize(request.Items);
        var changed = request.Revision is Guid expected
            ? await db.FavoriteShortcutLists.Where(list => list.UserSub == subject && list.Revision == expected)
                .ExecuteUpdateAsync(update => update.SetProperty(list => list.Revision, revision)
                    .SetProperty(list => list.ItemsJson, itemsJson), ct)
            : await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO favorite_shortcut_lists ("UserSub", "Revision", "ItemsJson")
                VALUES ({subject}, {revision}, {itemsJson})
                ON CONFLICT ("UserSub") DO NOTHING
                """, ct);

        if (changed == 0)
            return Conflict(new { error = "favorite_shortcuts_changed" });

        return Ok(new FavoriteShortcutsDto(revision, request.Items));
    }

    // Token refresh must not turn a request from one account into a write for another.
    private bool MatchesExpectedSubject(Guid subject, bool required) =>
        Request.Headers.TryGetValue(FavoriteShortcutRules.SubjectHeader, out var header)
            ? Guid.TryParse(header.ToString(), out var expected) && expected == subject
            : !required;

    private ConflictObjectResult AccountChanged() => Conflict(new { error = "account_changed" });
}
