using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Ai.EditorRuns;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Storage;
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
    private readonly GeneratedImageStorage storage = new();
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
        var firstResult = Value<NotebookCoverView>(await editor.Generate(TeachingPage, collectionId, default));
        var first = firstResult.CoverId;
        Assert.NotNull(first);
        Assert.Null(firstResult.Image);
        Assert.DoesNotContain("\"Image\"", JsonSerializer.Serialize(firstResult), StringComparison.Ordinal);
        Assert.Equal("Håndtryk og netværksprotokoller.", Assert.Single(Assert.Single(generator.Requests).Excerpts));
        Assert.IsType<NotFoundResult>(await student.Get(TeachingPage, collectionId, default, version: first));
        Assert.IsType<NotFoundResult>(await editor.Get(TeachingPage, collectionId, default, publishedOnly: true));

        Assert.False(Value<NotionPageSummary>(await notebook.Publish(collectionId, new(1, Fingerprint, []), default)).HasUnpublishedChanges);
        Assert.IsType<FileContentResult>(await student.Get(TeachingPage, collectionId, default, version: first));

        var secondResult = Value<NotebookCoverView>(await editor.Generate(TeachingPage, collectionId, default, includeImage: true));
        var second = secondResult.CoverId;
        Assert.NotNull(secondResult.Image);
        Assert.Equal(secondResult.Image, Assert.IsType<FileContentResult>(
            await editor.Get(TeachingPage, collectionId, default, version: second)).FileContents);
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

        var removed = Value<NotebookCoverView>(await editor.Remove(TeachingPage, collectionId, default));
        Assert.Null(removed.CoverId);
        Assert.Null(removed.Image);
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
        Assert.Null(generator.Requests[0].Course);

        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.RoyalBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var content = new MemoryStream(encoded.ToArray());
        var upload = new FormFile(content, 0, content.Length, "file", "cover.png");
        var saved = Value<NotebookCoverView>(await mine.Upload("notebook-page", noteId, upload, default, includeImage: true));
        var pageCover = saved.CoverId;

        var downloaded = Assert.IsType<FileContentResult>(await individual.Get("notebook-page", noteId, default, version: pageCover));
        Assert.NotNull(saved.Image);
        Assert.Equal(saved.Image, downloaded.FileContents);
        var json = JsonSerializer.Serialize(saved, JsonSerializerOptions.Web);
        Assert.Equal(saved.Image, JsonSerializer.Deserialize<NotebookCoverView>(json, JsonSerializerOptions.Web)?.Image);
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

        Assert.IsType<ConflictObjectResult>(await mine.Generate("notebook-page", noteId, default, includeImage: true));
        Assert.Empty(await db.NotebookCoverAssets.ToListAsync());
        Assert.IsType<NotFoundResult>(await mine.Get("notebook-page", noteId, default));

        mine.Request.Headers[NotebookCoverRules.SubjectHeader] = teacher.ToString();
        Assert.IsType<ConflictObjectResult>(await mine.Generate("notebook-page", noteId, default));
        Assert.Equal(2, generator.Requests.Count);
    }

    [Fact]
    public async Task A_generated_cover_files_each_version_under_the_depicted_character()
    {
        var root = await db.NotionPages.SingleAsync(page => page.Id == collectionId);
        root.Title = "Træningsdata til lineær regression";
        await db.SaveChangesAsync();
        var editor = Covers(teacher, isTeacher: true, includeArtwork: true);

        var first = Value<NotebookCoverView>(await editor.Generate(TeachingPage, collectionId, default, includeImage: true));
        var next = Value<NotebookCoverView>(await editor.Generate(TeachingPage, collectionId, default, characterSlug: "Raymond"));

        Assert.Equal("chameleon", first.CharacterSlug);
        Assert.Equal("raymond", next.CharacterSlug);
        Assert.Equal("raymond", generator.Requests[1].Artwork?.CharacterSlug);
        Assert.Equal(next.CoverId, await db.NotionPages.Where(page => page.Id == collectionId).Select(page => page.CoverId).SingleAsync());
        var archived = await db.CharacterGeneratedArtwork.AsNoTracking().ToArrayAsync();
        Assert.Equal(2, archived.Length);
        var previous = Assert.Single(archived, item => item.OriginalAssetId == first.CoverId);
        Assert.Equal("chameleon", previous.CharacterSlug);
        Assert.Equal(first.Image, previous.Image);
        Assert.All(archived, item => Assert.Equal(collectionId, item.NotionPageId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Inline_image_is_a_page_attachment_and_reusable_character_art(bool teaching)
    {
        var kind = teaching ? TeachingPage : "notebook-page";
        var pageId = teaching ? collectionId : noteId;
        var editor = Covers(teaching ? teacher : owner, isTeacher: teaching, includeArtwork: true);
        var subjectMismatch = Covers(pageReader);
        subjectMismatch.Request.Headers[NotebookCoverRules.SubjectHeader] = owner.ToString();
        Assert.IsType<ConflictObjectResult>(await subjectMismatch.GenerateImage(kind, pageId, new(), default));
        Assert.Empty(generator.Requests);

        var saved = Value<NotionAssetView>(await editor.GenerateImage(kind, pageId,
            new("Vis Raymond sortere relationer i en database.", "raymond"), default));
        Assert.Equal("image", saved.Kind);
        Assert.Equal("image/webp", saved.ContentType);
        Assert.Contains("raymond", saved.OriginalName, StringComparison.Ordinal);
        var request = Assert.Single(generator.Requests);
        Assert.Null(request.Course);
        Assert.Equal("raymond", request.Artwork?.CharacterSlug);
        Assert.Equal("Vis Raymond sortere relationer i en database.", request.IllustrationDescription);

        var key = teaching
            ? await db.NotionAssets.Where(asset => asset.Id == saved.Id && asset.PageId == pageId).Select(asset => asset.Key).SingleAsync()
            : await db.NotebookAssets.Where(asset => asset.Id == saved.Id && asset.DocumentId == pageId).Select(asset => asset.Key).SingleAsync();
        var art = await db.CharacterGeneratedArtwork.AsNoTracking().SingleAsync();
        Assert.Equal(pageId, teaching ? art.NotionPageId : art.NotebookPageId);
        Assert.Equal(saved.Id, art.OriginalAssetId);
        Assert.Equal(art.Image, storage.Objects[key]);
    }

    [Fact]
    public async Task Inline_image_does_not_commit_after_the_source_page_is_deleted_during_generation()
    {
        var editor = Covers(teacher, isTeacher: true, includeArtwork: true);
        generator.BeforeResult = () => db.NotionPages.Where(page => page.Id == collectionId)
            .ExecuteUpdateAsync(update => update.SetProperty(page => page.DeletedAtUtc, DateTimeOffset.UtcNow));

        Assert.IsType<ConflictObjectResult>(await editor.GenerateImage(TeachingPage, collectionId, new(null, "raymond"), default));
        Assert.False(await db.NotionAssets.AnyAsync());
        Assert.False(await db.CharacterGeneratedArtwork.AnyAsync());
        Assert.Empty(storage.Objects);
    }

    [Fact]
    public async Task Unavailable_projection_preserves_saved_titles_and_other_projected_excerpts()
    {
        db.NotionPages.Add(new()
        {
            Id = Guid.NewGuid(), CollectionId = collectionId, ParentPageId = collectionId, Title = "Undernet",
            CreatedBySub = teacher, YjsState = [4, 5, 6]
        });
        await db.SaveChangesAsync();

        // Hold the root projection until the sibling starts, then fail only the root.
        var siblingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rootObservedSibling = false;
        projection.Respond = async (request, ct) =>
        {
            var source = await request.Content!.ReadFromJsonAsync<ProjectionRequest>(ct);
            if (source?.StateBase64 == Convert.ToBase64String([1, 2, 3]))
            {
                await siblingStarted.Task.WaitAsync(ct);
                rootObservedSibling = true;
                return new(HttpStatusCode.ServiceUnavailable);
            }

            siblingStarted.TrySetResult();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new NotionAiProjection("Beregn CIDR.")) };
        };

        var editor = Covers(teacher, isTeacher: true);
        Assert.IsType<OkObjectResult>(await editor.Generate("collection", collectionId, default));
        Assert.True(rootObservedSibling);
        var request = Assert.Single(generator.Requests);
        Assert.Equal(new[] { "Netværk", "Undernet Beregn CIDR." }, request.Excerpts);

        Assert.IsType<OkObjectResult>(await editor.Generate(TeachingPage, collectionId, default));
        Assert.Equal("Netværk", generator.Requests[1].Title);
        Assert.Empty(generator.Requests[1].Excerpts);
    }

    [Fact]
    public async Task Slow_projection_has_its_own_deadline_and_uses_the_saved_title()
    {
        using var request = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        projection.Respond = async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("The projection delay should be canceled.");
        };

        var editor = Covers(teacher, isTeacher: true);
        Assert.IsType<OkObjectResult>(await editor.Generate(TeachingPage, collectionId, request.Token));
        Assert.False(request.IsCancellationRequested);
        var generated = Assert.Single(generator.Requests);
        Assert.Equal("Netværk", generated.Title);
        Assert.Empty(generated.Excerpts);
    }

    [Fact]
    public async Task Canceled_cover_request_does_not_fall_back_to_an_image_provider_call()
    {
        using var request = new CancellationTokenSource();
        projection.Respond = (_, ct) =>
        {
            request.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(ct);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Covers(teacher, isTeacher: true).Generate(TeachingPage, collectionId, request.Token));
        Assert.Empty(generator.Requests);
        Assert.Empty(await db.NotebookCoverAssets.ToListAsync());
    }

    [Fact]
    public async Task A_page_in_a_standalone_course_notebook_uses_its_course_art()
    {
        var root = await db.NotionPages.SingleAsync(page => page.Id == collectionId);
        root.Title = "Machine Learning";
        var pageId = Guid.NewGuid();
        db.NotionPages.Add(new()
        {
            Id = pageId, CollectionId = collectionId, ParentPageId = collectionId,
            Title = "Dag 6", CreatedBySub = teacher
        });
        await db.SaveChangesAsync();

        var editor = Covers(teacher, isTeacher: true, includeArtwork: true);
        Assert.IsType<OkObjectResult>(await editor.Generate(TeachingPage, pageId, default));

        var request = Assert.Single(generator.Requests);
        Assert.Equal("Dag 6", request.Title);
        Assert.NotNull(request.Artwork);
        Assert.Contains("Use the machine learning banner", request.Artwork.Direction, StringComparison.Ordinal);
        Assert.Contains(request.Artwork.References, image => image.Name == "chameleon");
        Assert.Null(request.Course);
    }

    [Fact]
    public async Task Course_collection_uses_dates_from_its_root_beyond_the_illustration_excerpt()
    {
        var root = await db.NotionPages.SingleAsync(page => page.Id == collectionId);
        root.Title = "Machine Learning 2027 :chameleon_test_data:";
        db.NotionPages.Add(new()
        {
            Id = Guid.NewGuid(), CollectionId = collectionId, ParentPageId = collectionId,
            Title = "Dag 6", CreatedBySub = teacher, YjsState = [4, 5, 6]
        });
        await db.SaveChangesAsync();
        projection.Respond = async (request, ct) =>
        {
            var source = await request.Content!.ReadFromJsonAsync<ProjectionRequest>(ct);
            var text = source?.StateBase64 == Convert.ToBase64String([1, 2, 3])
                ? new string('x', 550) + "\nForløbsperiode: 15. februar til 5. marts 2027."
                : "Aflevering: 24. februar 2027.";
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new NotebookCoverProjection(text)) };
        };

        var editor = Covers(teacher, isTeacher: true);
        Assert.IsType<OkObjectResult>(await editor.Generate("collection", collectionId, default));
        Assert.IsType<OkObjectResult>(await editor.Generate(TeachingPage, collectionId, default));
        Assert.All(generator.Requests, request =>
        {
            Assert.DoesNotContain(request.Excerpts, excerpt => excerpt.Contains("15. februar", StringComparison.Ordinal));
            var label = Assert.IsType<NotebookCoverCourseLabel>(request.Course);
            Assert.Equal("Machine Learning 2027", label.Title);
            var date = Assert.Single(label.Dates);
            Assert.Contains("15. februar", date, StringComparison.Ordinal);
            Assert.Contains("5. marts 2027", date, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Linked_course_supplies_its_name_and_visible_period()
    {
        var holdId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        db.Hold.Add(new() { Id = holdId, Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher } });
        db.NotionPages.Add(new() { Id = pageId, HoldId = holdId, Title = "Overblik", CreatedBySub = teacher });
        var course = new TeachingCourse
        {
            Id = courseId, HoldId = holdId, Title = "Spiludvikling med LUPP", Published = true,
            StartsOn = new(2027, 11, 15), EndsOn = new(2027, 12, 2),
            Resources = [new() { Kind = "page", ResourceId = pageId }]
        };
        db.TeachingCourses.Add(course);
        await db.SaveChangesAsync();

        var editor = Covers(teacher, isTeacher: true);
        Assert.IsType<OkObjectResult>(await editor.Generate(TeachingPage, pageId, default));
        var label = Assert.IsType<NotebookCoverCourseLabel>(Assert.Single(generator.Requests).Course);
        Assert.Equal(course.Title, label.Title);
        Assert.Equal("15. november – 2. december 2027", Assert.Single(label.Dates));

        course.Published = false;
        await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await editor.Generate(TeachingPage, pageId, default));
        Assert.Empty(Assert.IsType<NotebookCoverCourseLabel>(generator.Requests[1].Course).Dates);
    }

    [Fact]
    public async Task A_hold_course_home_displays_its_name_even_without_dates()
    {
        var holdId = Guid.NewGuid();
        db.Hold.Add(new() { Id = holdId, Name = "H3", Forloeb = new() { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher } });
        var root = await db.NotionPages.SingleAsync(page => page.Id == collectionId);
        root.HoldId = holdId;
        root.Title = "Kreativ programmering på dansk";
        var collection = await db.NotionCollections.SingleAsync(item => item.Id == collectionId);
        collection.HoldId = holdId;
        await db.SaveChangesAsync();

        var editor = Covers(teacher, isTeacher: true);
        Assert.IsType<OkObjectResult>(await editor.Generate(TeachingPage, collectionId, default));

        var label = Assert.IsType<NotebookCoverCourseLabel>(Assert.Single(generator.Requests).Course);
        Assert.Equal(root.Title, label.Title);
        Assert.Empty(label.Dates);
    }

    [Fact]
    public async Task Personal_page_uses_saved_icon_title_and_body_emojis_before_search_text_fallback()
    {
        var icon = Emoji("page_icon", "2b50");
        var title = Emoji("in_title", "2705");
        var inline = Emoji("from_yjs", "2764");
        var fallback = Emoji("search_fallback", "2753");
        db.CustomEmojis.AddRange(icon, title, inline, fallback);
        var document = await db.NotebookDocuments.SingleAsync(doc => doc.Id == noteId);
        document.IconEmojiId = icon.Id;
        document.Title = ":IN_TITLE: Sensorer";
        document.SearchText = "Sensorer :search_fallback:";
        document.YjsState = [7, 8, 9];
        await db.SaveChangesAsync();
        var projections = 0;
        projection.Respond = async (request, ct) =>
        {
            var body = await request.Content!.ReadFromJsonAsync<ProjectionRequest>(ct);
            Assert.Equal("notebook-cover", body?.Mode);
            Assert.Equal(Convert.ToBase64String([7, 8, 9]), body?.StateBase64);
            projections++;
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new NotebookCoverProjection("Sensorer",
                    [new(inline.Id, inline.Name), new(inline.Id, "dublet"), new(icon.Id, icon.Name)]))
            };
        };
        var generatorWithArtwork = Covers(owner, includeArtwork: true);

        Assert.IsType<NotFoundResult>(await Covers(pageReader, includeArtwork: true).Generate("notebook-page", noteId, default));
        Assert.IsType<OkObjectResult>(await generatorWithArtwork.Generate("notebook-page", noteId, default));
        var first = Assert.Single(generator.Requests);
        Assert.Equal(new[] { icon.Name, title.Name, inline.Name }, EmojiReferences(first.Artwork));
        Assert.Equal(1, projections);

        document.YjsState = null;
        await db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await generatorWithArtwork.Generate("notebook-page", noteId, default));
        Assert.Equal(new[] { icon.Name, title.Name, fallback.Name }, EmojiReferences(generator.Requests[1].Artwork));
        Assert.Equal(1, projections);
    }

    private static CustomEmoji Emoji(string name, string asset) => new()
    {
        Id = Guid.NewGuid(), Name = name, Key = "standard-emojis/" + asset + ".png", ContentType = "image/png", SizeBytes = 1024
    };

    private static string[] EmojiReferences(NotebookCoverArtDirection? artwork) =>
        artwork?.References.Where(reference => reference.Role.Contains("emoji used", StringComparison.Ordinal))
            .Select(reference => reference.Name).ToArray() ?? [];

    private NotebookCoversController Covers(Guid userSub, bool isTeacher = false, bool includeArtwork = false)
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
        return new(db, new(db), new(db), new(new HttpFactory(projection), configuration, new(configuration)), generator,
            includeArtwork ? new NotebookCoverArtwork(db, new DisabledObjectStorage()) : null, storage)
        {
            ControllerContext = new() { HttpContext = context }
        };
    }

    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private sealed class RecordingGenerator : INotebookCoverGenerator
    {
        public bool IsConfigured => true;
        public List<(string Title, IReadOnlyList<string> Excerpts, NotebookCoverArtDirection? Artwork,
            NotebookCoverCourseLabel? Course, string? IllustrationDescription)> Requests { get; } = [];
        public Func<Task>? BeforeResult { get; set; }
        private static readonly byte[] Image = MakeImage();

        public async Task<byte[]> GenerateAsync(string title, IReadOnlyList<string> excerpts, CancellationToken ct,
            NotebookCoverArtDirection? artwork = null, NotebookCoverCourseLabel? course = null, string? illustrationDescription = null)
        {
            Requests.Add((title, excerpts, artwork, course, illustrationDescription));
            if (BeforeResult is not null) await BeforeResult();
            return Image;
        }

        private static byte[] MakeImage()
        {
            using var bitmap = new SKBitmap(8, 8);
            bitmap.Erase(SKColors.Teal);
            using var image = SKImage.FromBitmap(bitmap);
            using var webp = image.Encode(SKEncodedImageFormat.Webp, 80);
            return webp.ToArray();
        }
    }

    private sealed class GeneratedImageStorage : IObjectStorage
    {
        public bool IsConfigured => true;
        public string Bucket => "notebook-test";
        public Dictionary<string, byte[]> Objects { get; } = [];

        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            Objects[key] = buffer.ToArray();
            return new(key, Bucket, contentType, buffer.Length);
        }

        public Task DeleteAsync(string key, CancellationToken ct)
        {
            Objects.Remove(key);
            return Task.CompletedTask;
        }

        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ProjectionHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Respond { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Respond?.Invoke(request, cancellationToken) ?? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new NotionAiProjection("Håndtryk og netværksprotokoller."))
            });
    }

    private sealed record ProjectionRequest(string StateBase64, string? Mode = null);

    private sealed class HttpFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
