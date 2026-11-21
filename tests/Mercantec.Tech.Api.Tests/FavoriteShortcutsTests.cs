using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class FavoriteShortcutsTests : IAsyncLifetime
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly FavoriteShortcut Courses = new("/courses", "Mine hold", "", "people");
    private static readonly FavoriteShortcut Notes = new("/notes", "Mine noter", "", "book");
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = OpenDatabase();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Favorites_follow_the_account_with_its_saved_order_and_explicit_empty_state()
    {
        FavoriteShortcutsDto reordered;
        await using (var db = OpenDatabase())
        {
            var alice = For(db, Alice);
            var empty = Success(await alice.Get(default));
            Assert.Null(empty.Revision);
            Assert.Empty(empty.Items);
            Assert.Empty(await db.FavoriteShortcutLists.ToListAsync());

            var added = Success(await alice.Update(new(null, [Courses, Notes]), default));
            reordered = Success(await alice.Update(new(added.Revision, [Notes, Courses]), default));
            Assert.NotNull(reordered.Revision);
            Assert.NotEqual(added.Revision, reordered.Revision);
        }

        await using var nextSession = OpenDatabase();
        var reopened = For(nextSession, Alice);
        Assert.Equal(new[] { Notes, Courses }, Success(await reopened.Get(default)).Items);
        Assert.Empty(Success(await For(nextSession, Bob).Get(default)).Items);

        var removed = Success(await reopened.Update(new(reordered.Revision, []), default));
        Assert.NotNull(removed.Revision);
        Assert.Equal(removed.Revision, Success(await reopened.Get(default)).Revision);
        Assert.Empty(Success(await reopened.Get(default)).Items);
    }

    [Fact]
    public async Task Stale_updates_and_a_changed_account_cannot_replace_saved_favorites()
    {
        await using var db = OpenDatabase();
        var alice = For(db, Alice);
        var first = Success(await alice.Update(new(null, [Courses]), default));
        var newest = Success(await alice.Update(new(first.Revision, [Notes]), default));

        Assert.IsType<ConflictObjectResult>((await alice.Update(new(null, [Courses]), default)).Result);
        Assert.IsType<ConflictObjectResult>((await alice.Update(new(first.Revision, []), default)).Result);
        var bob = For(db, Bob);
        bob.Request.Headers[FavoriteShortcutRules.SubjectHeader] = Alice.ToString();
        Assert.IsType<ConflictObjectResult>((await bob.Get(default)).Result);
        Assert.IsType<ConflictObjectResult>((await bob.Update(new(newest.Revision, []), default)).Result);
        Assert.Equal(Notes, Assert.Single(Success(await alice.Get(default)).Items));
    }

    [Fact]
    public async Task Privacy_export_contains_only_the_callers_favorites()
    {
        await using var db = OpenDatabase();
        var alice = For(db, Alice);
        Success(await alice.Update(new(null, [Courses]), default));
        Success(await For(db, Bob).Update(new(null, [Notes]), default));
        var privacy = new PrivacyController(db) { ControllerContext = alice.ControllerContext };

        var export = Assert.IsType<FileContentResult>(await privacy.Export(default));
        using var document = JsonDocument.Parse(export.FileContents);
        var favorites = document.RootElement.GetProperty("PlatformRecords").GetProperty("favoriteShortcuts");
        Assert.Equal(Courses.Href, Assert.Single(favorites.EnumerateArray()).GetProperty("Href").GetString());
    }

    [Theory]
    [InlineData("https://example.org")]
    [InlineData("//example.org")]
    [InlineData("/%2Fexample.org")]
    [InlineData("/notes/../api/test")]
    [InlineData("/AUTH/callback?code=test")]
    [InlineData("/%6cogin")]
    [InlineData("/teacher/view-as/sample")]
    public async Task Unsafe_destinations_are_rejected_without_saving(string href)
    {
        await using var db = OpenDatabase();
        var controller = For(db, Alice);
        var result = await controller.Update(new(null, [Notes with { Href = href }]), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(Success(await controller.Get(default)).Revision);
    }

    [Fact]
    public void Duplicate_destinations_are_rejected_but_different_views_can_be_saved()
    {
        Assert.False(FavoriteShortcutRules.IsValidList([Courses, Courses with { Href = "/Courses/" }]));
        Assert.True(FavoriteShortcutRules.IsValidList(
            [Courses with { Href = "/courses?view=content" }, Courses with { Href = "/courses?view=groups" }]));
    }

    private AppDbContext OpenDatabase() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private static FavoriteShortcutsController For(AppDbContext db, Guid subject)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", subject.ToString())], "test"))
        };
        http.Request.Headers[FavoriteShortcutRules.SubjectHeader] = subject.ToString();
        return new FavoriteShortcutsController(db) { ControllerContext = new() { HttpContext = http } };
    }

    private static FavoriteShortcutsDto Success(ActionResult<FavoriteShortcutsDto> result) =>
        Assert.IsType<FavoriteShortcutsDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
