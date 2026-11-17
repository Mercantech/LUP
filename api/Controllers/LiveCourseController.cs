using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/notion/pages/{pageId:guid}/live")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed partial class LiveCourseController(AppDbContext db, CourseAccess courses, NotionAiProjectionClient projection,
    TimeProvider time) : ControllerBase
{
    private static readonly string[] Emojis = ["👍", "🤔", "💡", "🙋"];
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan ReactionLifetime = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan ReactionInterval = TimeSpan.FromMilliseconds(750);
    private const int MaxRoundsPerSession = 200;

    [HttpGet]
    public async Task<ActionResult<LiveCourseView>> Get(Guid pageId, CancellationToken ct,
        [FromQuery] Guid? sessionId = null, [FromQuery] long? after = null)
    {
        var access = await ReadPage(pageId, ct);
        if (access is null) return NotFound();
        var now = time.GetUtcNow().UtcDateTime;
        var session = await CurrentSession(access, now, ct);
        if (session is null || !await CourseStillVisible(access, session.CourseId, ct)) return Ok(new LiveCourseView(null));

        // Reactions expire and quiz deadlines can pass without a write, so skip constructing a
        // snapshot only when neither of those clocks can have changed it.
        if (session.Id == sessionId && session.Revision == after
            && !await db.LiveCourseReactions.AnyAsync(r => r.SessionId == session.Id, ct)
            && !await db.LiveCourseRounds.AnyAsync(r => r.Id == session.ActiveRoundId && r.DeadlineUtc != null
                && r.ClosedAtUtc == null, ct))
            return StatusCode(StatusCodes.Status304NotModified);
        return Ok(await BuildView(session, access, now, ct));
    }

    [HttpPost("modules")]
    public async Task<ActionResult<LiveCourseModuleView>> SaveModule(Guid pageId, SaveLiveCourseModule request, CancellationToken ct)
    {
        if (await ReadPage(pageId, ct) is not { MayManage: true }) return NotFound();
        if (!TryModule(pageId, request, time.GetUtcNow().UtcDateTime, out var module))
            return BadRequest(new { error = "invalid_live_module" });

        // Saves retry safely and do not create another version when the teacher changes nothing.
        var saved = await db.LiveCourseModules.AsNoTracking()
            .Where(m => m.PageId == pageId && m.BlockId == module.BlockId && m.Kind == module.Kind
                && m.PollType == module.PollType && m.Prompt == module.Prompt && m.OptionsJson == module.OptionsJson
                && m.CorrectChoice == module.CorrectChoice && m.DurationSeconds == module.DurationSeconds)
            .OrderByDescending(m => m.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (saved is not null) return Ok(ModuleView(saved));
        db.LiveCourseModules.Add(module);
        await db.SaveChangesAsync(ct);
        return Ok(ModuleView(module));
    }

    [HttpGet("modules/{moduleId:guid}")]
    public async Task<ActionResult<LiveCourseModuleView>> GetModule(Guid pageId, Guid moduleId, CancellationToken ct)
    {
        if (await ReadPage(pageId, ct) is not { MayManage: true }) return NotFound();
        var module = await db.LiveCourseModules.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == moduleId && m.PageId == pageId, ct);
        return module is null ? NotFound() : Ok(ModuleView(module));
    }

    [HttpPost("start")]
    public async Task<ActionResult<LiveCourseView>> Start(Guid pageId, StartLiveCourse request, CancellationToken ct)
    {
        if (await ReadPage(pageId, ct) is not { MayManage: true } access) return NotFound();
        if (access.PublishedRevision != request.PublishedRevision) return Conflict(new { error = "publication_changed" });
        var course = await ResolveCourse(access, request.CourseId, ct);
        if (!course.Allowed) return NotFound();
        var now = time.GetUtcNow().UtcDateTime;
        var existing = await CurrentSession(access, now, ct);
        if (existing is not null)
            return existing.CourseId == course.Id ? Ok(await BuildView(existing, access, now, ct))
                : Conflict(new { error = "presentation_already_active" });

        var published = await db.NotionPages.AsNoTracking().Where(p => p.Id == pageId && p.DeletedAtUtc == null
            && p.PublishedRevision == request.PublishedRevision && p.PublicationVersion == access.PublicationVersion)
            .Select(p => p.PublishedState).SingleOrDefaultAsync(ct);
        if (published is not { Length: > 0 }) return Conflict(new { error = "publication_changed" });
        LiveCourseProjection slides;
        try { slides = await projection.ProjectLiveAsync(published, ct); }
        catch (Exception error) when (!ct.IsCancellationRequested && error is HttpRequestException or JsonException or InvalidDataException or OperationCanceledException)
        { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "live_projection_unavailable" }); }
        if (slides.SlideCount is < 1 or > 2000 || slides.Bindings is not { Length: <= 1000 }
            || slides.Bindings.Any(b => !ValidBlockId(b.BlockId) || b.ModuleId == Guid.Empty || b.SlideIndex < 0 || b.SlideIndex >= slides.SlideCount)
            || slides.Bindings.Select(b => b.BlockId).Distinct().Count() != slides.Bindings.Length)
            return BadRequest(new { error = "invalid_live_slides" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // The existing publish/unpublish endpoints take the same row lock.
        var pageLocked = await db.NotionPages.Where(p => p.Id == pageId && p.DeletedAtUtc == null
                && p.PublishedRevision == request.PublishedRevision && p.PublicationVersion == access.PublicationVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PublicationVersion, p => p.PublicationVersion), ct);
        if (pageLocked == 0 || !await CourseStillVisible(access, course.Id, ct))
            return Conflict(new { error = "publication_changed" });
        now = time.GetUtcNow().UtcDateTime;
        existing = await CurrentSession(access, now, ct);
        if (existing is not null)
            return existing.CourseId == course.Id ? Ok(await BuildView(existing, access, now, ct))
                : Conflict(new { error = "presentation_already_active" });

        var stale = db.LiveCourseSessions.Where(s => s.PageId == pageId && s.EndedAtUtc == null);
        await db.LiveCourseReactions.Where(r => stale.Any(s => s.Id == r.SessionId)).ExecuteDeleteAsync(ct);
        await stale.ExecuteUpdateAsync(s => s.SetProperty(p => p.EndedAtUtc, now), ct);
        var session = new LiveCourseSession
        {
            Id = Guid.NewGuid(), PageId = pageId, HoldId = access.HoldId, CourseId = course.Id,
            PublishedRevision = request.PublishedRevision, PublicationVersion = access.PublicationVersion,
            SlideCount = slides.SlideCount, BindingsJson = JsonSerializer.Serialize(slides.Bindings),
            StartedAtUtc = now, ExpiresAtUtc = now + SessionLifetime, Revision = 1
        };
        db.LiveCourseSessions.Add(session);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(await BuildView(session, access, now, ct));
    }

    [HttpPost("{sessionId:guid}/stop")]
    public Task<ActionResult<LiveCourseView>> Stop(Guid pageId, Guid sessionId, CancellationToken ct) =>
        Change(pageId, sessionId, true, async (session, _, now) =>
        {
            await CloseRound(session, now, ct);
            session.EndedAtUtc = now;
            await db.LiveCourseReactions.Where(r => r.SessionId == session.Id).ExecuteDeleteAsync(ct);
            return null;
        }, ct);

    [HttpPost("{sessionId:guid}/slide")]
    public Task<ActionResult<LiveCourseView>> Slide(Guid pageId, Guid sessionId, LiveCourseSlide request, CancellationToken ct) =>
        Change(pageId, sessionId, true, async (session, _, now) =>
        {
            if (request.SlideIndex < 0 || request.SlideIndex >= session.SlideCount) return BadRequest(new { error = "invalid_slide" });
            if (session.SlideIndex == request.SlideIndex) return null;
            await CloseRound(session, now, ct);
            session.SlideIndex = request.SlideIndex;
            return null;
        }, ct);

    [HttpPost("{sessionId:guid}/react")]
    public Task<ActionResult<LiveCourseView>> React(Guid pageId, Guid sessionId, LiveCourseReaction request, CancellationToken ct) =>
        Change(pageId, sessionId, false, async (session, access, now) =>
        {
            if (!Emojis.Contains(request.Emoji)) return BadRequest(new { error = "invalid_reaction" });
            var reaction = await db.LiveCourseReactions.FindAsync([session.Id, access.UserSub], ct);
            if (reaction is not null && now - reaction.UpdatedAtUtc < ReactionInterval)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "reaction_too_fast" });
            if (reaction is null)
            {
                reaction = new() { SessionId = session.Id, UserSub = access.UserSub };
                db.LiveCourseReactions.Add(reaction);
            }
            reaction.Emoji = request.Emoji;
            reaction.UpdatedAtUtc = now;
            return null;
        }, ct);

    [HttpPost("{sessionId:guid}/ranking")]
    public Task<ActionResult<LiveCourseView>> Ranking(Guid pageId, Guid sessionId, ShowLiveCourseRanking request, CancellationToken ct) =>
        Change(pageId, sessionId, true, (session, _, _) =>
        {
            session.RankingVisible = request.Visible;
            return Task.FromResult<ActionResult?>(null);
        }, ct);

    private async Task<ActionResult<LiveCourseView>> Change(Guid pageId, Guid sessionId, bool manage,
        Func<LiveCourseSession, PageAccess, DateTime, Task<ActionResult?>> change, CancellationToken ct)
    {
        var access = await ReadPage(pageId, ct);
        if (access is null || manage && !access.MayManage || !manage && !await IsParticipant(access, ct)) return NotFound();
        var now = time.GetUtcNow().UtcDateTime;
        var current = await CurrentSession(access, now, ct);
        if (current?.Id != sessionId || !await CourseStillVisible(access, current.CourseId, ct))
            return Conflict(new { error = "presentation_ended" });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var pageLocked = await db.NotionPages.Where(p => p.Id == pageId && p.DeletedAtUtc == null
                && p.PublishedRevision == current.PublishedRevision && p.PublicationVersion == current.PublicationVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PublicationVersion, p => p.PublicationVersion), ct);
        if (pageLocked == 0) return Conflict(new { error = "presentation_ended" });
        // Serialize controller actions, moderation, vote and answer writes on the same session row.
        var updated = await db.LiveCourseSessions.Where(s => s.Id == sessionId && s.EndedAtUtc == null && s.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Revision, p => p.Revision + 1), ct);
        if (updated == 0) return Conflict(new { error = "presentation_ended" });
        var session = await db.LiveCourseSessions.SingleAsync(s => s.Id == sessionId, ct);
        // The lock may have waited behind another answer; use server time after it was acquired.
        now = time.GetUtcNow().UtcDateTime;
        if (session.ExpiresAtUtc <= now) return Conflict(new { error = "presentation_ended" });
        var error = await change(session, access, now);
        if (error is not null) return error;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(await BuildView(session, access, now, ct));
    }

    private sealed record PageAccess(Guid PageId, Guid HoldId, Guid UserSub, bool MayManage, long? PublishedRevision,
        long PublicationVersion, Guid? ParentPageId);

    private async Task<PageAccess?> ReadPage(Guid pageId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid userSub) return null;
        var page = await db.NotionPages.AsNoTracking().Where(p => p.Id == pageId && p.DeletedAtUtc == null && p.HoldId != null)
            .Select(p => new { HoldId = p.HoldId!.Value, p.PublishedRevision, p.PublicationVersion, p.ParentPageId,
                Published = p.PublishedAtUtc != null && p.PublishedState != null && p.PublishedTitle != null }).SingleOrDefaultAsync(ct);
        if (page is null) return null;
        var mayManage = await courses.CanManageAsync(page.HoldId, User, ct);
        if (!mayManage && (!page.Published || !await courses.CanAccessAsync(page.HoldId, User, ct))) return null;
        return new(pageId, page.HoldId, userSub, mayManage, page.Published ? page.PublishedRevision : null, page.PublicationVersion, page.ParentPageId);
    }

    private Task<bool> IsParticipant(PageAccess access, CancellationToken ct) =>
        db.Memberships.AnyAsync(m => m.HoldId == access.HoldId && m.UserSub == access.UserSub, ct);

    private Task<LiveCourseSession?> CurrentSession(PageAccess access, DateTime now, CancellationToken ct) =>
        db.LiveCourseSessions.AsNoTracking().SingleOrDefaultAsync(s => s.PageId == access.PageId && s.HoldId == access.HoldId
            && s.EndedAtUtc == null && s.ExpiresAtUtc > now && s.PublishedRevision == access.PublishedRevision
            && s.PublicationVersion == access.PublicationVersion, ct);

    private async Task<(bool Allowed, Guid? Id)> ResolveCourse(PageAccess access, Guid? selected, CancellationToken ct)
    {
        var branch = new List<Guid> { access.PageId };
        var parent = access.ParentPageId;
        while (parent is Guid parentId && branch.Count < 64 && !branch.Contains(parentId))
        {
            branch.Add(parentId);
            parent = await db.NotionPages.AsNoTracking().Where(p => p.Id == parentId && p.HoldId == access.HoldId && p.DeletedAtUtc == null)
                .Select(p => p.ParentPageId).SingleOrDefaultAsync(ct);
        }
        var resources = await db.TeachingCourseResources.AsNoTracking().Where(r => r.Kind == "page" && branch.Contains(r.ResourceId)
                && r.Course.HoldId == access.HoldId)
            .Select(r => new { r.CourseId, r.ResourceId, Visible = r.Course.Published && !r.Course.Archived }).ToArrayAsync(ct);
        if (resources.Length == 0) return (selected == null, null);
        var available = resources.Where(r => r.Visible && (selected == null || r.CourseId == selected))
            .OrderBy(r => branch.IndexOf(r.ResourceId)).ThenBy(r => r.CourseId).FirstOrDefault();
        return (available is not null, available?.CourseId);
    }

    private async Task<bool> CourseStillVisible(PageAccess access, Guid? courseId, CancellationToken ct)
    {
        var resolved = await ResolveCourse(access, courseId, ct);
        return resolved.Allowed && resolved.Id == courseId;
    }

    private async Task CloseRound(LiveCourseSession session, DateTime now, CancellationToken ct)
    {
        if (session.ActiveRoundId is not Guid id) return;
        var round = await db.LiveCourseRounds.SingleOrDefaultAsync(r => r.Id == id && r.SessionId == session.Id, ct);
        if (round is not null) round.ClosedAtUtc ??= now;
        session.ActiveRoundId = null;
    }

    private static bool ValidBlockId(string? id) => id is { Length: > 0 and <= 128 }
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
