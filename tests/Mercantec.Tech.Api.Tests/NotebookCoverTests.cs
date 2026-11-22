using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotebookCoverTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly RecordingGenerator generator = new();
    private readonly ProjectionHandler projection = new();
    private AppDbContext db = null!;
    private readonly Guid teacher = Guid.NewGuid(), owner = Guid.NewGuid(), pageReader = Guid.NewGuid(), bookReader = Guid.NewGuid();
    private readonly Guid collectionId = Guid.NewGuid(), bookId = Guid.NewGuid(), noteId = Guid.NewGuid();
    private static readonly string Fingerprint = new('a', 64);
    private const string TeachingPage = "notion-page";

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.NotionCollections.Add(new() { Id = collectionId, Title = "Netværk", CreatedBySub = teacher });
        db.NotionPages.Add(new()
        {
            Id = collectionId, CollectionId = collectionId, Title = "Netværk", CreatedBySub = teacher,
            YjsState = [1, 2, 3], DraftFingerprint = Fingerprint, DraftRevision = 1
        });
        db.Notebooks.Add(new() { Id = bookId, OwnerSub = owner });
        db.NotebookDocuments.AddRange(
            new() { Id = noteId, NotebookId = bookId, Title = "Sensorer", SearchText = "Temperaturmåling med en ESP32." },
            new() { Id = Guid.NewGuid(), NotebookId = bookId, Kind = "journal", Title = "Logbog", SearchText = "Privat journaltekst" });
        db.NotebookShares.AddRange(
            new() { Id = Guid.NewGuid(), NotebookId = bookId, RecipientSub = bookReader },
            new() { Id = Guid.NewGuid(), NotebookId = bookId, PageId = noteId, RecipientSub = pageReader });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Teaching_page_covers_follow_the_published_snapshot_and_release_old_assets()
    {
        var editor = Covers(teacher, isTeacher: true);
        var student = Covers(owner);
        var notebook = new NotionController(db, new(db)) { ControllerContext = editor.ControllerContext };

        Assert.IsType<NotFoundResult>(await student.Generate(TeachingPage, collectionId, default));
        Assert.Empty(generator.Requests);
        var first = Value<NotebookCoverView>(await editor.Generate(TeachingPage, collectionId, default)).CoverId;
        Assert.NotNull(first);
        Assert.Equal("Håndtryk og netværksprotokoller.", Assert.Single(Assert.Single(generator.Requests).Excerpts));
        Assert.IsType<NotFoundResult>(await student.Get(TeachingPage, collectionId, default, version: first));
        Assert.IsType<NotFoundResult>(await editor.Get(TeachingPage, collectionId, default, publishedOnly: true));

        Assert.False(Value<NotionPageSummary>(await notebook.Publish(collectionId, new(1, Fingerprint, []), default)).HasUnpublishedChanges);
        Assert.IsType<FileContentResult>(await student.Get(TeachingPage, collectionId, default, version: first));

        var second = Value<NotebookCoverView>(await editor.Generate(TeachingPage, collectionId, default)).CoverId;
        var draft = Value<NotionPageContext>(await notebook.Context(collectionId, default));
        var published = Value<NotionPageContext>(await notebook.PublishedContext(collectionId, default));
        Assert.Equal(second, draft.CoverId);
        Assert.Equal(first, draft.PublishedCoverId);
        Assert.Equal(first, published.CoverId);
        var rows = Value<List<NotionPageSummary>>(await notebook.List(null, default, collectionId: collectionId));
        Assert.True(Assert.Single(rows).HasUnpublishedChanges);
        Assert.IsType<NotFoundResult>(await student.Get(TeachingPage, collectionId, default, version: second));
        Assert.IsType<FileContentResult>(await student.Get(TeachingPage, collectionId, default, version: first));
        Assert.Equal(2, await db.NotebookCoverAssets.CountAsync());

        Assert.Null(Value<NotebookCoverView>(await editor.Remove(TeachingPage, collectionId, default)).CoverId);
        Assert.Equal(first, (await db.NotebookCoverAssets.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(first, Value<NotionPageContext>(await notebook.PublishedContext(collectionId, default)).CoverId);
        Assert.False(Value<NotionPageSummary>(await notebook.Publish(collectionId, new(1, Fingerprint, []), default)).HasUnpublishedChanges);
        Assert.IsType<NotFoundResult>(await student.Get(TeachingPage, collectionId, default));
        Assert.Empty(await db.NotebookCoverAssets.ToListAsync());
    }

    [Fact]
    public async Task Collection_cover_has_the_same_visibility_as_its_collection()
    {
        var editor = Covers(teacher, isTeacher: true);
        var student = Covers(owner);
        var collections = new NotionCollectionsController(db, new(db)) { ControllerContext = student.ControllerContext };
        var teaching = new NotionController(db, new(db)) { ControllerContext = editor.ControllerContext };

        var cover = Value<NotebookCoverView>(await editor.Generate("collection", collectionId, default)).CoverId;
        Assert.IsType<NotFoundResult>(await collections.Get(collectionId, default));
        Assert.IsType<NotFoundResult>(await student.Get("collection", collectionId, default));

        Assert.IsType<OkObjectResult>(await teaching.Publish(collectionId, new(1, Fingerprint, []), default));
        Assert.Equal(cover, Value<NotionCollectionView>(await collections.Get(collectionId, default)).CoverId);
        Assert.IsType<FileContentResult>(await student.Get("collection", collectionId, default, version: cover));
        await teaching.Unpublish(collectionId, default);
        Assert.IsType<NotFoundResult>(await student.Get("collection", collectionId, default, version: cover));
        Assert.IsType<FileContentResult>(await editor.Get("collection", collectionId, default, version: cover));
    }

    [Fact]
    public async Task Whole_notebook_cover_is_not_exposed_with_a_single_page_share()
    {
        var mine = Covers(owner);
        var individual = Covers(pageReader);
        var whole = Covers(bookReader);
        var bookCover = Value<NotebookCoverView>(await mine.Generate("notebook", bookId, default)).CoverId;
        Assert.Equal("Sensorer Temperaturmåling med en ESP32.", Assert.Single(Assert.Single(generator.Requests).Excerpts));

        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.RoyalBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var content = new MemoryStream(encoded.ToArray());
        var upload = new FormFile(content, 0, content.Length, "file", "cover.png");
        var pageCover = Value<NotebookCoverView>(await mine.Upload("notebook-page", noteId, upload, default)).CoverId;

        var downloaded = Assert.IsType<FileContentResult>(await individual.Get("notebook-page", noteId, default, version: pageCover));
        Assert.Equal("image/webp", downloaded.ContentType);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(downloaded.FileContents, 0, 4));
        Assert.IsType<NotFoundResult>(await individual.Get("notebook", bookId, default, version: bookCover));
        Assert.IsType<NotFoundResult>(await whole.Remove("notebook", bookId, default));
        Assert.IsType<FileContentResult>(await whole.Get("notebook", bookId, default, version: bookCover));

        var shared = new NotebooksController(db, new(db)) { ControllerContext = individual.ControllerContext };
        var onePage = Assert.Single(Value<NotebookPageSummary[]>(await shared.Shared(default)));
        Assert.Equal(pageCover, onePage.CoverId);
        Assert.Null(onePage.NotebookCoverId);
        Assert.Null(Value<NotebookDocumentContext>(await shared.Context(noteId, default)).NotebookCoverId);

        shared.ControllerContext = whole.ControllerContext;
        Assert.All(Value<NotebookPageSummary[]>(await shared.Shared(default)), card => Assert.Equal(bookCover, card.NotebookCoverId));
        Assert.Equal(bookCover, Value<NotebookDocumentContext>(await shared.Context(noteId, default)).NotebookCoverId);

        await db.NotebookShares.Where(share => share.RecipientSub == pageReader).ExecuteDeleteAsync();
        Assert.IsType<NotFoundResult>(await individual.Get("notebook-page", noteId, default, version: pageCover));
    }

    [Fact]
    public async Task In_flight_generation_does_not_overwrite_a_later_cover_change()
    {
        var mine = Covers(owner);
        var original = Value<NotebookCoverView>(await mine.Generate("notebook-page", noteId, default)).CoverId;
        Assert.NotNull(original);
        generator.BeforeResult = async () => Assert.Null(
            Value<NotebookCoverView>(await mine.Remove("notebook-page", noteId, default)).CoverId);

        Assert.IsType<ConflictObjectResult>(await mine.Generate("notebook-page", noteId, default));
        Assert.Empty(await db.NotebookCoverAssets.ToListAsync());
        Assert.IsType<NotFoundResult>(await mine.Get("notebook-page", noteId, default));

        mine.Request.Headers[NotebookCoverRules.SubjectHeader] = teacher.ToString();
        Assert.IsType<ConflictObjectResult>(await mine.Generate("notebook-page", noteId, default));
        Assert.Equal(2, generator.Requests.Count);
    }

    private NotebookCoversController Covers(Guid userSub, bool isTeacher = false)
    {
        var claims = new List<Claim> { new("sub", userSub.ToString()) };
        if (isTeacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        var context = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) };
        context.Request.Headers[NotebookCoverRules.SubjectHeader] = userSub.ToString();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notion:ServiceSecret"] = new string('s', 40),
            ["Notebook:CollaborationBaseUrl"] = "http://documents"
        }).Build();
        return new(db, new(db), new(db), new(new HttpFactory(projection), configuration, new(configuration)), generator)
        {
            ControllerContext = new() { HttpContext = context }
        };
    }

    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private sealed class RecordingGenerator : INotebookCoverGenerator
    {
        public bool IsConfigured => true;
        public List<(string Title, IReadOnlyList<string> Excerpts)> Requests { get; } = [];
        public Func<Task>? BeforeResult { get; set; }

        public async Task<byte[]> GenerateAsync(string title, IReadOnlyList<string> excerpts, CancellationToken ct)
        {
            Requests.Add((title, excerpts));
            if (BeforeResult is not null) await BeforeResult();
            return [1, 2, 3];
        }
    }

    private sealed class ProjectionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new NotionAiProjection("Håndtryk og netværksprotokoller."))
            });
    }

    private sealed class HttpFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
