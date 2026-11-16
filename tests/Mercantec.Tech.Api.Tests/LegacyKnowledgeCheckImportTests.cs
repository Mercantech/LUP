using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.KnowledgeChecks;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mercantec.Tech.Api.Tests;

public sealed class LegacyKnowledgeCheckImportTests
{
    [Fact]
    public async Task ImportRequiresTeacherAndPreservesEditedDraftsOnRetry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var teacher = As(db, Guid.NewGuid(), teacher: true);
        var otherTeacher = As(db, Guid.NewGuid(), teacher: true);
        var student = As(db, Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(await As(db, null).ImportLegacy(default));
        Assert.IsType<ForbidResult>(await student.ImportLegacy(default));
        var imported = Read<KnowledgeCheckLegacyImport>(await teacher.ImportLegacy(default));
        Assert.True(imported.Added > 1);
        Assert.Equal(0, imported.Existing);
        var catalog = Read<KnowledgeCheckIndex>(await teacher.List(default));
        Assert.Equal(imported.Added, catalog.Owned.Length);
        Assert.Empty(catalog.Publications);
        Assert.Empty(Read<KnowledgeCheckIndex>(await student.List(default)).Publications);

        var item = catalog.Owned[0];
        var original = Read<KnowledgeCheckDraftView>(await teacher.GetDraft(item.Id, default));
        Assert.NotEmpty(original.Questions);
        var edited = new KnowledgeCheckDraft("Min bearbejdede kladde", original.Description, original.Topic,
            original.EstimatedMinutes, original.Questions, original.Revision);
        var saved = Read<KnowledgeCheckDraftView>(await teacher.Update(item.Id, edited, default));
        Assert.Equal(new KnowledgeCheckLegacyImport(0, imported.Added),
            Read<KnowledgeCheckLegacyImport>(await teacher.ImportLegacy(default)));
        var preserved = Read<KnowledgeCheckDraftView>(await teacher.GetDraft(item.Id, default));
        Assert.Equal(edited.Title, preserved.Title);
        Assert.Equal(saved.Revision, preserved.Revision);

        Assert.IsType<NotFoundResult>(await otherTeacher.GetDraft(item.Id, default));
        Assert.Equal(imported, Read<KnowledgeCheckLegacyImport>(await otherTeacher.ImportLegacy(default)));
        Assert.IsType<NotFoundResult>(await otherTeacher.GetDraft(item.Id, default));
        Assert.Equal(imported.Added * 2, await db.KnowledgeCheckQuizzes.CountAsync());
        Assert.Empty(await db.KnowledgeCheckPublications.ToListAsync());
    }

    [Fact]
    public async Task PublicCatalogPreservesLegacyCategoriesAndDifficultyByDraftIdentity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var owner = Guid.NewGuid();
        var teacher = As(db, owner, teacher: true);
        var student = As(db, Guid.NewGuid());

        await teacher.ImportLegacy(default);
        var legacyId = LegacyKnowledgeCheckCatalog.ForOwner(owner).Single(entry => entry.Value.Key == "api-calls").Key;
        var original = Read<KnowledgeCheckDraftView>(await teacher.GetDraft(legacyId, default));
        var editedTitle = "HTTP i praksis";
        var edit = new KnowledgeCheckDraft(editedTitle, original.Description, original.Topic,
            original.EstimatedMinutes, original.Questions, original.Revision);
        var edited = Read<KnowledgeCheckDraftView>(await teacher.Update(legacyId, edit, default));
        var batch = await LegacyKnowledgeCheckPublisher.RunAsync(db, owner, dryRun: false);
        Assert.Empty(batch.Issues);
        Assert.Equal(26, batch.Published);

        var custom = Read<KnowledgeCheckDraftView>(await teacher.Create(edit with { Topic = "HTTP", Revision = null }, default));
        await teacher.Publish(custom.Id, new(null, null, "after-submit"), default);
        var catalog = Read<KnowledgeCheckIndex>(await student.List(default));
        var legacy = catalog.Publications.Where(publication => publication.LegacyDiscovery is not null).ToArray();
        Assert.Equal(26, legacy.Length);

        var renamed = Assert.Single(legacy, publication => publication.QuizId == legacyId);
        Assert.Equal(editedTitle, renamed.Title);
        Assert.Equal(original.Topic, renamed.Topic);
        var metadata = Assert.IsType<KnowledgeCheckLegacyDiscovery>(renamed.LegacyDiscovery);
        Assert.Equal(new[] { "Backend", "Frontend" }, metadata.Categories);
        Assert.Equal("Mellem", metadata.Difficulty);

        var categories = legacy.SelectMany(publication => publication.LegacyDiscovery!.Categories).ToLookup(category => category);
        Assert.Equal(8, categories["GF2"].Count());
        Assert.Equal(17, categories["Backend"].Count());
        Assert.Equal(3, categories["Frontend"].Count());
        Assert.Single(categories["Flutter"]);
        var difficulty = legacy.ToLookup(publication => publication.LegacyDiscovery!.Difficulty);
        Assert.Equal(9, difficulty["Begynder"].Count());
        Assert.Equal(15, difficulty["Mellem"].Count());
        Assert.Equal(2, difficulty["Avanceret"].Count());

        var sameTitle = Assert.Single(catalog.Publications, publication => publication.QuizId == custom.Id);
        Assert.Equal(editedTitle, sameTitle.Title);
        Assert.Equal("HTTP", sameTitle.Topic);
        Assert.Null(sameTitle.LegacyDiscovery);

        await teacher.ClosePublication(renamed.Id, default);
        Assert.DoesNotContain(Read<KnowledgeCheckIndex>(await student.List(default)).Publications, publication => publication.Id == renamed.Id);
        var closed = Assert.Single(Read<KnowledgeCheckIndex>(await teacher.List(default)).Publications, publication => publication.Id == renamed.Id);
        Assert.False(closed.IsActive);
        Assert.Null(closed.LegacyDiscovery);

        await teacher.Update(legacyId, edit with { Topic = "Webteknologi", Revision = edited.Revision }, default);
        var moved = Read<KnowledgeCheckPublicationView>(await teacher.Publish(legacyId, new(null, null, "after-submit"), default));
        var movedInCatalog = Assert.Single(Read<KnowledgeCheckIndex>(await student.List(default)).Publications, publication => publication.Id == moved.Id);
        Assert.Equal("Webteknologi", movedInCatalog.Topic);
        var movedDiscovery = Assert.IsType<KnowledgeCheckLegacyDiscovery>(movedInCatalog.LegacyDiscovery);
        Assert.Equal(new[] { "Webteknologi" }, movedDiscovery.Categories);
        Assert.Equal("Mellem", movedDiscovery.Difficulty);
    }

    [Fact]
    public async Task ConcurrentImportsAcrossDatabaseContextsAddOneSet()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lup-legacy-knowledge-check-race-{Guid.NewGuid():N}.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 15 }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection)
            .AddInterceptors(new ConcurrentSaveBarrier()).Options;
        try
        {
            await using var setup = new AppDbContext(options);
            await setup.Database.EnsureCreatedAsync();
            await setup.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            var owner = Guid.NewGuid();
            await using var first = new AppDbContext(options);
            await using var second = new AppDbContext(options);
            var firstRequest = As(first, owner, teacher: true).ImportLegacy(default);
            var secondRequest = As(second, owner, teacher: true).ImportLegacy(default);
            var responses = await Task.WhenAll(firstRequest, secondRequest).WaitAsync(TimeSpan.FromSeconds(20));
            var results = responses.Select(Read<KnowledgeCheckLegacyImport>).ToArray();
            var count = await setup.KnowledgeCheckQuizzes.CountAsync();
            Assert.True(count > 1);
            Assert.Contains(new KnowledgeCheckLegacyImport(count, 0), results);
            Assert.Contains(new KnowledgeCheckLegacyImport(0, count), results);
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private static KnowledgeChecksController As(AppDbContext db, Guid? sub, bool teacher = false)
    {
        var claims = new List<Claim>();
        if (sub is Guid id) claims.Add(new("sub", id.ToString()));
        if (teacher) claims.Add(new("role", "Underviser"));
        return new(db, new CourseAccess(db))
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, sub is null ? null : "test")) }
            }
        };
    }

    private static T Read<T>(IActionResult result) =>
        Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private sealed class ConcurrentSaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothRequestsAreReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _pendingRequests;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // Force both requests to observe the same empty catalog before either writes.
            if (Interlocked.Increment(ref _pendingRequests) == 2) _bothRequestsAreReady.SetResult();
            await _bothRequestsAreReady.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}
