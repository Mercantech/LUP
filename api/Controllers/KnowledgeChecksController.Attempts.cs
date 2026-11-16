using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class KnowledgeChecksController
{
    [HttpPost("publications/{id:guid}/attempts")]
    public async Task<IActionResult> StartAttempt(Guid id, KnowledgeCheckStart request, [FromQuery] Guid? pageId,
        [FromQuery] string? blockId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetVisiblePublication(id, pageId, blockId, ct);
        if (publication is null) return NotFound();
        if (request.RequestId == Guid.Empty) return BadRequest(new { error = "invalid_attempt" });
        if (publication.HoldId is Guid hold && !await db.Memberships.AnyAsync(row => row.HoldId == hold && row.UserSub == sub, ct))
            return Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await LockPublication(id, activeOnly: true, ct)) return Closed();

        var own = db.KnowledgeCheckAttempts.Where(row => row.PublicationId == id && row.UserSub == sub);
        var replay = request.RequestId is Guid requestId
            ? await own.AsNoTracking().SingleOrDefaultAsync(row => row.RequestId == requestId, ct) : null;
        var attempt = replay ?? await own.AsNoTracking().SingleOrDefaultAsync(row => row.Status == Started, ct);
        if (attempt is null)
        {
            if (publication.MaxAttempts is int max && await own.CountAsync(ct) >= max)
                return Conflict(new { error = "attempt_limit" });
            attempt = new KnowledgeCheckAttempt
            {
                Id = Guid.NewGuid(), PublicationId = id, UserSub = sub, RequestId = request.RequestId,
                Status = Started, StartedAtUtc = DateTime.UtcNow, Revision = 1
            };
            db.KnowledgeCheckAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Ok(AttemptView(publication, attempt));
    }

    [HttpGet("publications/{id:guid}/attempts/{attemptId:guid}")]
    public async Task<IActionResult> GetAttempt(Guid id, Guid attemptId, [FromQuery] Guid? pageId, [FromQuery] string? blockId,
        CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetVisiblePublication(id, pageId, blockId, ct);
        if (publication is null) return NotFound();
        var attempt = await OwnedAttempt(id, attemptId, sub).AsNoTracking().SingleOrDefaultAsync(ct);
        if (attempt is null) return NotFound();
        if (attempt.Status == Started && !publication.IsActive) return Closed();
        return Ok(AttemptView(publication, attempt));
    }

    [HttpPut("publications/{id:guid}/attempts/{attemptId:guid}")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> SaveAttempt(Guid id, Guid attemptId, KnowledgeCheckSave request, [FromQuery] Guid? pageId,
        [FromQuery] string? blockId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetVisiblePublication(id, pageId, blockId, ct);
        if (publication is null) return NotFound();

        var questions = Read<KnowledgeCheckQuestion>(publication.Version.QuestionsJson);
        var answers = NormalizeAnswers(questions, request);
        if (answers is null) return BadRequest(new { error = "invalid_answer" });
        var order = questions.Select((question, index) => (question.Id, index)).ToDictionary(row => row.Id, row => row.index);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await LockPublication(id, activeOnly: true, ct)) return Closed();
        var attempt = await OwnedAttempt(id, attemptId, sub).SingleOrDefaultAsync(ct);
        if (attempt is null) return NotFound();
        if (attempt.Status != Started) return AttemptChanged();
        if (HasImmediateFeedback(publication))
        {
            var checkedIds = Read<StoredGrade>(attempt.GradesJson).Select(grade => grade.QuestionId).ToHashSet();
            if (checkedIds.Count > 0)
            {
                // All clients of this attempt share the checked answers, including an older embedded runner.
                var locked = Read<KnowledgeCheckAnswer>(attempt.AnswersJson).Where(answer => checkedIds.Contains(answer.QuestionId));
                answers = answers.Where(answer => !checkedIds.Contains(answer.QuestionId)).Concat(locked).ToArray();
            }
        }
        var json = Write(answers.OrderBy(answer => order[answer.QuestionId]).ToArray());
        if (attempt.AnswersJson != json || request.QuestionIndex != attempt.QuestionIndex)
        {
            if (attempt.Revision != request.Revision) return AttemptChanged();
            attempt.AnswersJson = json;
            attempt.QuestionIndex = request.QuestionIndex;
            attempt.Revision++;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Ok(AttemptView(publication, attempt));
    }

    [HttpPost("publications/{id:guid}/attempts/{attemptId:guid}/check")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> CheckAnswer(Guid id, Guid attemptId, KnowledgeCheckCheck request, [FromQuery] Guid? pageId,
        [FromQuery] string? blockId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetVisiblePublication(id, pageId, blockId, ct);
        if (publication is null) return NotFound();
        if (!HasImmediateFeedback(publication)) return Forbid();

        var questions = Read<KnowledgeCheckQuestion>(publication.Version.QuestionsJson);
        var questionIndex = Array.FindIndex(questions, question => question.Id == request.Answer?.QuestionId);
        if (questionIndex < 0 || request.Answer is null || questions[questionIndex].ManualGrading
            || !TryNormalizeAnswer(questions[questionIndex], request.Answer, out var answer) || answer is null)
            return BadRequest(new { error = "invalid_answer" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await LockPublication(id, activeOnly: true, ct)) return Closed();
        var attempt = await OwnedAttempt(id, attemptId, sub).SingleOrDefaultAsync(ct);
        if (attempt is null) return NotFound();
        if (attempt.Status != Started) return AttemptChanged();

        // During a free attempt, GradesJson holds only the questions already checked.
        var checkedGrades = Read<StoredGrade>(attempt.GradesJson);
        var answers = Read<KnowledgeCheckAnswer>(attempt.AnswersJson).ToDictionary(item => item.QuestionId);
        if (checkedGrades.Any(grade => grade.QuestionId == answer.QuestionId))
        {
            var locked = answers[answer.QuestionId];
            if (locked.Text != answer.Text || !locked.SelectedOptions.SequenceEqual(answer.SelectedOptions))
                return AttemptChanged();
        }
        else
        {
            if (attempt.Revision != request.Revision) return AttemptChanged();
            answers[answer.QuestionId] = answer;
            attempt.AnswersJson = Write(questions.Where(question => answers.ContainsKey(question.Id))
                .Select(question => answers[question.Id]).ToArray());
            attempt.GradesJson = Write(checkedGrades.Append(Grade(questions[questionIndex], answer)).ToArray());
            attempt.QuestionIndex = questionIndex;
            attempt.Revision++;
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return Ok(AttemptView(publication, attempt));
    }

    [HttpPost("publications/{id:guid}/attempts/{attemptId:guid}/submit")]
    public async Task<IActionResult> SubmitAttempt(Guid id, Guid attemptId, KnowledgeCheckSubmit request, [FromQuery] Guid? pageId,
        [FromQuery] string? blockId, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetVisiblePublication(id, pageId, blockId, ct);
        if (publication is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await LockPublication(id, activeOnly: false, ct)) return NotFound();
        var attempt = await OwnedAttempt(id, attemptId, sub).SingleOrDefaultAsync(ct);
        if (attempt is null) return NotFound();
        if (attempt.Status != Started)
        {
            await transaction.CommitAsync(ct);
            return Ok(AttemptView(publication, attempt));
        }
        if (!await db.KnowledgeCheckPublications.AnyAsync(row => row.Id == id && row.IsActive, ct)) return Closed();
        if (attempt.Revision != request.Revision) return AttemptChanged();

        var questions = Read<KnowledgeCheckQuestion>(publication.Version.QuestionsJson);
        var answers = Read<KnowledgeCheckAnswer>(attempt.AnswersJson).ToDictionary(answer => answer.QuestionId);
        if (answers.Count != questions.Length) return Conflict(new { error = "incomplete_attempt" });
        SetGrades(attempt, questions.Select(question => Grade(question, answers[question.Id])).ToArray());
        attempt.SubmittedAtUtc = DateTime.UtcNow;
        attempt.Revision++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(AttemptView(publication, attempt));
    }

    private IQueryable<KnowledgeCheckAttempt> OwnedAttempt(Guid publicationId, Guid attemptId, Guid sub) =>
        db.KnowledgeCheckAttempts.Where(row => row.PublicationId == publicationId && row.Id == attemptId && row.UserSub == sub);

    private async Task<bool> LockPublication(Guid id, bool activeOnly, CancellationToken ct)
    {
        // All respondents and publication state changes synchronize on the same row across API instances.
        return await db.KnowledgeCheckPublications.Where(row => row.Id == id && (!activeOnly || row.IsActive))
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.IsActive, row => row.IsActive), ct) != 0;
    }

    private static KnowledgeCheckAttemptView AttemptView(KnowledgeCheckPublication publication, KnowledgeCheckAttempt attempt)
    {
        var questions = Read<KnowledgeCheckQuestion>(publication.Version.QuestionsJson);
        var showResults = attempt.Status == Graded && CanSeeFeedback(publication);
        var showChecks = attempt.Status == Started && HasImmediateFeedback(publication);
        var grades = showResults || showChecks
            ? Read<StoredGrade>(attempt.GradesJson).ToDictionary(grade => grade.QuestionId)
            : null;
        return new(attempt.Id, publication.Id, attempt.Status, attempt.Revision, attempt.QuestionIndex,
            questions.Select(question => new KnowledgeCheckLearnerQuestion(question.Id, question.Prompt, question.Kind, question.Options)).ToArray(),
            Read<KnowledgeCheckAnswer>(attempt.AnswersJson), showResults ? attempt.Score : null,
            showResults ? questions.Select(question => Result(question, grades![question.Id])).ToArray() : null,
            attempt.StartedAtUtc, attempt.SubmittedAtUtc,
            showChecks && grades is { Count: > 0 }
                ? questions.Where(question => grades.ContainsKey(question.Id)).Select(question => Result(question, grades[question.Id])).ToArray()
                : null);
    }

    private static bool HasImmediateFeedback(KnowledgeCheckPublication publication) =>
        publication.HoldId is null && publication.FeedbackMode == AfterSubmit;

    private static KnowledgeCheckResult Result(KnowledgeCheckQuestion question, StoredGrade grade) =>
        new(question.Id, grade.Correct == true, question.CorrectOptions, question.AcceptedAnswers,
            question.Explanation, question.LearnMoreUrl, grade.Comment, question.OptionExplanations,
            question.LearnMoreTitle, question.LearnMoreDescription);

    private ConflictObjectResult Closed() => Conflict(new { error = "publication_closed" });
    private ConflictObjectResult AttemptChanged() => Conflict(new { error = "attempt_changed" });
}
