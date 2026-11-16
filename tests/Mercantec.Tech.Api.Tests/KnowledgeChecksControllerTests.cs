using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class KnowledgeChecksControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Guid _teacher = Guid.NewGuid(), _otherTeacher = Guid.NewGuid(), _student = Guid.NewGuid(),
        _peer = Guid.NewGuid(), _outsider = Guid.NewGuid();
    private Guid _hold;
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "GF2", Forloeb = new Forloeb
        {
            Id = Guid.NewGuid(), Name = "Programmering", CreatedBySub = _teacher
        } };
        _hold = hold.Id;
        _db.Memberships.AddRange(
            new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = _student, DisplayName = "Sofie" },
            new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = _peer, DisplayName = "Jens" });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task FreePracticeIsPrivateAndHoldPublicationRequiresMembership()
    {
        var question = Choice();
        var teacher = As(_teacher, true);
        var draft = Read<KnowledgeCheckDraftView>(await teacher.Create(Draft(question), default));
        var free = Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, new(null, null, "after-submit"), default));
        var assigned = Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, new(_hold, 1, "manual"), default));

        Assert.IsType<UnauthorizedResult>(await As(null).List(default));
        Assert.Empty(Read<KnowledgeCheckIndex>(await As(_student).List(default)).Owned);
        Assert.IsType<ForbidResult>(await As(_student).GetDraft(draft.Id, default));
        Assert.IsType<NotFoundResult>(await As(_otherTeacher, true).GetDraft(draft.Id, default));
        Assert.IsType<NotFoundResult>(await As(_outsider).GetPublication(assigned.Id, null, null, default));
        Assert.IsType<NotFoundResult>(await As(_otherTeacher, true).GetReport(assigned.Id, default));
        Assert.Equal(free.Id, Assert.Single(Read<KnowledgeCheckIndex>(await As(_outsider).List(default)).Publications).Id);

        var attempt = Read<KnowledgeCheckAttemptView>(await As(_outsider).StartAttempt(free.Id, new(), null, null, default));
        Assert.IsType<NotFoundResult>(await As(_student).GetAttempt(free.Id, attempt.Id, null, null, default));
        Assert.IsType<NotFoundResult>(await teacher.GetReport(free.Id, default));
        Assert.IsType<NotFoundResult>(await As(_outsider).GetPublication(free.Id, Guid.NewGuid(), null, default));
        Assert.IsType<NotFoundResult>(await As(_outsider).GetPublication(free.Id, null, "block", default));
    }

    [Fact]
    public async Task AttemptsResumeGradeAndProtectFeedbackUntilReleased()
    {
        var single = Choice() with
        {
            OptionExplanations = ["  Den første forklaring.  ", "Den anden forklaring."],
            LearnMoreUrl = "https://example.com/guide",
            LearnMoreTitle = "  Kildens navn  ",
            LearnMoreDescription = "Læs om svarmulighederne."
        };
        var multiple = Choice() with { Kind = "multiple", CorrectOptions = [0, 1] };
        var boolean = Choice() with { Kind = "boolean", CorrectOptions = [0] };
        var automatic = Question("short", accepted: ["  Øl & blåbær  "]);
        var manual = Question("short") with { ManualGrading = true };
        var teacher = As(_teacher, true);
        var draft = Read<KnowledgeCheckDraftView>(await teacher.Create(Draft(single, multiple, boolean, automatic, manual), default));
        Assert.IsType<BadRequestObjectResult>(await teacher.Publish(draft.Id, new(null, null, "after-submit"), default));
        var publication = Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, new(_hold, 1, "manual"), default));
        var student = As(_student);
        var first = Read<KnowledgeCheckAttemptView>(await student.StartAttempt(publication.Id, new(), null, null, default));
        Assert.Null(first.Results);
        Assert.DoesNotContain("DEN KORREKTE FORKLARING", JsonSerializer.Serialize(first));
        Assert.DoesNotContain("Den første forklaring.", JsonSerializer.Serialize(first));
        Assert.DoesNotContain("Kildens navn", JsonSerializer.Serialize(first));
        Assert.IsType<ConflictObjectResult>(
            await student.SubmitAttempt(publication.Id, first.Id, new(first.Revision), null, null, default));

        KnowledgeCheckAnswer[] answers =
        [
            new(single.Id, [0], null), new(multiple.Id, [1, 0], null), new(boolean.Id, [0], null),
            new(automatic.Id, [], "øl  &  BLÅBÆR"), new(manual.Id, [], "Min begrundelse")
        ];
        var save = new KnowledgeCheckSave(first.Revision, 4, answers);
        var saved = Read<KnowledgeCheckAttemptView>(await student.SaveAttempt(publication.Id, first.Id, save, null, null, default));
        var replay = Read<KnowledgeCheckAttemptView>(await student.SaveAttempt(publication.Id, first.Id, save, null, null, default));
        Assert.Equal(saved.Revision, replay.Revision);
        Assert.IsType<ConflictObjectResult>(await student.SaveAttempt(publication.Id, first.Id, save with { QuestionIndex = 1 }, null, null, default));
        var resumed = Read<KnowledgeCheckAttemptView>(await student.StartAttempt(publication.Id, new(), null, null, default));
        Assert.Equal(first.Id, resumed.Id);
        Assert.Equal(4, resumed.QuestionIndex);
        Assert.Equal(5, resumed.Answers.Length);
        Assert.Equal(new[] { "Sandt", "Falsk" }, resumed.Questions[2].Options);

        var submitted = Read<KnowledgeCheckAttemptView>(await student.SubmitAttempt(publication.Id, first.Id, new(resumed.Revision), null, null, default));
        Assert.Equal("submitted", submitted.Status);
        Assert.Null(submitted.Score);
        Assert.IsType<ConflictObjectResult>(await student.StartAttempt(publication.Id, new(), null, null, default));
        Assert.IsType<NotFoundResult>(await As(_peer).GetAttempt(publication.Id, first.Id, null, null, default));
        var report = Read<KnowledgeCheckReport>(await teacher.GetReport(publication.Id, default));
        Assert.Equal("Sofie", Assert.Single(report.Participants).DisplayName);
        var pending = Assert.Single(report.PendingGrades);
        Assert.Equal(manual.Id, pending.QuestionId);
        Assert.Equal("Min begrundelse", pending.Answer);
        Assert.Equal(new[] { 1, 1 }, report.Questions[1].OptionCounts);
        Assert.Equal(1, report.Questions[3].CorrectCount);
        Assert.Equal(0, report.Questions[4].GradedCount);
        Assert.IsType<NotFoundResult>(await As(_peer).GradeAttempt(publication.Id, first.Id, manual.Id, new(true, null), default));
        Assert.IsType<NoContentResult>(await teacher.GradeAttempt(publication.Id, first.Id, manual.Id, new(true, "God begrundelse"), default));
        var graded = Read<KnowledgeCheckAttemptView>(await student.GetAttempt(publication.Id, first.Id, null, null, default));
        Assert.Equal("graded", graded.Status);
        Assert.Null(graded.Score);
        Assert.Null(graded.Results);
        await teacher.ReleaseFeedback(publication.Id, default);
        var result = Read<KnowledgeCheckAttemptView>(await student.GetAttempt(publication.Id, first.Id, null, null, default));
        Assert.Equal(5, result.Score);
        Assert.Equal("God begrundelse", result.Results![4].Comment);
        Assert.Equal(new[] { "Den første forklaring.", "Den anden forklaring." }, result.Results[0].OptionExplanations);
        Assert.Equal("https://example.com/guide", result.Results[0].LearnMoreUrl);
        Assert.Equal("Kildens navn", result.Results[0].LearnMoreTitle);
        Assert.Equal("Læs om svarmulighederne.", result.Results[0].LearnMoreDescription);
        Assert.Null(Assert.Single(Read<KnowledgeCheckIndex>(await As(_peer).List(default)).Publications).LatestAttemptId);
    }

    [Fact]
    public async Task CheckedFreeAnswersArePrivatePersistentAndCannotBeChangedByAutosave()
    {
        var first = Choice() with
        {
            Kind = "multiple", Options = ["A", "B", "C"], CorrectOptions = [1, 2],
            Explanation = "Forklarer det kontrollerede svar",
            OptionExplanations = ["Kun A", "Kun B", "Kun C"],
            LearnMoreUrl = "https://example.com/forklaring",
            LearnMoreTitle = "Læs om spørgsmålet"
        };
        var second = Question("short", accepted: ["æble og pære", "UNIKT_ANDENT_FACIT"]) with { Explanation = "HEMMELIG_FORKLARING" };
        var teacher = As(_teacher, true);
        var draft = Read<KnowledgeCheckDraftView>(await teacher.Create(Draft(first, second), default));
        var publication = Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, new(null, null, "after-submit"), default));
        var student = As(_student);
        var attempt = Read<KnowledgeCheckAttemptView>(await student.StartAttempt(publication.Id, new(), null, null, default));
        Assert.Null(attempt.CheckedResults);
        var selected = new KnowledgeCheckAnswer(first.Id, [2, 0], null);
        var request = new KnowledgeCheckCheck(attempt.Revision, selected);
        Assert.IsType<NotFoundResult>(await As(_peer).CheckAnswer(publication.Id, attempt.Id, request, null, null, default));
        Assert.IsType<NotFoundResult>(await student.CheckAnswer(publication.Id, attempt.Id, request,
            Guid.NewGuid(), "unbound-block", default));
        Assert.IsType<BadRequestObjectResult>(await student.CheckAnswer(publication.Id, attempt.Id,
            request with { Answer = selected with { SelectedOptions = [] } }, null, null, default));

        var checkedAttempt = Read<KnowledgeCheckAttemptView>(await student.CheckAnswer(publication.Id, attempt.Id, request, null, null, default));
        Assert.Equal("started", checkedAttempt.Status);
        Assert.Equal(attempt.Revision + 1, checkedAttempt.Revision);
        Assert.Null(checkedAttempt.Score);
        Assert.Null(checkedAttempt.Results);
        var result = Assert.Single(checkedAttempt.CheckedResults!);
        Assert.Equal(first.Id, result.QuestionId);
        Assert.False(result.Correct);
        Assert.Equal(first.CorrectOptions, result.CorrectOptions);
        Assert.Equal(first.OptionExplanations, result.OptionExplanations);
        Assert.Equal(first.LearnMoreTitle, result.LearnMoreTitle);
        var response = JsonSerializer.Serialize(checkedAttempt);
        Assert.DoesNotContain(second.Explanation, response, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIKT_ANDENT_FACIT", response, StringComparison.Ordinal);

        var replay = Read<KnowledgeCheckAttemptView>(await student.CheckAnswer(publication.Id, attempt.Id,
            request with { Answer = selected with { SelectedOptions = [0, 2] } }, null, null, default));
        Assert.Equal(checkedAttempt.Revision, replay.Revision);
        Assert.IsType<ConflictObjectResult>(await student.CheckAnswer(publication.Id, attempt.Id,
            request with { Revision = replay.Revision, Answer = selected with { SelectedOptions = [1, 2] } }, null, null, default));
        var oldClientRetry = Read<KnowledgeCheckAttemptView>(await student.SaveAttempt(publication.Id, attempt.Id,
            new(attempt.Revision, 0, []), null, null, default));
        Assert.Equal(replay.Revision, oldClientRetry.Revision);
        Assert.Equal(new[] { 0, 2 }, Assert.Single(oldClientRetry.Answers).SelectedOptions);

        var otherAnswer = new KnowledgeCheckAnswer(second.Id, [], "  ÆBLE  OG  PÆRE  ");
        Assert.IsType<ConflictObjectResult>(await student.CheckAnswer(publication.Id, attempt.Id,
            new(attempt.Revision, otherAnswer), null, null, default));
        var changed = new KnowledgeCheckSave(replay.Revision, 1, [selected with { SelectedOptions = [1, 2] }, otherAnswer]);
        var saved = Read<KnowledgeCheckAttemptView>(await student.SaveAttempt(publication.Id, attempt.Id, changed, null, null, default));
        Assert.Equal(new[] { 0, 2 }, saved.Answers[0].SelectedOptions);
        Assert.False(Assert.Single(saved.CheckedResults!).Correct);
        Assert.Equal(1, saved.QuestionIndex);

        var resumed = Read<KnowledgeCheckAttemptView>(await student.GetAttempt(publication.Id, attempt.Id, null, null, default));
        Assert.Equal(first.Id, Assert.Single(resumed.CheckedResults!).QuestionId);
        var secondChecked = Read<KnowledgeCheckAttemptView>(await student.CheckAnswer(publication.Id, attempt.Id,
            new(resumed.Revision, otherAnswer), null, null, default));
        Assert.Equal(2, secondChecked.CheckedResults!.Length);
        Assert.True(secondChecked.CheckedResults[1].Correct);
        var submitted = Read<KnowledgeCheckAttemptView>(await student.SubmitAttempt(publication.Id, attempt.Id,
            new(secondChecked.Revision), null, null, default));
        Assert.Equal(1, submitted.Score);
        Assert.Null(submitted.CheckedResults);
        Assert.Equal(2, submitted.Results!.Length);
        Assert.False(submitted.Results[0].Correct);
    }

    [Theory]
    [InlineData("after-submit")]
    [InlineData("manual")]
    public async Task HoldAttemptsCannotCheckIndividualAnswers(string feedbackMode)
    {
        var question = Choice();
        var teacher = As(_teacher, true);
        var draft = Read<KnowledgeCheckDraftView>(await teacher.Create(Draft(question), default));
        var hold = Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, new(_hold, 1, feedbackMode), default));
        var student = As(_student);
        var attempt = Read<KnowledgeCheckAttemptView>(await student.StartAttempt(hold.Id, new(), null, null, default));
        Assert.IsType<ForbidResult>(await student.CheckAnswer(hold.Id, attempt.Id,
            new(attempt.Revision, new(question.Id, [0], null)), null, null, default));
        var resumed = Read<KnowledgeCheckAttemptView>(await student.GetAttempt(hold.Id, attempt.Id, null, null, default));
        Assert.Equal(attempt.Revision, resumed.Revision);
        Assert.Empty(resumed.Answers);
        Assert.Null(resumed.CheckedResults);
        Assert.Null(resumed.Results);
    }

    [Fact]
    public async Task VersionAndAttemptsRemainStableAcrossRepublishRetrialAndClose()
    {
        var question = Choice() with
        {
            OptionExplanations = ["Den oprindelige forklaring.", ""],
            LearnMoreUrl = "https://example.com/oprindelig",
            LearnMoreTitle = "Oprindelig kilde"
        };
        var teacher = As(_teacher, true);
        var draft = Read<KnowledgeCheckDraftView>(await teacher.Create(Draft(question), default));
        var publish = new KnowledgeCheckPublish(_hold, 2, "after-submit", draft.Revision, Guid.NewGuid());
        var initial = Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, publish, default));
        Assert.Equal(initial.Id, Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, publish, default)).Id);
        var students = As(_student);
        var action = new KnowledgeCheckStart(Guid.NewGuid());
        var first = Read<KnowledgeCheckAttemptView>(await students.StartAttempt(initial.Id, action, null, null, default));
        var saved = Read<KnowledgeCheckAttemptView>(await students.SaveAttempt(initial.Id, first.Id,
            new(first.Revision, 0, [new(question.Id, [0], null)]), null, null, default));
        var submitted = Read<KnowledgeCheckAttemptView>(await students.SubmitAttempt(initial.Id, first.Id, new(saved.Revision), null, null, default));
        var duplicate = Read<KnowledgeCheckAttemptView>(await students.SubmitAttempt(initial.Id, first.Id, new(saved.Revision), null, null, default));
        Assert.Equal(submitted.SubmittedAtUtc, duplicate.SubmittedAtUtc);
        Assert.Equal(submitted.Id, Read<KnowledgeCheckAttemptView>(await students.StartAttempt(initial.Id, action, null, null, default)).Id);

        var second = Read<KnowledgeCheckAttemptView>(await students.StartAttempt(initial.Id, new(), null, null, default));
        var secondSaved = Read<KnowledgeCheckAttemptView>(await students.SaveAttempt(initial.Id, second.Id,
            new(second.Revision, 0, [new(question.Id, [1], null)]), null, null, default));
        var secondResult = Read<KnowledgeCheckAttemptView>(await students.SubmitAttempt(initial.Id, second.Id, new(secondSaved.Revision), null, null, default));
        Assert.Equal(0, secondResult.Score);
        var report = Read<KnowledgeCheckReport>(await teacher.GetReport(initial.Id, default));
        Assert.Equal(second.Id, Assert.Single(report.Participants).AttemptId);
        Assert.Equal(new[] { 0, 1 }, Assert.Single(report.Questions).OptionCounts);

        var change = Draft(question with
        {
            CorrectOptions = [1],
            OptionExplanations = ["", "Den nye forklaring."],
            LearnMoreUrl = "https://example.com/ny",
            LearnMoreTitle = "Ny kilde"
        }) with { Revision = draft.Revision, Title = "Nyt navn" };
        var updated = Read<KnowledgeCheckDraftView>(await teacher.Update(draft.Id, change, default));
        Assert.IsType<ConflictObjectResult>(await teacher.Update(draft.Id, change, default));
        var next = Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, publish with
        {
            Revision = updated.Revision, RequestId = Guid.NewGuid()
        }, default));
        Assert.NotEqual(initial.Id, next.Id);
        await teacher.ClosePublication(initial.Id, default);
        Assert.IsType<ConflictObjectResult>(await students.StartAttempt(initial.Id, new(), null, null, default));
        var historical = Read<KnowledgeCheckAttemptView>(await students.GetAttempt(initial.Id, first.Id, null, null, default));
        Assert.Equal(1, historical.Score);
        var historicalFeedback = Assert.Single(historical.Results!);
        Assert.Equal("Den oprindelige forklaring.", historicalFeedback.OptionExplanations![0]);
        Assert.Equal("Oprindelig kilde", historicalFeedback.LearnMoreTitle);
        Assert.Equal("https://example.com/oprindelig", historicalFeedback.LearnMoreUrl);
        var list = Read<KnowledgeCheckIndex>(await students.List(default));
        Assert.Equal(next.Id, Assert.Single(list.Publications).Id);
        Assert.All(list.History, item => Assert.True(item.CanOpen));
        Assert.Equal(2, list.History.Length);
    }

    [Fact]
    public async Task PilotIsAnOwnedEditableDraft()
    {
        var teacher = As(_teacher, true);
        var pilot = Read<KnowledgeCheckDraftView>(await teacher.CreatePilot(default));
        Assert.Contains("Control Flow", pilot.Title);
        Assert.NotEmpty(pilot.Questions);
        Assert.IsType<NotFoundResult>(await As(_otherTeacher, true).GetDraft(pilot.Id, default));
        Assert.Empty(Read<KnowledgeCheckIndex>(await As(_student).List(default)).Publications);
        Assert.Equal(pilot.Id, Assert.Single(Read<KnowledgeCheckIndex>(await teacher.List(default)).Owned).Id);
    }

    [Fact]
    public async Task ConcurrentStartsAndSubmitsAcrossDatabaseContextsKeepOneAttempt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lup-knowledge-check-race-{Guid.NewGuid():N}.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 15 }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var question = Choice();
        try
        {
            await using var setup = new AppDbContext(options);
            await setup.Database.EnsureCreatedAsync();
            await setup.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            var teacher = As(_teacher, true, setup);
            var draft = Read<KnowledgeCheckDraftView>(await teacher.Create(Draft(question), default));
            var publication = Read<KnowledgeCheckPublicationView>(await teacher.Publish(draft.Id, new(null, null, "after-submit"), default));
            await using var firstDb = new AppDbContext(options);
            await using var secondDb = new AppDbContext(options);
            var firstStudent = As(_student, context: firstDb);
            var secondStudent = As(_student, context: secondDb);

            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<IActionResult> Start(KnowledgeChecksController controller)
            {
                await startGate.Task;
                return await controller.StartAttempt(publication.Id, new(Guid.NewGuid()), null, null, default);
            }
            var firstStart = Task.Run(() => Start(firstStudent));
            var secondStart = Task.Run(() => Start(secondStudent));
            startGate.SetResult();
            var starts = await Task.WhenAll(firstStart, secondStart).WaitAsync(TimeSpan.FromSeconds(20));
            var attempt = Read<KnowledgeCheckAttemptView>(starts[0]);
            Assert.Equal(attempt.Id, Read<KnowledgeCheckAttemptView>(starts[1]).Id);
            firstDb.ChangeTracker.Clear();
            secondDb.ChangeTracker.Clear();

            var saved = Read<KnowledgeCheckAttemptView>(await firstStudent.SaveAttempt(publication.Id, attempt.Id,
                new(attempt.Revision, 0, [new(question.Id, [0], null)]), null, null, default));
            firstDb.ChangeTracker.Clear();
            var submitGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<IActionResult> Submit(KnowledgeChecksController controller)
            {
                await submitGate.Task;
                return await controller.SubmitAttempt(publication.Id, attempt.Id, new(saved.Revision), null, null, default);
            }
            var firstSubmit = Task.Run(() => Submit(firstStudent));
            var secondSubmit = Task.Run(() => Submit(secondStudent));
            submitGate.SetResult();
            var results = await Task.WhenAll(firstSubmit, secondSubmit).WaitAsync(TimeSpan.FromSeconds(20));
            var version = Read<KnowledgeCheckAttemptView>(results[0]);
            var duplicate = Read<KnowledgeCheckAttemptView>(results[1]);
            Assert.Equal("graded", version.Status);
            Assert.Equal(1, version.Score);
            Assert.Equal(version.Revision, duplicate.Revision);
            Assert.Equal(version.SubmittedAtUtc, duplicate.SubmittedAtUtc);
            Assert.Equal(attempt.Id, Assert.Single(await setup.KnowledgeCheckAttempts.AsNoTracking().ToListAsync()).Id);
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private KnowledgeChecksController As(Guid? sub, bool teacher = false, AppDbContext? context = null)
    {
        var claims = new List<Claim>();
        if (sub is Guid id) claims.Add(new("sub", id.ToString()));
        if (teacher) claims.Add(new("role", "Underviser"));
        var db = context ?? _db;
        return new(db, new CourseAccess(db))
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, sub is null ? null : "test")) }
            }
        };
    }

    private static KnowledgeCheckDraft Draft(params KnowledgeCheckQuestion[] questions) =>
        new("GF2 Control Flow", "Vælg svaret", "GF2", 8, questions);

    private static KnowledgeCheckQuestion Choice() => Question("single") with { Options = ["Sandt", "Falsk"], CorrectOptions = [0] };

    private static KnowledgeCheckQuestion Question(string kind, string[]? accepted = null) =>
        new(Guid.NewGuid(), kind, "Dit svar?", [], [], accepted ?? [], false, "DEN KORREKTE FORKLARING", "/notion");

    private static T Read<T>(IActionResult result) =>
        Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
}
