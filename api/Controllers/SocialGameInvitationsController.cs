using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/social-games/rooms/{code}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SocialGameInvitationsController(AppDbContext db) : ControllerBase
{
    [HttpGet("invitees")]
    public async Task<IActionResult> Candidates(string code, [FromQuery] string? q, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!await OpenLobby(code, sub).AnyAsync(ct)) return NotFound();
        if (q?.Length > 200) return BadRequest(new { error = "Søg med et kortere navn." });
        var profiles = db.NotebookProfiles.AsNoTracking().Where(p => p.Sub != sub);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            profiles = profiles.Where(p => p.DisplayName.ToLower().Contains(term));
        }
        var ownHolds = db.Hold.Where(h => h.Forloeb.CreatedBySub == sub || h.Memberships.Any(m => m.UserSub == sub));
        var ranked = profiles.Select(p => new { p.Sub, p.DisplayName, Own = ownHolds.Any(h =>
            h.Forloeb.CreatedBySub == p.Sub || h.Memberships.Any(m => m.UserSub == p.Sub)) });
        return Ok(await ranked.OrderByDescending(p => p.Own).ThenBy(p => p.DisplayName).ThenBy(p => p.Sub)
            .Take(50).Select(p => new SocialInviteCandidate(p.Sub, p.DisplayName, p.Own)).ToArrayAsync(ct));
    }

    [HttpPost("invitations")]
    public async Task<IActionResult> Invite(string code, SocialInviteRequest request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var room = await OpenLobby(code, sub).Select(r => new { r.Id }).SingleOrDefaultAsync(ct);
        if (room is null) return NotFound(new { error = "Lobbyen er lukket, eller du er ikke vært." });
        if (request.UserSub == sub || !await db.NotebookProfiles.AnyAsync(p => p.Sub == request.UserSub, ct))
            return BadRequest(new { error = "Vælg en registreret LUP-bruger." });
        var invites = db.SocialGameInvitations;
        if (await invites.AnyAsync(i => i.RoomId == room.Id && i.RecipientSub == request.UserSub, ct))
            return NoContent();
        var sender = await db.NotebookProfiles.Where(p => p.Sub == sub).Select(p => p.DisplayName).SingleOrDefaultAsync(ct)
            ?? User.FindFirstValue("name") ?? "En LUP-bruger";
        var invitation = new SocialGameInvitation { Id = Guid.NewGuid(), RoomId = room.Id,
            RecipientSub = request.UserSub, SenderName = sender[..Math.Min(sender.Length, 200)] };
        invites.Add(invitation);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(invitation).State = EntityState.Detached;
            if (!await invites.AnyAsync(i => i.RoomId == room.Id && i.RecipientSub == request.UserSub, ct)) throw;
        }
        return NoContent();
    }

    private IQueryable<SocialGameRoom> OpenLobby(string code, Guid host) =>
        db.SocialGameRooms.AsNoTracking().Where(r => r.Code == code && r.HostSub == host && !r.IsStarted && r.ExpiresAtUtc > DateTime.UtcNow);
}
