using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Characters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class CharacterKnowledgeCheckSeriesTests
{
    [Fact]
    public void FirstSeriesUsesElevenDifferentCharacterProfilesAndSixQuestionsEach()
    {
        var entries = CharacterKnowledgeCheckCatalog.All.Values;
        Assert.Equal(11, entries.Count());
        Assert.Equal(11, entries.Select(template => template.CharacterSlug).Distinct(StringComparer.Ordinal).Count());
        Assert.All(entries, template =>
        {
            Assert.NotNull(CharacterCatalog.Find(template.CharacterSlug));
            Assert.Equal(6, template.Draft.Questions.Length);
            Assert.All(template.Draft.Questions, question =>
            {
                Assert.False(question.ManualGrading);
                Assert.False(string.IsNullOrWhiteSpace(question.Explanation));
            });
            Assert.Contains(template.Draft.Questions, question => question.LearnMoreUrl is not null);
        });
    }

    [Fact]
    public async Task ImportCreatesOneSeriesAndKeepsClosedPublicationsAndTeacherEdits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = Guid.NewGuid();
        var otherTeacher = Guid.NewGuid();

        var unverified = await KnowledgeCheckSeriesPublisher.RunCharacterSeriesAsync(db, owner, dryRun: true);
        Assert.Equal("owner_not_verified_as_teacher", Assert.Single(unverified.Issues).Code);
        db.NotebookProfiles.AddRange(
            new NotebookProfile { Sub = owner, IsTeacher = true, DisplayName = "Underviser", UpdatedAtUtc = DateTimeOffset.UtcNow },
            new NotebookProfile { Sub = otherTeacher, IsTeacher = true, DisplayName = "Anden underviser", UpdatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var dryRun = await KnowledgeCheckSeriesPublisher.RunCharacterSeriesAsync(db, owner, dryRun: true);
        Assert.Empty(dryRun.Issues);
        Assert.Equal(11, dryRun.DraftsToAdd);
        Assert.Equal(11, dryRun.PublicationsToAdd);
        Assert.Empty(await db.KnowledgeCheckQuizzes.ToListAsync());

        var first = await KnowledgeCheckSeriesPublisher.RunCharacterSeriesAsync(db, owner, dryRun: false);
        Assert.Empty(first.Issues);
        Assert.Equal(11, first.FinalActive);
        Assert.Equal(11, first.Published);
        var quizId = Assert.Single(CharacterKnowledgeCheckCatalog.QuizIds("raymond"));
        var publication = await db.KnowledgeCheckPublications.AsNoTracking().SingleAsync(row => row.Version.QuizId == quizId);
        var teacher = Controller(db, owner, "Underviser");
        var original = Value<KnowledgeCheckDraftView>(await teacher.GetDraft(quizId, default));
        var changed = Value<KnowledgeCheckDraftView>(await teacher.Update(quizId,
            new("Raymonds lokale titel", original.Description, original.Topic, original.EstimatedMinutes, original.Questions, original.Revision), default));
        await db.KnowledgeCheckPublications.Where(row => row.Id == publication.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.IsActive, false));
        db.ChangeTracker.Clear();

        var repeated = await KnowledgeCheckSeriesPublisher.RunCharacterSeriesAsync(db, owner, dryRun: false);
        Assert.Empty(repeated.Issues);
        Assert.Equal(0, repeated.Published);
        Assert.Equal(1, repeated.PreviouslyClosed);
        Assert.Equal(10, repeated.FinalActive);
        Assert.Equal(11, await db.KnowledgeCheckPublications.CountAsync());
        Assert.Equal(changed.Title, Value<KnowledgeCheckDraftView>(await teacher.GetDraft(quizId, default)).Title);

        var other = await KnowledgeCheckSeriesPublisher.RunCharacterSeriesAsync(db, otherTeacher, dryRun: false);
        Assert.All(other.Issues, issue => Assert.Equal("draft_owned_by_another_teacher", issue.Code));
        Assert.Equal(11, other.Issues.Length);
        Assert.Equal(11, await db.KnowledgeCheckQuizzes.CountAsync());
    }

    [Fact]
    public async Task CharacterCardsAndCatalogUseNewestActivePracticeAndKeepOlderAttempts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = Guid.NewGuid();
        db.NotebookProfiles.Add(new NotebookProfile
        {
            Sub = owner, IsTeacher = true, DisplayName = "Underviser", UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        Assert.Empty((await KnowledgeCheckSeriesPublisher.RunCharacterSeriesAsync(db, owner, dryRun: false)).Issues);

        var guest = Controller(db);
        var profile = Assert.Single(await guest.CharacterExercises("raymond", default));
        Assert.Empty(await guest.CharacterExercises("unknown", default));
        Assert.NotNull(typeof(KnowledgeChecksController).GetMethod(nameof(KnowledgeChecksController.CharacterExercises))!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false).SingleOrDefault());
        var json = JsonSerializer.SerializeToElement(profile, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(new[] { "difficulty", "estimatedMinutes", "id", "questionCount", "title", "topic" },
            json.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.IsType<UnauthorizedResult>(await guest.GetPublication(profile.Id, null, null, default));

        var student = Controller(db, Guid.NewGuid(), "Elev");
        var attempt = Value<KnowledgeCheckAttemptView>(await student.StartAttempt(profile.Id, new(), null, null, default));
        var teacher = Controller(db, owner, "Underviser");
        var quizId = Assert.Single(CharacterKnowledgeCheckCatalog.QuizIds("raymond"));
        var draft = Value<KnowledgeCheckDraftView>(await teacher.GetDraft(quizId, default));
        await teacher.Update(quizId, new("Raymond undersøger caféen", draft.Description, draft.Topic,
            draft.EstimatedMinutes, draft.Questions, draft.Revision), default);
        var next = Value<KnowledgeCheckPublicationView>(await teacher.Publish(quizId, new(null, null, "after-submit"), default));
        Assert.NotEqual(profile.Id, next.Id);
        Assert.Equal("raymond", next.CharacterHost?.Slug);
        Assert.True(next.IsCurrentPractice);
        Assert.Equal(next.Id, Assert.Single(await guest.CharacterExercises("raymond", default)).Id);

        var index = Value<KnowledgeCheckIndex>(await student.List(default));
        var hosted = index.Publications.Where(row => row.CharacterHost?.Slug == "raymond").ToArray();
        Assert.Equal(2, hosted.Length);
        Assert.Equal(next.Id, Assert.Single(hosted, row => row.IsCurrentPractice).Id);
        Assert.Contains(index.History, entry => entry.AttemptId == attempt.Id && entry.CanOpen);
        var previous = Value<KnowledgeCheckPublicationView>(await student.GetPublication(profile.Id, null, null, default));
        Assert.False(previous.IsCurrentPractice);
        Assert.Equal("raymond", previous.CharacterHost?.Slug);
        Assert.Equal(attempt.Id, Value<KnowledgeCheckAttemptView>(await student.GetAttempt(profile.Id, attempt.Id, null, null, default)).Id);

        var hold = new Hold
        {
            Id = Guid.NewGuid(), Name = "Prøvehold",
            Forloeb = new Forloeb { Id = Guid.NewGuid(), Name = "Undervisning", CreatedBySub = owner }
        };
        db.Hold.Add(hold);
        await db.SaveChangesAsync();
        var assigned = Value<KnowledgeCheckPublicationView>(await teacher.Publish(quizId, new(hold.Id, 1, "manual"), default));
        Assert.Null(assigned.CharacterHost);
        Assert.IsType<NotFoundResult>(await student.GetPublication(assigned.Id, null, null, default));
        Assert.Equal(next.Id, Assert.Single(await guest.CharacterExercises("raymond", default)).Id);

        await teacher.ClosePublication(next.Id, default);
        Assert.Equal(profile.Id, Assert.Single(await guest.CharacterExercises("raymond", default)).Id);
        await teacher.ClosePublication(profile.Id, default);
        Assert.Empty(await guest.CharacterExercises("raymond", default));
    }

    private static KnowledgeChecksController Controller(AppDbContext db, Guid? sub = null, string role = "Elev") =>
        new(db, new CourseAccess(db))
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = sub is Guid identity
                        ? new(new ClaimsIdentity([new("sub", identity.ToString()), new("role", role)], "test"))
                        : new(new ClaimsIdentity())
                }
            }
        };

    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
}
