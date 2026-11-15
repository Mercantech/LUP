using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Mercantec.Tech.Api.KnowledgeChecks.KnowledgeCheckRules;

namespace Mercantec.Tech.Api.Controllers;

public sealed partial class KnowledgeChecksController
{
    [HttpPost("publications/{id:guid}/close")]
    public async Task<IActionResult> ClosePublication(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetManagedPublication(id, sub, holdOnly: false, ct);
        if (publication is null) return NotFound();
        await db.KnowledgeCheckPublications.Where(row => row.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.IsActive, false), ct);
        return await GetPublication(id, null, null, ct);
    }

    [HttpPost("publications/{id:guid}/release-feedback")]
    public async Task<IActionResult> ReleaseFeedback(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetManagedPublication(id, sub, holdOnly: true, ct);
        if (publication is null) return NotFound();
        if (publication.FeedbackMode != Manual) return Conflict(new { error = "feedback_already_available" });
        await db.KnowledgeCheckPublications.Where(row => row.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.FeedbackReleased, true), ct);
        return await GetPublication(id, null, null, ct);
    }

    [HttpGet("publications/{id:guid}/report")]
    public async Task<IActionResult> GetReport(Guid id, CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetManagedPublication(id, sub, holdOnly: true, ct);
        if (publication?.HoldId is not Guid hold) return NotFound();

        var submissions = await db.KnowledgeCheckAttempts.AsNoTracking()
            .Where(row => row.PublicationId == id && row.SubmittedAtUtc != null)
            .OrderByDescending(row => row.SubmittedAtUtc).ThenByDescending(row => row.StartedAtUtc).ThenBy(row => row.Id)
            .ToArrayAsync(ct);
        var students = submissions.Select(row => row.UserSub).Distinct().ToArray();
        var names = await db.Memberships.AsNoTracking()
            .Where(row => row.HoldId == hold && row.UserSub != null && students.Contains(row.UserSub.Value))
            .Select(row => new { Sub = row.UserSub!.Value, row.DisplayName }).ToDictionaryAsync(row => row.Sub, row => row.DisplayName, ct);
        var profileNames = await db.ProfileNames.AsNoTracking().Where(row => students.Contains(row.UserSub))
            .ToDictionaryAsync(row => row.UserSub, row => row.DisplayName, ct);
        string Name(Guid student) => profileNames.GetValueOrDefault(student) ?? names.GetValueOrDefault(student) ?? "Elev";

        var latest = submissions.DistinctBy(row => row.UserSub).ToArray();
        var participants = latest.Select(row => new KnowledgeCheckParticipant(row.Id, Name(row.UserSub), row.Status, row.SubmittedAtUtc)).ToArray();
        var responses = latest.SelectMany(row => Read<KnowledgeCheckAnswer>(row.AnswersJson)).ToLookup(row => row.QuestionId);
        var grades = latest.SelectMany(row => Read<StoredGrade>(row.GradesJson)).ToLookup(row => row.QuestionId);
        var questions = Read<KnowledgeCheckQuestion>(publication.Version.QuestionsJson);
        var reports = questions.Select(question =>
        {
            var answers = responses[question.Id];
            var evaluated = grades[question.Id];
            var counts = new int[question.Options.Length];
            foreach (var index in answers.SelectMany(answer => answer.SelectedOptions)) counts[index]++;
            return new KnowledgeCheckQuestionReport(question.Id, question.Prompt, question.Kind, question.Options, counts,
                evaluated.Count(grade => grade.Correct == true), evaluated.Count(grade => grade.Correct is not null), answers.Count());
        }).ToArray();
        var prompts = questions.ToDictionary(row => row.Id, row => row.Prompt);
        var pending = submissions.Where(row => row.Status == Submitted).SelectMany(row =>
        {
            var answers = Read<KnowledgeCheckAnswer>(row.AnswersJson).ToDictionary(answer => answer.QuestionId);
            return Read<StoredGrade>(row.GradesJson).Where(grade => grade.Correct is null)
                .Select(grade => new KnowledgeCheckPendingGrade(row.Id, grade.QuestionId, prompts[grade.QuestionId],
                    answers[grade.QuestionId].Text ?? "", Name(row.UserSub)));
        }).ToArray();
        return Ok(new KnowledgeCheckReport(View(publication, true), participants, reports, pending));
    }

    [HttpPut("publications/{id:guid}/attempts/{attemptId:guid}/grades/{questionId:guid}")]
    public async Task<IActionResult> GradeAttempt(Guid id, Guid attemptId, Guid questionId, KnowledgeCheckGrade request,
        CancellationToken ct)
    {
        if (UserClaims.GetSub(User) is not Guid sub) return Unauthorized();
        var publication = await GetManagedPublication(id, sub, holdOnly: true, ct);
        if (publication is null) return NotFound();
        var comment = request.Comment?.Trim();
        if (comment?.Length > 2000) return BadRequest(new { error = "invalid_grade" });
        if (!Read<KnowledgeCheckQuestion>(publication.Version.QuestionsJson)
            .Any(question => question.Id == questionId && question.ManualGrading)) return NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await LockPublication(id, activeOnly: false, ct)) return NotFound();
        var attempt = await db.KnowledgeCheckAttempts
            .SingleOrDefaultAsync(row => row.Id == attemptId && row.PublicationId == id && row.Status != Started, ct);
        if (attempt is null) return NotFound();
        var replacement = new StoredGrade(questionId, request.Correct, string.IsNullOrEmpty(comment) ? null : comment);
        var grades = Read<StoredGrade>(attempt.GradesJson);
        var index = Array.FindIndex(grades, grade => grade.QuestionId == questionId);
        if (index < 0) return NotFound();
        if (grades[index] != replacement)
        {
            grades[index] = replacement;
            SetGrades(attempt, grades);
            attempt.Revision++;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private async Task<KnowledgeCheckPublication?> GetManagedPublication(Guid id, Guid sub, bool holdOnly, CancellationToken ct)
    {
        var publication = await PublicationQuery()
            .SingleOrDefaultAsync(row => row.Id == id && (!holdOnly || row.HoldId != null), ct);
        return publication is not null && await CanManage(publication, sub, ct) ? publication : null;
    }
}
