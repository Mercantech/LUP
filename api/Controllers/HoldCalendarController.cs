using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/holds/{holdId:guid}/calendar")]
public sealed partial class HoldCalendarController(AppDbContext db, CourseAccess access, AzureGraphCalendarClient? graph = null) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid holdId, CancellationToken ct, [FromQuery] Guid? asUser = null)
    {
        if (ReadIdentity(asUser, out var viewer) is { } error) return error;
        if (!await VisibleHolds(asUser).AnyAsync(h => h.Id == holdId, ct)) return NotFound();
        Guid[] manageable = !asUser.HasValue && await access.CanManageAsync(holdId, User, ct) ? [holdId] : [];
        var events = await new HoldCalendarAccess(db).Visible(db.HoldCalendarEvents.AsNoTracking().Where(e => e.HoldId == holdId),
            viewer, manageable).ToListAsync(ct);
        return Ok(events.OrderBy(e => e.StartUtc).Select(HoldCalendarAccess.View).ToArray());
    }

    [HttpGet("/api/calendar/holds"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Overview([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? asUser, CancellationToken ct)
    {
        if (ReadIdentity(asUser, out var viewer) is { } error) return error;
        if (from is not { } suppliedStart || to is not { } suppliedEnd || suppliedEnd <= suppliedStart)
            return BadRequest(new { error = "invalid_range" });
        if (suppliedEnd - suppliedStart > TimeSpan.FromDays(45))
            return BadRequest(new { error = "range_too_large" });
        return Ok(await new HoldCalendarAccess(db).ReadOverviewAsync(
            VisibleHolds(asUser), access.AccessibleHolds(User, manageOnly: true), viewer,
            suppliedStart.ToUniversalTime(), suppliedEnd.ToUniversalTime(), ct, preview: asUser.HasValue));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid holdId, SaveHoldCalendarEvent request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        if (!await IsValidAsync(holdId, request, ct)) return BadRequest(new { error = "invalid_calendar_event" });
        var item = new HoldCalendarEvent { Id = Guid.NewGuid(), HoldId = holdId };
        Apply(item, request);
        db.HoldCalendarEvents.Add(item);
        await db.SaveChangesAsync(ct);
        return Ok(HoldCalendarAccess.View(item));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid holdId, Guid id, SaveHoldCalendarEvent request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var item = await db.HoldCalendarEvents.SingleOrDefaultAsync(e => e.Id == id && e.HoldId == holdId, ct);
        if (item is null) return NotFound();
        if (request.Version != item.Version) return Conflict(new { error = "calendar_event_changed" });
        if (!await IsValidAsync(holdId, request, ct, item.QuestId)) return BadRequest(new { error = "invalid_calendar_event" });
        var change = item.StartUtc != request.StartUtc || item.EndUtc != request.EndUtc ? AddChange(item, "moved") : null;
        Apply(item, request);
        item.Version++;
        if (!await SaveChangeAsync(item, change, ct)) return Conflict(new { error = "calendar_event_changed" });
        return Ok(HoldCalendarAccess.View(item));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid holdId, Guid id, [FromQuery] int version, CancellationToken ct)
    {
        if (!await access.CanManageAsync(holdId, User, ct)) return NotFound();
        var item = await db.HoldCalendarEvents.SingleOrDefaultAsync(e => e.Id == id && e.HoldId == holdId, ct);
        if (item is null) return NotFound();
        if (version != item.Version) return Conflict(new { error = "calendar_event_changed" });
        var change = AddChange(item, "cancelled");
        db.HoldCalendarEvents.Remove(item);
        if (!await SaveChangeAsync(item, change, ct)) return Conflict(new { error = "calendar_event_changed" });
        return Ok(new { deleted = true });
    }

    private IActionResult? ReadIdentity(Guid? asUser, out Guid viewer)
    {
        viewer = UserClaims.GetSub(User) ?? Guid.Empty;
        if (viewer == Guid.Empty) return Unauthorized();
        if (!asUser.HasValue) return null;
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (asUser == Guid.Empty) return BadRequest(new { error = "invalid_user" });
        viewer = asUser.Value;
        return null;
    }

    private IQueryable<Hold> VisibleHolds(Guid? asUser)
    {
        var holds = access.AccessibleHolds(User);
        return asUser is Guid student ? holds.Where(h => h.Memberships.Any(m => m.UserSub == student)) : holds;
    }

    private async Task<bool> IsValidAsync(Guid holdId, SaveHoldCalendarEvent request, CancellationToken ct, Guid? existingQuest = null)
    {
        if (request.HoldId != holdId || string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200
            || request.Description is null || request.Description.Length > 4000 || request.Location is null || request.Location.Length > 200
            || request.Kind is not ("teaching" or "workshop" or "exam" or "other" or "quest")
            || request.AttendanceMode is not (null or "physical" or "online" or "home")
            || request.StartUtc == default || request.EndUtc <= request.StartUtc || request.EndUtc - request.StartUtc > TimeSpan.FromDays(31))
            return false;
        if (request.Kind != "quest") return request.QuestId is null;
        return request.QuestId is Guid questId && await db.LearningQuests.AnyAsync(q => q.Id == questId && q.HoldId == holdId
            && (q.Published || q.Id == existingQuest), ct);
    }

    private HoldCalendarChange AddChange(HoldCalendarEvent item, string kind)
    {
        var change = new HoldCalendarChange
        {
            Id = Guid.NewGuid(), HoldId = item.HoldId, EventId = item.Id, EventVersion = item.Version + 1, QuestId = item.QuestId,
            Title = item.Title, Kind = kind, ChangedAtUtc = DateTimeOffset.UtcNow
        };
        db.HoldCalendarChanges.Add(change);
        return change;
    }

    private async Task<bool> SaveChangeAsync(HoldCalendarEvent item, HoldCalendarChange? change, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(item).State = EntityState.Detached;
            if (change is not null) db.Entry(change).State = EntityState.Detached;
            return false;
        }
    }

    private static void Apply(HoldCalendarEvent item, SaveHoldCalendarEvent r)
    {
        item.Title = r.Title.Trim(); item.Description = r.Description.Trim();
        item.StartUtc = r.StartUtc.ToUniversalTime(); item.EndUtc = r.EndUtc.ToUniversalTime();
        item.Location = r.Location.Trim(); item.Kind = r.Kind;
        item.AttendanceMode = r.AttendanceMode; item.QuestId = r.QuestId;
    }
}
