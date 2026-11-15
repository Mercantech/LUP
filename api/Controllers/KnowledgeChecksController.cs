using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/knowledge-checks")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed partial class KnowledgeChecksController(AppDbContext db, CourseAccess courses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();

        var teacher = UserClaims.IsTeacher(User);
        var owned = teacher ? await db.KnowledgeCheckQuizzes.AsNoTracking().Where(quiz => quiz.OwnerSub == sub)
            .OrderByDescending(quiz => quiz.UpdatedAtUtc)
            .Select(quiz => new KnowledgeCheckOwned(quiz.Id, quiz.Title, quiz.Topic, quiz.QuestionCount, quiz.UpdatedAtUtc))
            .ToArrayAsync(ct) : [];
        var holds = teacher ? await courses.AccessibleHolds(User, true).OrderBy(hold => hold.Name)
            .Select(hold => new KnowledgeCheckHold(hold.Id, hold.Name)).ToArrayAsync(ct) : [];
        var managed = holds.Select(hold => hold.Id).ToHashSet();
        var allowed = courses.AccessibleHolds(User).Select(hold => hold.Id);
        var visible = await PublicationQuery().Where(publication =>
            publication.HoldId == null
                ? publication.IsActive || teacher && publication.Version.Quiz.OwnerSub == sub
                : allowed.Contains(publication.HoldId.Value) && (publication.IsActive || managed.Contains(publication.HoldId.Value)))
            .OrderByDescending(publication => publication.CreatedAtUtc).ToArrayAsync(ct);
        var attempts = await db.KnowledgeCheckAttempts.AsNoTracking().Where(attempt => attempt.UserSub == sub)
            .OrderByDescending(attempt => attempt.StartedAtUtc).ThenBy(attempt => attempt.Id)
            .Select(attempt => new
            {
                attempt.Id, attempt.PublicationId, attempt.Status, attempt.Score, attempt.StartedAtUtc, attempt.SubmittedAtUtc,
                attempt.Publication.HoldId, attempt.Publication.IsActive, attempt.Publication.FeedbackMode, attempt.Publication.FeedbackReleased,
                attempt.Publication.Version.Title, attempt.Publication.Version.QuestionCount,
                HasAccess = attempt.Publication.HoldId == null || allowed.Contains(attempt.Publication.HoldId.Value)
            }).ToArrayAsync(ct);
        var ownByPublication = attempts.ToLookup(attempt => attempt.PublicationId);
        var views = visible.Select(publication =>
        {
            var own = ownByPublication[publication.Id];
            var canManage = publication.HoldId is Guid hold
                ? managed.Contains(hold) : teacher && publication.Version.Quiz.OwnerSub == sub;
            return View(publication, canManage, own.FirstOrDefault(attempt => attempt.Status == Started)?.Id,
                own.FirstOrDefault()?.Id, own.Count());
        }).ToArray();
        var history = attempts.Where(attempt => attempt.SubmittedAtUtc is not null || attempt.HasAccess && attempt.IsActive)
            .Select(attempt => new KnowledgeCheckHistory(attempt.PublicationId, attempt.Id, attempt.Title, attempt.Status,
                attempt.FeedbackMode == AfterSubmit || attempt.FeedbackReleased ? attempt.Score : null,
                attempt.QuestionCount, attempt.StartedAtUtc, attempt.SubmittedAtUtc,
                attempt.HasAccess && (attempt.IsActive || attempt.SubmittedAtUtc is not null))).ToArray();
        return Ok(new KnowledgeCheckIndex(owned, views, history, holds));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDraft(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var quiz = await GetOwned(id, sub, ct);
        return quiz is null ? NotFound() : Ok(DraftView(quiz));
    }

    [HttpPost]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Create(KnowledgeCheckDraft request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        if (!NormalizeDraft(request, out var draft, out var invalidQuestion)) return DraftError(invalidQuestion);
        var quiz = NewDraftQuiz(Guid.NewGuid(), sub, draft, DateTime.UtcNow);
        db.KnowledgeCheckQuizzes.Add(quiz);
        await db.SaveChangesAsync(ct);
        return Ok(DraftView(quiz));
    }

    [HttpPost("pilot")]
    public async Task<IActionResult> CreatePilot(CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is null) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var pilot = Pilot();
        return pilot is null ? Problem() : await Create(pilot, ct);
    }

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Update(Guid id, KnowledgeCheckDraft request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var quiz = await GetOwned(id, sub, ct);
        if (quiz is null) return NotFound();
        if (!NormalizeDraft(request, out var draft, out var invalidQuestion)) return DraftError(invalidQuestion);
        if (draft.Revision is long revision && revision != quiz.Revision) return Conflict(new { error = "draft_changed" });
        var json = Write(draft.Questions);
        if (quiz.Title == draft.Title && quiz.Description == draft.Description && quiz.Topic == draft.Topic
            && quiz.EstimatedMinutes == draft.EstimatedMinutes && quiz.QuestionsJson == json)
            return Ok(DraftView(quiz));

        var updatedAt = DateTime.UtcNow;
        var changes = await db.KnowledgeCheckQuizzes.Where(current => current.Id == id && current.Revision == quiz.Revision)
            .ExecuteUpdateAsync(set => set.SetProperty(current => current.Title, draft.Title)
                .SetProperty(current => current.Description, draft.Description)
                .SetProperty(current => current.Topic, draft.Topic)
                .SetProperty(current => current.EstimatedMinutes, draft.EstimatedMinutes)
                .SetProperty(current => current.QuestionsJson, json)
                .SetProperty(current => current.QuestionCount, draft.Questions.Length)
                .SetProperty(current => current.UpdatedAtUtc, updatedAt)
                .SetProperty(current => current.Revision, current => current.Revision + 1), ct);
        return changes == 0 ? Conflict(new { error = "draft_changed" })
            : Ok(new KnowledgeCheckDraftView(id, draft.Title, draft.Description, draft.Topic, draft.EstimatedMinutes,
                draft.Questions, updatedAt, quiz.Revision + 1));
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, KnowledgeCheckPublish request, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        if (!UserClaims.IsTeacher(User)) return Forbid();
        var quiz = await GetOwned(id, sub, ct);
        if (quiz is null) return NotFound();
        if (request.FeedbackMode is not (AfterSubmit or Manual) || request.MaxAttempts is < 1 or > 100
            || request.HoldId is null && (request.MaxAttempts is not null || request.FeedbackMode != AfterSubmit)
            || request.RequestId == Guid.Empty) return BadRequest(new { error = "invalid_publication" });
        if (request.HoldId is Guid hold && !await courses.CanManageAsync(hold, User, ct)) return NotFound();
        if (request.Revision is long revision && revision != quiz.Revision) return Conflict(new { error = "draft_changed" });
        if (quiz.QuestionCount == 0) return DraftError(null);
        var questions = Read<KnowledgeCheckQuestion>(quiz.QuestionsJson);
        var invalidQuestion = Array.FindIndex(questions, question => !IsReady(question));
        if (invalidQuestion >= 0) return DraftError(invalidQuestion);
        if (request.HoldId is null && questions.Any(question => question.ManualGrading))
            return BadRequest(new { error = "manual_grading_requires_hold" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Lock the exact draft revision while finding or creating its immutable snapshot.
        var locked = await db.KnowledgeCheckQuizzes.Where(row => row.Id == id && row.Revision == quiz.Revision)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.Revision, row => row.Revision), ct);
        if (locked == 0) return Conflict(new { error = "draft_changed" });
        var version = await db.KnowledgeCheckVersions.SingleOrDefaultAsync(row => row.QuizId == id && row.DraftRevision == quiz.Revision, ct);
        if (version is null)
        {
            version = new KnowledgeCheckVersion
            {
                Id = Guid.NewGuid(), QuizId = id, DraftRevision = quiz.Revision, Title = quiz.Title, Description = quiz.Description,
                Topic = quiz.Topic, EstimatedMinutes = quiz.EstimatedMinutes, QuestionsJson = quiz.QuestionsJson, QuestionCount = quiz.QuestionCount
            };
            db.KnowledgeCheckVersions.Add(version);
        }
        var publication = request.RequestId is Guid requestId
            ? await db.KnowledgeCheckPublications.SingleOrDefaultAsync(row => row.VersionId == version.Id && row.RequestId == requestId, ct)
            : null;
        if (publication is not null && (publication.HoldId != request.HoldId || publication.MaxAttempts != request.MaxAttempts
            || publication.FeedbackMode != request.FeedbackMode)) return Conflict(new { error = "request_conflict" });
        if (publication is null)
        {
            publication = new KnowledgeCheckPublication
            {
                Id = Guid.NewGuid(), Version = version, HoldId = request.HoldId, RequestId = request.RequestId,
                MaxAttempts = request.MaxAttempts, FeedbackMode = request.FeedbackMode,
                IsActive = true, CreatedAtUtc = DateTime.UtcNow
            };
            db.KnowledgeCheckPublications.Add(publication);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return await GetPublication(publication.Id, null, null, ct);
    }

    [HttpGet("publications/{id:guid}")]
    public async Task<IActionResult> GetPublication(Guid id, [FromQuery] Guid? pageId, [FromQuery] string? blockId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetVisiblePublication(id, pageId, blockId, ct);
        if (publication is null) return NotFound();
        return Ok(await OwnPublicationView(publication, sub, ct));
    }

    private Task<KnowledgeCheckQuiz?> GetOwned(Guid id, Guid sub, CancellationToken ct) =>
        db.KnowledgeCheckQuizzes.AsNoTracking().SingleOrDefaultAsync(quiz => quiz.Id == id && quiz.OwnerSub == sub, ct);

    private IQueryable<KnowledgeCheckPublication> PublicationQuery() =>
        db.KnowledgeCheckPublications.AsNoTracking().Include(row => row.Version).ThenInclude(row => row.Quiz).Include(row => row.Hold);

    private async Task<KnowledgeCheckPublication?> GetVisiblePublication(Guid id, Guid? pageId, string? blockId, CancellationToken ct)
    {
        if ((pageId is null) != (blockId is null)) return null;
        var publication = await PublicationQuery().SingleOrDefaultAsync(row => row.Id == id, ct);
        if (publication is null || publication.HoldId is Guid hold && !await courses.CanAccessAsync(hold, User, ct)) return null;
        if (pageId is Guid page && !await KnowledgeCheckNotebookAccess.CanReadAsync(db, page, blockId!, id, User, ct)) return null;
        return publication;
    }

    private async Task<bool> CanManage(KnowledgeCheckPublication publication, Guid sub, CancellationToken ct) =>
        UserClaims.IsTeacher(User) && (publication.HoldId is Guid hold
            ? await courses.CanManageAsync(hold, User, ct)
            : publication.Version.Quiz.OwnerSub == sub);

    private async Task<KnowledgeCheckPublicationView> OwnPublicationView(KnowledgeCheckPublication publication, Guid sub, CancellationToken ct)
    {
        var query = db.KnowledgeCheckAttempts.AsNoTracking().Where(row => row.PublicationId == publication.Id && row.UserSub == sub);
        var latest = await query.OrderByDescending(row => row.StartedAtUtc).ThenBy(row => row.Id)
            .Select(row => new { row.Id, row.Status }).FirstOrDefaultAsync(ct);
        return View(publication, await CanManage(publication, sub, ct), latest?.Status == Started ? latest.Id : null, latest?.Id,
            await query.CountAsync(ct));
    }

    private static KnowledgeCheckPublicationView View(KnowledgeCheckPublication row, bool manage, Guid? active = null, Guid? latest = null,
        int used = 0) => new(row.Id, row.Version.QuizId, row.Version.Title, row.Version.Description, row.Version.Topic, row.Version.EstimatedMinutes,
        row.Version.QuestionCount, row.HoldId, row.Hold?.Name, row.MaxAttempts, row.IsActive, manage, row.FeedbackMode,
        row.FeedbackReleased, active, latest, used, row.CreatedAtUtc);

    private static KnowledgeCheckDraftView DraftView(KnowledgeCheckQuiz quiz) =>
        new(quiz.Id, quiz.Title, quiz.Description, quiz.Topic, quiz.EstimatedMinutes, Read<KnowledgeCheckQuestion>(quiz.QuestionsJson),
            quiz.UpdatedAtUtc, quiz.Revision);

    private static KnowledgeCheckQuiz NewDraftQuiz(Guid id, Guid owner, KnowledgeCheckDraft draft, DateTime now) =>
        new()
        {
            Id = id, OwnerSub = owner, Title = draft.Title, Description = draft.Description, Topic = draft.Topic,
            EstimatedMinutes = draft.EstimatedMinutes, QuestionsJson = Write(draft.Questions),
            QuestionCount = draft.Questions.Length, UpdatedAtUtc = now, Revision = 1
        };

    private BadRequestObjectResult DraftError(int? index) => index is int questionIndex
        ? BadRequest(new { error = "invalid_question", questionIndex })
        : BadRequest(new { error = "invalid_draft" });
}
