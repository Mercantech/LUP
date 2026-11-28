using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/badges"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BadgesController(AppDbContext db, CourseAccess access) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var entries = await db.AccountBadges.Where(b => b.UserSub == sub).OrderByDescending(b => b.UpdatedUtc)
            .Select(b => new BadgeEntry(b.Id, b.BadgeId, b.Rank, b.Status, b.Evidence, b.UpdatedUtc)).ToArrayAsync(ct);
        var holds = await db.Hold.Where(h => h.Memberships.Any(m => m.UserSub == sub)).OrderBy(h => h.Name)
            .Select(h => new BadgeHold(h.Id, h.Name)).ToArrayAsync(ct);
        return Ok(new BadgeCollection(entries, holds));
    }

    [HttpPost("{badgeId}/requests")]
    public async Task<IActionResult> Apply(string badgeId, BadgeApplication request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var definition = BadgeCatalog.Find(badgeId);
        if (definition is null || definition.Automatic || request.Rank < 1 || request.Rank > definition.Levels
            || string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Length > 700) return BadRequest();
        if (!await db.Memberships.AnyAsync(m => m.UserSub == sub && m.HoldId == request.HoldId, ct)) return Forbid();
        if (request.Rank > 1 && !await db.AccountBadges.AnyAsync(b => b.UserSub == sub && b.BadgeId == badgeId
            && b.Rank == request.Rank - 1 && b.Status == "earned", ct)) return Conflict();

        var previous = db.AccountBadges.Where(b => b.UserSub == sub && b.BadgeId == badgeId && b.Rank == request.Rank);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await previous.Where(b => b.Status == "declined").ExecuteDeleteAsync(ct);
        if (await previous.AnyAsync(ct)) return Conflict();
        db.AccountBadges.Add(new AccountBadge { UserSub = sub, BadgeId = badgeId, Rank = request.Rank,
            HoldId = request.HoldId, Evidence = request.Evidence.Trim() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { return Conflict(); }
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private IQueryable<AccountBadge> Reviewable(Guid reviewer)
    {
        var holds = access.AccessibleHolds(User, true);
        return db.AccountBadges.Where(b => b.Status == "pending" && b.UserSub != reviewer &&
            holds.Any(h => h.Id == b.HoldId && h.Memberships.Any(m => m.UserSub == b.UserSub)));
    }

    [HttpGet("requests")]
    public async Task<IActionResult> Requests(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var requests = await (from b in Reviewable(sub)
                              join m in db.Memberships on new { b.HoldId, Sub = (Guid?)b.UserSub }
                                  equals new { HoldId = (Guid?)m.HoldId, Sub = m.UserSub }
                              orderby b.UpdatedUtc
                              select new BadgeReview(b.Id, b.BadgeId, b.Rank, b.Evidence,
                                  db.ProfileNames.Where(p => p.UserSub == b.UserSub).Select(p => p.DisplayName).FirstOrDefault()
                                      ?? m.DisplayName ?? m.LectioId ?? "Elev", m.Hold.Name)).ToArrayAsync(ct);
        return Ok(requests);
    }

    [HttpPost("requests/{id:guid}/decision")]
    public async Task<IActionResult> Review(Guid id, BadgeDecision decision, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var changed = await Reviewable(sub).Where(b => b.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(b => b.Status, decision.Approve ? "earned" : "declined")
            .SetProperty(b => b.ReviewerSub, sub).SetProperty(b => b.UpdatedUtc, DateTime.UtcNow), ct);
        return changed == 1 ? NoContent() : NotFound();
    }
}
