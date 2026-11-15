using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class LegacyKnowledgeCheckPublisherTests
{
    [Fact]
    public async Task DryRunAndPublishReuseDraftEditsPublicationsAndAttempts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = Guid.NewGuid();
        await AddTeacherAsync(db, owner);

        var initial = await LegacyKnowledgeCheckPublisher.RunAsync(db, owner, dryRun: true);
        Assert.Empty(initial.Issues);
        Assert.Equal(initial.Total, initial.DraftsToAdd);
        Assert.Equal(initial.Total, initial.PublicationsToAdd);
        Assert.Equal(0, initial.FinalActive);
        Assert.Empty(db.KnowledgeCheckQuizzes);

        var controller = AsTeacher(db, owner);
        var imported = Value<KnowledgeCheckLegacyImport>(await controller.ImportLegacy(default));
        Assert.True(imported.Added > 1);
        var ids = LegacyKnowledgeCheckCatalog.ForOwner(owner).Keys.Take(2).ToArray();
        var first = Value<KnowledgeCheckPublicationView>(await controller.Publish(ids[0], new(null, null, "after-submit"), default));
        var attempted = new KnowledgeCheckAttempt
        {
            Id = Guid.NewGuid(), PublicationId = first.Id, UserSub = Guid.NewGuid(), Status = "graded",
            StartedAtUtc = DateTime.UtcNow, SubmittedAtUtc = DateTime.UtcNow, Revision = 1
        };
        db.KnowledgeCheckAttempts.Add(attempted);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var original = Value<KnowledgeCheckDraftView>(await controller.GetDraft(ids[1], default));
        var edited = Value<KnowledgeCheckDraftView>(await controller.Update(ids[1],
            new("Bevar den lokale titel", original.Description, original.Topic, original.EstimatedMinutes, original.Questions, original.Revision),
            default));

        var dryRun = await LegacyKnowledgeCheckPublisher.RunAsync(db, owner, dryRun: true);
        Assert.Empty(dryRun.Issues);
        Assert.Equal(initial.Total - 1, dryRun.PublicationsToAdd);
        Assert.Equal(1, dryRun.FinalActive);
        Assert.Equal(1, await db.KnowledgeCheckPublications.CountAsync());

        var published = await LegacyKnowledgeCheckPublisher.RunAsync(db, owner, dryRun: false);
        Assert.Empty(published.Issues);
        Assert.Equal(0, published.AddedDrafts);
        Assert.Equal(initial.Total - 1, published.Published);
        Assert.Equal(initial.Total, published.FinalActive);
        Assert.Equal(1, published.AlreadyActive);
        var historical = await db.KnowledgeCheckAttempts.AsNoTracking().SingleAsync();
        Assert.Equal(first.Id, historical.PublicationId);
        var local = await db.KnowledgeCheckPublications.AsNoTracking().Include(row => row.Version)
            .SingleAsync(row => row.Version.QuizId == edited.Id);
        Assert.Equal(edited.Title, local.Version.Title);
        Assert.Equal(edited.Revision, local.Version.DraftRevision);

        db.ChangeTracker.Clear();
        var repeated = await LegacyKnowledgeCheckPublisher.RunAsync(db, owner, dryRun: false);
        Assert.Empty(repeated.Issues);
        Assert.Equal(0, repeated.Published);
        Assert.Equal(initial.Total, repeated.AlreadyActive);
        var publicRows = await db.KnowledgeCheckPublications.AsNoTracking().ToListAsync();
        Assert.Equal(initial.Total, publicRows.Count);
        Assert.All(publicRows, row =>
        {
            Assert.Null(row.HoldId);
            Assert.Null(row.MaxAttempts);
            Assert.Equal("after-submit", row.FeedbackMode);
            Assert.True(row.IsActive);
        });
    }

    [Fact]
    public async Task NonTeachersAndUnreadyLocalEditsDoNotPublishAnything()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = Guid.NewGuid();

        var blocked = await LegacyKnowledgeCheckPublisher.RunAsync(db, owner, dryRun: false);
        Assert.Equal("owner_not_verified_as_teacher", Assert.Single(blocked.Issues).Code);
        Assert.Empty(db.KnowledgeCheckQuizzes);
        await AddTeacherAsync(db, owner);

        var imported = LegacyKnowledgeCheckCatalog.ForOwner(owner).First();
        db.KnowledgeCheckQuizzes.Add(new()
        {
            Id = imported.Key, OwnerSub = owner, Title = "Bearbejdning", UpdatedAtUtc = DateTime.UtcNow, Revision = 1
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        foreach (var dryRun in new[] { true, false })
        {
            blocked = await LegacyKnowledgeCheckPublisher.RunAsync(db, owner, dryRun);
            var issue = Assert.Single(blocked.Issues);
            Assert.Equal(imported.Value.Key, issue.Source);
            Assert.Equal("draft_not_ready_for_free_exercise", issue.Code);
            Assert.Equal(0, blocked.AddedDrafts);
            Assert.Equal(0, blocked.Published);
            Assert.Equal(1, await db.KnowledgeCheckQuizzes.CountAsync());
            Assert.Empty(db.KnowledgeCheckPublications);
            Assert.Empty(db.KnowledgeCheckVersions);
        }

        await db.NotebookProfiles.Where(profile => profile.Sub == owner)
            .ExecuteUpdateAsync(setters => setters.SetProperty(profile => profile.IsTeacher, false));
        blocked = await LegacyKnowledgeCheckPublisher.RunAsync(db, owner, dryRun: false);
        Assert.Equal("owner_not_verified_as_teacher", Assert.Single(blocked.Issues).Code);
        Assert.Empty(db.KnowledgeCheckPublications);
    }

    [Fact]
    public async Task ConcurrentPublishesAcrossContextsProduceOneSet()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lup-legacy-publish-{Guid.NewGuid():N}.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 15 }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        try
        {
            await using var setup = new AppDbContext(options);
            await setup.Database.EnsureCreatedAsync();
            await setup.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            var owner = Guid.NewGuid();
            await AddTeacherAsync(setup, owner);
            await using var first = new AppDbContext(options);
            await using var second = new AppDbContext(options);
            var results = await Task.WhenAll(
                Task.Run(() => LegacyKnowledgeCheckPublisher.RunAsync(first, owner, dryRun: false)),
                Task.Run(() => LegacyKnowledgeCheckPublisher.RunAsync(second, owner, dryRun: false)))
                .WaitAsync(TimeSpan.FromSeconds(20));

            var total = LegacyKnowledgeCheckCatalog.ForOwner(owner).Count;
            Assert.All(results, result => Assert.Empty(result.Issues));
            Assert.Equal(total, results.Sum(row => row.Published));
            Assert.Equal(total, results.Sum(row => row.AddedDrafts));
            Assert.Equal(total, await setup.KnowledgeCheckPublications.CountAsync());
            Assert.Equal(total, await setup.KnowledgeCheckVersions.CountAsync());
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private static async Task AddTeacherAsync(AppDbContext db, Guid owner)
    {
        db.NotebookProfiles.Add(new NotebookProfile
        {
            Sub = owner, DisplayName = "QA Underviser", IsTeacher = true, UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static KnowledgeChecksController AsTeacher(AppDbContext db, Guid owner) => new(db, new CourseAccess(db))
    {
        ControllerContext = new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new(new ClaimsIdentity([new("sub", owner.ToString()), new("role", "Underviser")], "test"))
            }
        }
    };

    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);
}
