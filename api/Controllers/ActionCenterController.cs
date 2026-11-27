using Mercantec.Tech.Contracts;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/me/actions")]
public sealed class ActionCenterController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromQuery] Guid? asUser, CancellationToken ct, [FromQuery] int version = 1, [FromQuery] bool socialGames = false, [FromQuery] bool badges = false)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        var teacher = UserClaims.IsTeacher(User);
        if (asUser.HasValue && !teacher) return Forbid();
        if (asUser == Guid.Empty) return BadRequest(new { error = "invalid_user" });
        if (version is not (1 or 2)) return BadRequest(new { error = "unsupported_actions_version" });
        var preview = asUser.HasValue;
        var target = asUser ?? subject;
        var holds = teacher ? access.AccessibleHolds(User, true) : db.Hold.Where(h => h.Memberships.Any(m => m.UserSub == target));
        if (preview) holds = holds.Where(h => h.Memberships.Any(m => m.UserSub == target));
        var scope = await holds.AsNoTracking().Select(h => new ActionCenterHold(h.Id, h.ForloebId, h.Name)).ToArrayAsync(ct);
        // An empty preview scope cannot establish the selected pupil's personal status.
        if (preview && scope.Length == 0) return NotFound();
        var snapshot = await new ActionCenterBuilder(db).BuildAsync(target, scope, teacher && !preview, ct, version: version, includeGifts: !preview, includeNews: !preview);
        if (version == 2 && !preview)
        {
            snapshot = snapshot with { Items = [.. snapshot.Items, .. await new Mercantec.Tech.Api.References.ReferenceService(db).MentionsAsync(User, ct)] };
            // Older web clients validate each notification kind before displaying the snapshot.
            if (badges)
            {
                var earned = await db.AccountBadges.Where(b => b.UserSub == subject && b.Status == "earned" && !b.NoticeRead)
                    .Select(b => new { b.Id, b.BadgeId, b.Rank }).ToArrayAsync(ct);
                var notices = earned.Select(b => new ActionCenterItem("badge-award", "optional", null, null, null, b.Id, null,
                    BadgeCatalog.Find(b.BadgeId)?.Name ?? b.BadgeId, BadgeCatalog.Find(b.BadgeId)?.Levels > 1 ? $"Niveau {b.Rank}" : "Mine badges", null, State: b.BadgeId));
                snapshot = snapshot with { Items = [.. snapshot.Items, .. notices] };
            }
            if (socialGames)
            {
                var invitations = await db.SocialGameInvitations.AsNoTracking()
                    .Where(i => i.RecipientSub == subject && i.ReadAtUtc == null && !i.Room.IsStarted && i.Room.ExpiresAtUtc > DateTime.UtcNow)
                    .Select(i => new { i.Id, i.SenderName, i.Room.Code }).ToListAsync(ct);
                var gameItems = invitations.Select(i => new ActionCenterItem("social-game-invite", "optional", null, null,
                    null, i.Id, null, i.SenderName + " inviterer dig til Werewolf", "", null, State: i.Code));
                snapshot = snapshot with { Items = [.. snapshot.Items, .. gameItems] };
            }
        }
        return Ok(snapshot);
    }
    [HttpPost("read"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Read(ActionCenterReadRequest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid subject || subject == Guid.Empty) return Unauthorized();
        if (request?.Items is null || request.Items.Length > 200) return BadRequest(new { error = "invalid_read_receipts" });
        var teacher = UserClaims.IsTeacher(User);
        var holds = teacher ? access.AccessibleHolds(User, true) : db.Hold.Where(h => h.Memberships.Any(m => m.UserSub == subject));
        var scope = await holds.AsNoTracking().Select(h => new ActionCenterHold(h.Id, h.ForloebId, h.Name)).ToArrayAsync(ct);
        var badgeIds = request.Items.Where(i => i.Kind == "badge-award").Select(i => i.ResourceId).ToArray();
        if (badgeIds.Length > 0)
            await db.AccountBadges.Where(b => b.UserSub == subject && b.Status == "earned" && badgeIds.Contains(b.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.NoticeRead, true), ct);
        var inviteIds = request.Items.Where(item => item.Kind == "social-game-invite").Select(item => item.ResourceId).ToArray();
        if (inviteIds.Length > 0)
        {
            var invitations = await db.SocialGameInvitations.Where(i => i.RecipientSub == subject && inviteIds.Contains(i.Id) && i.ReadAtUtc == null).ToListAsync(ct);
            foreach (var invitation in invitations) invitation.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await new ActionCenterNews(db).AcknowledgeAsync(subject, request.Items, scope, teacher, ct);
        await new Mercantec.Tech.Api.References.ReferenceService(db).AcknowledgeMentionsAsync(User, request.Items, ct);
        return NoContent();
    }
}
