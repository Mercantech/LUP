using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Characters;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Mercantec.Tech.Api.Tests;

public sealed class CharacterArtworkTests : IAsyncLifetime
{
    private static readonly byte[] TestImage = MakeImage();
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Guid teacher = Guid.NewGuid(), learner = Guid.NewGuid(), outsider = Guid.NewGuid();
    private AppDbContext db = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Teaching_images_require_their_exact_publication_and_show_only_permitted_provenance()
    {
        var collection = new NotionCollection { Id = Guid.NewGuid(), Title = "Kladdebog", CoverId = Guid.NewGuid(), CreatedBySub = teacher };
        var root = new NotionPage
        {
            Id = collection.Id, CollectionId = collection.Id, Title = "Kladdens rod", CreatedBySub = teacher
        };
        var page = new NotionPage { Id = Guid.NewGuid(), CollectionId = collection.Id, Title = "Kladdeside", CreatedBySub = teacher };
        db.NotionCollections.Add(collection);
        db.NotionPages.AddRange(root, page);
        var oldCover = Guid.NewGuid();
        var currentCover = Guid.NewGuid();
        var inline = Guid.NewGuid();
        page.CoverId = currentCover;
        var library = new CharacterArtworkLibrary(db);
        library.RegisterCover("raymond", "notion-page", page.Id, oldCover, TestImage);
        library.RegisterCover("raymond", "notion-page", page.Id, currentCover, TestImage);
        library.RegisterCover("raymond", "collection", collection.Id, collection.CoverId.Value, TestImage);
        library.RegisterCover("raymond", "collection", collection.Id, Guid.NewGuid(), TestImage);
        library.RegisterPageImage("raymond", "notion-page", page.Id, inline, TestImage);
        library.RegisterPageImage("raymond", "notion-page", page.Id, inline, TestImage);
        await db.SaveChangesAsync();
        var artworkId = (await db.CharacterGeneratedArtwork.SingleAsync(item => item.OriginalAssetId == inline)).Id;
        var oldArtwork = (await db.CharacterGeneratedArtwork.SingleAsync(item => item.OriginalAssetId == oldCover)).Id;
        var manager = Controller(teacher, teaching: true);
        var viewer = Controller(learner);
        Assert.Equal(5, (await List(manager)).Items.Length);
        Assert.Empty((await List(viewer)).Items);
        Assert.IsType<NotFoundResult>(await viewer.Content(artworkId, default, thumbnail: true));

        page.PublishedTitle = "Udgivet side";
        page.PublishedAtUtc = DateTimeOffset.UtcNow;
        page.PublishedState = [0x01];
        page.PublishedCoverId = currentCover;
        page.PublishedAssetIdsJson = JsonSerializer.Serialize(new[] { $"{inline:D}-prefix-only" });
        await db.SaveChangesAsync();
        Assert.Equal("cover", Assert.Single((await List(viewer)).Items).Kind);
        Assert.IsType<NotFoundResult>(await viewer.Content(oldArtwork, default));
        Assert.IsType<NotFoundResult>(await viewer.Content(artworkId, default));

        root.PublishedAtUtc = DateTimeOffset.UtcNow;
        root.PublishedTitle = "Udgivet bog";
        root.PublishedState = [0x01];
        root.Title = "Omdøbt i kladde";
        page.PublishedAssetIdsJson = JsonSerializer.Serialize(new[] { inline });
        await db.SaveChangesAsync();
        Assert.All((await List(manager)).Items, item => Assert.Equal(root.Title, item.NotebookTitle));
        var items = (await List(viewer)).Items;
        Assert.Equal(3, items.Length);
        var publishedBook = Assert.Single(items, item => item.SourceKind == "collection");
        Assert.Equal("Udgivet bog", publishedBook.NotebookTitle);
        Assert.Equal(NotesRoutes.Collection(collection.Id) + "?published=true", publishedBook.SourceHref);
        Assert.All(items.Where(item => item.SourceKind == "notion-page"), item =>
        {
            Assert.Equal("Udgivet bog", item.NotebookTitle);
            Assert.Equal("Udgivet side", item.PageTitle);
            Assert.Equal($"/notes/{page.Id:D}?published=true", item.SourceHref);
        });
        var image = Assert.IsType<FileContentResult>(await viewer.Content(artworkId, default));
        Assert.Equal("image/webp", image.ContentType);
        Assert.Equal(TestImage, image.FileContents);
        var preview = Assert.IsType<FileContentResult>(await viewer.Content(artworkId, default, thumbnail: true));
        using var bitmap = SKBitmap.Decode(preview.FileContents);
        Assert.Equal(512, bitmap.Width);
        Assert.Equal("private, no-store", viewer.Response.Headers.CacheControl);

        page.PublishedAtUtc = null;
        root.PublishedAtUtc = null;
        await db.SaveChangesAsync();
        Assert.Empty((await List(viewer)).Items);
        Assert.IsType<NotFoundResult>(await viewer.Content(artworkId, default));
        Assert.IsType<FileContentResult>(await manager.Content(oldArtwork, default));
    }

    [Fact]
    public async Task Hold_membership_is_checked_on_list_and_content_after_publication()
    {
        var hold = new Hold { Id = Guid.NewGuid(), Name = "Mit hold", Forloeb = new() { Id = Guid.NewGuid(), Name = "Mit hold", CreatedBySub = teacher } };
        var member = new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = learner };
        var page = new NotionPage
        {
            Id = Guid.NewGuid(), HoldId = hold.Id, CreatedBySub = teacher, Title = "Side", PublishedTitle = "Side",
            PublishedAtUtc = DateTimeOffset.UtcNow, PublishedState = [0x01], PublishedCoverId = Guid.NewGuid()
        };
        db.Hold.Add(hold);
        db.Memberships.Add(member);
        db.NotionPages.Add(page);
        new CharacterArtworkLibrary(db).RegisterCover("chameleon", "notion-page", page.Id, page.PublishedCoverId.Value, TestImage);
        await db.SaveChangesAsync();

        var memberController = Controller(learner);
        var item = Assert.Single((await List(memberController)).Items);
        Assert.Equal(hold.Name, item.NotebookTitle);
        Assert.Empty((await List(Controller(outsider))).Items);
        Assert.IsType<NotFoundResult>(await Controller(outsider).Content(item.Id, default));

        db.Memberships.Remove(member);
        await db.SaveChangesAsync();
        Assert.Empty((await List(memberController)).Items);
        Assert.IsType<NotFoundResult>(await memberController.Content(item.Id, default, thumbnail: true));
    }

    [Fact]
    public async Task Personal_notebook_archive_keeps_whole_book_and_historical_art_out_of_a_page_share()
    {
        var book = new Notebook { Id = Guid.NewGuid(), OwnerSub = learner, CoverId = Guid.NewGuid() };
        var page = new NotebookDocument { Id = Guid.NewGuid(), NotebookId = book.Id, CoverId = Guid.NewGuid(), Title = "Model" };
        var inline = Guid.NewGuid();
        var historical = Guid.NewGuid();
        db.Notebooks.Add(book);
        db.NotebookDocuments.Add(page);
        db.NotebookProfiles.Add(new() { Sub = learner, DisplayName = "Eksempel elev" });
        var share = new NotebookShare { Id = Guid.NewGuid(), NotebookId = book.Id, PageId = page.Id, RecipientSub = outsider };
        db.NotebookShares.Add(share);
        var library = new CharacterArtworkLibrary(db);
        library.RegisterCover("lupp", "notebook", book.Id, book.CoverId.Value, TestImage);
        library.RegisterCover("lupp", "notebook-page", page.Id, page.CoverId.Value, TestImage);
        library.RegisterCover("lupp", "notebook-page", page.Id, historical, TestImage);
        library.RegisterPageImage("lupp", "notebook-page", page.Id, inline, TestImage);
        await db.SaveChangesAsync();

        var viewer = Controller(outsider);
        var viewed = Assert.Single((await List(viewer)).Items);
        Assert.Equal("Eksempel elev · Notesbog", viewed.NotebookTitle);
        Assert.Equal($"/notebook/{page.Id:D}", viewed.SourceHref);
        Assert.Equal(4, (await List(Controller(learner))).Items.Length);
        var annotation = await List(Controller(teacher, teaching: true));
        Assert.Contains(annotation.Items, item => item.SourceKind == "notebook");
        Assert.Contains(annotation.Items, item => item.SourceKind == "notebook-page");
        Assert.Equal(2, annotation.Items.Length);

        var savedId = (await db.CharacterGeneratedArtwork.SingleAsync(item => item.OriginalAssetId == inline)).Id;
        Assert.IsType<NotFoundResult>(await viewer.Content(savedId, default));
        page.AssetIdsJson = JsonSerializer.Serialize(new[] { inline });
        await db.SaveChangesAsync();
        Assert.Equal(2, (await List(viewer)).Items.Length);
        Assert.IsType<FileContentResult>(await viewer.Content(savedId, default));

        db.NotebookShares.Remove(share);
        await db.SaveChangesAsync();
        Assert.Empty((await List(viewer)).Items);
        Assert.IsType<NotFoundResult>(await viewer.Content(savedId, default));
    }

    [Fact]
    public async Task Pagination_counts_only_visible_art_matching_the_character()
    {
        var mine = new Notebook { Id = Guid.NewGuid(), OwnerSub = learner };
        var other = new Notebook { Id = Guid.NewGuid(), OwnerSub = outsider };
        db.Notebooks.AddRange(mine, other);
        for (var i = 0; i < 15; i++)
        {
            var item = new CharacterGeneratedArtwork
            {
                Id = Guid.NewGuid(), CharacterSlug = "lupp", Kind = "cover", SourceKind = "notebook",
                OriginalAssetId = Guid.NewGuid(), CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, i, DateTimeKind.Utc),
                NotebookId = i < 13 ? mine.Id : other.Id, Image = TestImage, Thumbnail = TestImage
            };
            db.CharacterGeneratedArtwork.Add(item);
        }
        new CharacterArtworkLibrary(db).RegisterCover("raymond", "notebook", mine.Id, Guid.NewGuid(), TestImage);
        await db.SaveChangesAsync();

        var viewer = Controller(learner);
        var first = await List(viewer, "LUPP");
        Assert.Equal(12, first.Items.Length);
        var nextOffset = Assert.IsType<int>(first.NextOffset);
        Assert.Equal(12, nextOffset);
        var second = await List(viewer, "lupp", nextOffset);
        Assert.Null(second.NextOffset);
        Assert.DoesNotContain(Assert.Single(second.Items).Id, first.Items.Select(item => item.Id));
        Assert.Equal("raymond", Assert.Single((await List(viewer, "raymond")).Items).CharacterSlug);
        Assert.IsType<NotFoundResult>(await viewer.List(default, "unknown"));
        Assert.IsType<UnauthorizedResult>(await Controller(null).List(default));
    }

    private CharacterArtworkController Controller(Guid? subject, bool teaching = false)
    {
        var claims = new List<Claim>();
        if (subject is Guid id) claims.Add(new("sub", id.ToString("D")));
        if (teaching) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(db)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } }
        };
    }

    private static async Task<CharacterGeneratedArtPage> List(CharacterArtworkController controller, string? slug = null, int offset = 0) =>
        Assert.IsType<CharacterGeneratedArtPage>(Assert.IsType<OkObjectResult>(await controller.List(default, slug, offset)).Value);

    private static byte[] MakeImage()
    {
        using var bitmap = new SKBitmap(720, 480);
        bitmap.Erase(SKColors.DarkOliveGreen);
        using var image = SKImage.FromBitmap(bitmap);
        using var webp = image.Encode(SKEncodedImageFormat.Webp, 80);
        return webp.ToArray();
    }
}
