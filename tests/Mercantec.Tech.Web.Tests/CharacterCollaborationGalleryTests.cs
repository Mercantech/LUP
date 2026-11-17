using System.Reflection;
using Bunit;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Characters;
using Mercantec.Tech.Web.Demo;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using CharacterIndex = Mercantec.Tech.Web.Pages.Characters.Index;
using GalleryPage = Mercantec.Tech.Web.Pages.Characters.Collaborations;
using LibraryPage = Mercantec.Tech.Web.Pages.Library;

namespace Mercantec.Tech.Web.Tests;

public sealed class CharacterCollaborationGalleryTests
{
    private const string GalleryPath = "/characters/collaborations";

    [Fact]
    public async Task Public_gallery_shows_each_scene_once_with_its_original_and_both_profiles()
    {
        Assert.True(PublicPageAccess.Allows(GalleryPath));
        Assert.Contains(typeof(GalleryPage).GetCustomAttributes<RouteAttribute>(),
            route => route.Template == GalleryPath);
        using var api = new CharacterQuizTestApi();
        using var context = await CreateAnonymousContext(api);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(GalleryPath);
        var page = context.Render<GalleryPage>();
        var scenes = CharacterCatalog.All.SelectMany(character => character.Collaborations)
            .DistinctBy(scene => scene.Id).ToArray();

        Assert.Equal("Når fagene mødes", page.Find("h1").TextContent.Trim());
        Assert.NotEmpty(scenes);
        Assert.Equal(scenes.Length, page.FindAll("img[src^='/images/characters/collaborations/']").Count);
        foreach (var scene in scenes)
        {
            var card = Assert.Single(page.FindAll($"[id='{scene.Anchor}']"));
            var image = Assert.Single(card.QuerySelectorAll($"a[href='{scene.Image}'] img"));
            Assert.Equal(scene.Preview, image.GetAttribute("src"));
            Assert.Equal(scene.Alt, image.GetAttribute("alt"));
            var download = Assert.Single(card.QuerySelectorAll("a[download]"));
            Assert.Equal(scene.Image, download.GetAttribute("href"));
            Assert.Equal($"{scene.Id}.png", download.GetAttribute("download"));

            foreach (var slug in new[] { scene.FirstSlug, scene.SecondSlug })
            {
                var character = CharacterCatalog.Find(slug)!;
                var participantLink = Assert.Single(card.QuerySelectorAll($"a[href='{character.Path}#{scene.Anchor}']"));
                Assert.Contains(character.Name, participantLink.TextContent, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task Shared_gallery_exercises_keep_the_task_and_hide_the_result_until_expanded()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateAnonymousContext(api);
        var page = context.Render<GalleryPage>();
        var scenes = CharacterCatalog.All.SelectMany(character => character.Collaborations)
            .DistinctBy(scene => scene.Id).Where(scene => scene.Exercise is not null).ToArray();

        Assert.NotEmpty(scenes);
        Assert.Equal(scenes.Length, page.FindAll(".character-collaboration-exercise").Count);
        foreach (var scene in scenes)
        {
            var exercise = scene.Exercise!;
            var block = page.Find($"#{scene.Anchor} .character-collaboration-exercise");
            Assert.Contains("Prøv selv", block.TextContent, StringComparison.Ordinal);
            Assert.Contains(exercise.Topic, block.TextContent, StringComparison.Ordinal);
            Assert.Contains(exercise.Task, block.TextContent, StringComparison.Ordinal);
            var details = Assert.Single(block.QuerySelectorAll("details"));
            Assert.Equal("Vis forventet resultat", details.QuerySelector("summary")!.TextContent.Trim());
            Assert.Contains(exercise.ExpectedResult, details.TextContent, StringComparison.Ordinal);
            Assert.False(details.HasAttribute("open"));
            var code = block.QuerySelector("pre code");
            Assert.Equal(exercise.StarterCode?.Trim().ReplaceLineEndings("\n"),
                code?.TextContent.Trim().ReplaceLineEndings("\n"));
        }
    }

    [Fact]
    public async Task Filtering_by_Raymond_and_resetting_restores_the_full_gallery()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateAnonymousContext(api);
        var page = context.Render<GalleryPage>();
        var allAnchors = CharacterCatalog.All.SelectMany(character => character.Collaborations)
            .DistinctBy(scene => scene.Id).Select(scene => scene.Anchor).Order(StringComparer.Ordinal).ToArray();
        var raymond = CharacterCatalog.Find("raymond")!;
        var raymondAnchors = raymond.Collaborations.Select(scene => scene.Anchor).Order(StringComparer.Ordinal).ToArray();

        page.Find("#collaboration-character").Change(raymond.Slug);

        Assert.NotEmpty(raymondAnchors);
        Assert.Equal(raymondAnchors, VisibleAnchors());
        Assert.StartsWith($"{raymondAnchors.Length} ", page.Find(".collaboration-toolbar [role='status']").TextContent.Trim());

        page.Find("#collaboration-character").Change("");

        Assert.Equal(allAnchors, VisibleAnchors());
        Assert.StartsWith($"{allAnchors.Length} ", page.Find(".collaboration-toolbar [role='status']").TextContent.Trim());

        string[] VisibleAnchors() => page.FindAll("figure[id^='collaboration-']")
            .Select(card => card.Id!).Order(StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public async Task Character_overview_and_library_category_offer_the_public_gallery_link()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateAnonymousContext(api);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/characters");
        var overview = context.Render<CharacterIndex>();
        Assert.NotEmpty(overview.FindAll($"a[href='{GalleryPath}']"));

        navigation.NavigateTo("/library?category=characters");
        var library = context.Render<LibraryPage>();
        Assert.NotEmpty(library.FindAll($"a[href='{GalleryPath}']"));
    }

    private static async Task<LearningUiTestContext> CreateAnonymousContext(CharacterQuizTestApi api)
    {
        var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<LearningContext>());
        await context.Auth.LogoutLocalAsync();
        return context;
    }
}
