using System.Collections.Concurrent;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Characters;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using CharacterDetail = Mercantec.Tech.Web.Pages.Characters.Detail;
using CharacterIndex = Mercantec.Tech.Web.Pages.Characters.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class CharacterDetailTests
{
    [Fact]
    public async Task Guest_can_open_the_published_exercise_after_login()
    {
        var id = Guid.Parse("11111111-2222-4333-8444-555555555555");
        using var api = new CharacterQuizTestApi("raymond",
            [new(id, "Find den manglende række", "SQL", 5, 6, "Begynder")]);
        using var context = await LearningUiTestContext.CreateAsync(api);
        await context.Auth.LogoutLocalAsync();

        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "raymond"));
        var section = page.WaitForElement(".character-quizzes");
        Assert.Equal("Videnstjek med Raymond", section.QuerySelector("h2")!.TextContent.Trim());
        Assert.Equal("Find den manglende række", section.QuerySelector("h3")!.TextContent);
        Assert.Contains("6 spørgsmål", section.TextContent);
        var link = Assert.Single(section.QuerySelectorAll("a"));
        Assert.StartsWith("Log ind og åbn quiz", link.TextContent.Trim());
        Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString($"/videnstjek/{id}?karakter=raymond"),
            link.GetAttribute("href"));
    }

    [Fact]
    public async Task ChoosingAnAppearanceUpdatesThePortraitAndPressedState()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));

        page.FindAll(".character-appearance-picker button")[2].Click();

        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
        var selected = Assert.Single(page.FindAll(".character-appearance-picker button[aria-pressed='true']"));
        Assert.Contains("Festtøj", selected.TextContent);
        Assert.Equal(3, page.FindAll(".character-appearance-picker button[aria-pressed='false']").Count);
    }

    [Fact]
    public async Task RaymondArtworkCanBeOpenedAndDownloadedWithoutReplacingTheSelectedPortrait()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();

        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        var artworks = CharacterCatalog.Find("raymond")!.Reactions.Concat(CharacterCatalog.Find("raymond")!.Scenes).ToArray();
        var figures = page.FindAll(".character-artwork-gallery figure");
        Assert.Equal(artworks.Length, figures.Count);
        for (var index = 0; index < artworks.Length; index++)
        {
            var expected = artworks[index];
            var figure = figures[index];
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            var filePath = expected.Image.Split('?')[0];
            Assert.Equal(expected.Preview, image.GetAttribute("src"));
            Assert.Equal(expected.Alt, image.GetAttribute("alt"));
            Assert.StartsWith("/images/characters/raymond/", filePath);
            Assert.EndsWith(".png", filePath);
            Assert.Equal(expected.Image, preview.GetAttribute("href"));
            Assert.Equal("_blank", preview.GetAttribute("target"));
            Assert.Contains("noopener", preview.GetAttribute("rel"));
            Assert.Equal(expected.Image, download.GetAttribute("href"));
            Assert.Equal($"raymond-{Path.GetFileName(filePath)}", download.GetAttribute("download"));
        }
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(4, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal(54, page.FindAll(".character-artwork-preview").Select(link => link.GetAttribute("href")).Distinct().Count());
    }

    [Fact]
    public async Task RaymondArtworkDoesNotFollowTheUserToOtherProfiles()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(2, page.FindAll(".character-artwork-gallery").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));
        Assert.Equal(2, page.FindAll(".character-artwork-gallery").Count);
        Assert.Empty(page.FindAll(".character-animation-card"));
        Assert.Equal(2, page.FindAll(".character-scenes figure").Count);
        Assert.All(page.FindAll(".character-scenes img"), image => Assert.StartsWith("/images/characters/lupp/scenes/", image.GetAttribute("src")));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.All(page.FindAll(".character-reactions img"), image => Assert.StartsWith("/images/characters/lupp/", image.GetAttribute("src")));
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "vatson"));
        Assert.Empty(page.FindAll(".character-gallery"));
        Assert.Equal(2, page.FindAll(".character-scenes figure").Count);
        Assert.All(page.FindAll(".character-scenes img"), image => Assert.StartsWith("/images/characters/vatson/scenes/", image.GetAttribute("src")));
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.All(page.FindAll(".character-reactions img"), image => Assert.StartsWith("/images/characters/vatson/", image.GetAttribute("src")));

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task VatsonsLargeGalleryContainsAllExistingEmojisWithPublicOriginalDownloads()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "vatson"));

        Assert.Single(page.FindAll(".character-reactions-large"));
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.Contains("63 af 63", page.Find(".character-artwork-count").TextContent);
        Assert.All(page.FindAll(".character-reactions figure"), figure =>
        {
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            var source = image.GetAttribute("src")!;
            Assert.StartsWith("/images/characters/vatson/emojis/", source);
            Assert.EndsWith(".png", source);
            Assert.DoesNotContain("ticket", source, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
            Assert.Equal(source, preview.GetAttribute("href"));
            Assert.Equal("_blank", preview.GetAttribute("target"));
            Assert.Contains("noopener", preview.GetAttribute("rel"));
            Assert.Equal(source, download.GetAttribute("href"));
            Assert.Equal($"vatson-{Path.GetFileName(source)}", download.GetAttribute("download"));
        });
        Assert.Equal(63, page.FindAll(".character-reactions img").Select(image => image.GetAttribute("src")).Distinct().Count());
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "https://vatek.dk/");
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "https://videnscenterportalen.dk/vat/");
    }

    [Fact]
    public async Task VatsonsEmojiSearchFiltersByMotifAndCanBeClearedOrResetByProfileNavigation()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "vatson"));

        page.Find(".character-artwork-search input").Input("kaffe");
        Assert.Single(page.FindAll(".character-reactions figure"));
        Assert.EndsWith("/kaffe.png", page.Find(".character-reactions img").GetAttribute("src"));
        Assert.Contains("1 af 63", page.Find(".character-artwork-count").TextContent);

        page.Find(".character-artwork-search input").Input("PLC");
        Assert.Equal(2, page.FindAll(".character-reactions figure").Count);

        page.Find(".character-artwork-search input").Input("findes-ikke-her");
        Assert.Empty(page.FindAll(".character-reactions figure"));
        Assert.Contains("Ingen emojis matcher", page.Find(".character-artwork-empty").TextContent);

        page.Find(".character-artwork-search button").Click();
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.Empty(page.FindAll(".character-artwork-empty"));
        page.Find(".character-artwork-search input").Input("kaffe");

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Single(page.FindAll(".character-artwork-search"));
        Assert.Single(page.FindAll(".character-reactions-large"));
        Assert.Equal("", page.Find(".character-artwork-search input").GetAttribute("value"));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "vatson"));
        Assert.Equal(63, page.FindAll(".character-reactions figure").Count);
        Assert.Equal("", page.Find(".character-artwork-search input").GetAttribute("value"));
    }

    [Fact]
    public async Task RaymondAnimationsStartOnlyOnRequestAndCanBeStoppedWithoutChangingThePortrait()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();

        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
        Assert.All(page.FindAll(".character-animation-screen img"), image => Assert.EndsWith(".png", image.GetAttribute("src")));
        Assert.All(page.FindAll(".character-animation-actions button"), button => Assert.Equal("false", button.GetAttribute("aria-pressed")));
        Assert.All(page.FindAll(".character-animation-actions a"), link =>
        {
            Assert.StartsWith("/images/characters/raymond/animations/", link.GetAttribute("href"));
            Assert.EndsWith(".gif", link.GetAttribute("href"));
            Assert.EndsWith(".gif", link.GetAttribute("download"));
        });

        page.Find("button[aria-label='Afspil Forbind tabeller']").Click();
        Assert.EndsWith("/join-tables.gif", page.Find("#animation-join-tables img").GetAttribute("src"));
        Assert.EndsWith("/sort-rows.png", page.Find("#animation-sort-rows img").GetAttribute("src"));
        Assert.Equal("true", page.Find("button[aria-label='Stop Forbind tabeller']").GetAttribute("aria-pressed"));
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));

        page.Find("button[aria-label='Stop Forbind tabeller']").Click();
        Assert.EndsWith("/join-tables.png", page.Find("#animation-join-tables img").GetAttribute("src"));
        Assert.Equal("false", page.Find("button[aria-label='Afspil Forbind tabeller']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task AnimationPlaybackIsResetWhenLeavingRaymondsProfile()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.Find("button[aria-label='Afspil Find den manglende række']").Click();

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));
        Assert.Empty(page.FindAll(".character-animations"));
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
        Assert.All(page.FindAll(".character-animation-screen img"), image => Assert.EndsWith(".png", image.GetAttribute("src")));
        Assert.Empty(page.FindAll(".character-animation-actions button[aria-pressed='true']"));
    }

    [Fact]
    public async Task MovingBetweenProfilesResetsTheAppearanceWithoutShowingThePreviousPortrait()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[3].Click();

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));

        Assert.Equal("LUPP", page.Find("h1").TextContent);
        Assert.Equal(CharacterCatalog.Find("lupp")!.DisplayPortrait!.Image, page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(2, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal("Vælg udtryk for LUPP", page.Find(".character-appearance-picker").GetAttribute("aria-label"));

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));

        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Contains("Portræt", page.Find(".character-appearance-picker button[aria-pressed='true']").TextContent);
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.All(page.FindAll(".character-gallery img"), image => Assert.StartsWith("/images/characters/raymond/infographics/", image.GetAttribute("src")));
    }

    [Theory]
    [InlineData("beaver", "Bæver", "Blå team", "robuste")]
    [InlineData("fox", "Ræv", "Rød team", "etisk")]
    public async Task SecurityTeachersHaveTheirOwnPortraitRoleAndAnimalExplanation(string slug, string name, string team, string explanation)
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, slug));

        Assert.Equal(name, page.Find("h1").TextContent);
        Assert.Contains(team, page.Find(".character-subtitle").TextContent);
        Assert.Equal(CharacterCatalog.Find(slug)!.DisplayPortrait!.Image, page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Contains(explanation, page.Find(".character-features").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(page.FindAll(".character-gallery, .character-status"));
        Assert.Equal(12, page.FindAll(".character-reactions figure").Count);
        var reactions = CharacterCatalog.Find(slug)!.Reactions;
        var figures = page.FindAll(".character-reactions figure");
        for (var index = 0; index < reactions.Count; index++)
        {
            var expected = reactions[index];
            var figure = figures[index];
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            Assert.Equal(expected.Preview, image.GetAttribute("src"));
            Assert.Equal(expected.Alt, image.GetAttribute("alt"));
            Assert.Equal(expected.Image, preview.GetAttribute("href"));
            Assert.Equal(expected.Image, download.GetAttribute("href"));
            Assert.StartsWith($"/images/characters/{slug}/reactions/", expected.Image);
            Assert.EndsWith(".png", expected.Image);
            Assert.EndsWith(".webp", expected.Preview);
            Assert.Equal($"{slug}-{Path.GetFileName(expected.Image)}", download.GetAttribute("download"));
        }
    }

    [Fact]
    public async Task GameDesignIllustrationsAreSeparateFromPortraitsAndRemainLocallyHosted()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "lupp"));

        Assert.Contains("Underviser i Game Design", page.Find(".character-subtitle").TextContent);
        Assert.Equal(2, page.FindAll(".character-appearance-picker button").Count);
        var figures = page.FindAll(".character-gallery figure");
        Assert.Equal(24, figures.Count);
        Assert.Equal(15, figures.Count(figure => figure.QuerySelector("img")!.GetAttribute("src")!.EndsWith(".webp")));
        Assert.All(figures, figure =>
        {
            var img = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-illustration-preview")!;
            var download = figure.QuerySelector(".character-illustration-download")!;
            var source = img.GetAttribute("src");
            Assert.StartsWith("/images/characters/lupp/", source);
            Assert.False(string.IsNullOrWhiteSpace(img.GetAttribute("alt")));
            Assert.Equal(Path.ChangeExtension(source, ".png"), preview.GetAttribute("href"));
            Assert.Equal(preview.GetAttribute("href"), download.GetAttribute("href"));
            Assert.Equal("_blank", preview.GetAttribute("target"));
            Assert.Contains("noopener", preview.GetAttribute("rel"));
        });
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "/courses/template/game-design");
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "https://app.notion.com/p/Game-Design-3c6123f464aa8158906ae7c7a3643328");

        page.FindAll(".character-appearance-picker button")[1].Click();
        Assert.EndsWith("/lup-fireworks-player.png", page.Find(".character-main-art img").GetAttribute("src"));

        page.Render(parameters => parameters.Add(p => p.Slug, "fox"));
        Assert.Empty(page.FindAll(".character-gallery, .character-appearance-picker"));
        Assert.Equal(CharacterCatalog.Find("fox")!.DisplayPortrait!.Image, page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task Lupps48EmojisUseWebpPreviewsAndPngDownloadsWithoutChangingTheSelectedPortrait()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "lupp"));
        page.FindAll(".character-appearance-picker button")[1].Click();

        var reactions = CharacterCatalog.Find("lupp")!.Reactions;
        Assert.Equal(48, reactions.Count);
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(48, page.FindAll(".character-reactions .character-artwork-download").Select(link => link.GetAttribute("href")).Distinct().Count());
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);
        Assert.Contains("Game Design", page.Find(".character-reactions").TextContent);
        Assert.Contains("character-reactions-large", page.Find(".character-reactions").ClassList);
        foreach (var (figure, expected) in page.FindAll(".character-reactions figure").Zip(reactions))
        {
            var image = figure.QuerySelector("img")!;
            var preview = figure.QuerySelector(".character-artwork-preview")!;
            var download = figure.QuerySelector(".character-artwork-download")!;
            Assert.Equal(expected.Preview, image.GetAttribute("src"));
            Assert.Equal(expected.Alt, image.GetAttribute("alt"));
            Assert.StartsWith("/images/characters/lupp/reactions/", expected.Image);
            Assert.EndsWith(".webp", expected.Preview);
            Assert.EndsWith(".png", expected.Image);
            Assert.Equal(expected.Image, preview.GetAttribute("href"));
            Assert.Equal(expected.Image, download.GetAttribute("href"));
            Assert.Equal($"lupp-{Path.GetFileName(expected.Image)}", download.GetAttribute("download"));
        }

        page.Find(".character-reactions .character-artwork-search input").Input("Godmorgen");
        Assert.Single(page.FindAll(".character-reactions figure"));
        Assert.Contains("1 af 48", page.Find(".character-reactions .character-artwork-count").TextContent);
        Assert.Equal("/images/characters/lupp/reactions/good-morning.png", page.Find(".character-reactions .character-artwork-download").GetAttribute("href"));
        Assert.EndsWith("/lup-fireworks-player.png", page.Find(".character-main-art img").GetAttribute("src"));

        page.Find(".character-reactions .character-artwork-search input").Input("ingen-match-her-987");
        Assert.Empty(page.FindAll(".character-reactions figure"));
        Assert.Contains("Ingen emojis", page.Find(".character-reactions .character-artwork-empty").TextContent);
        page.Find(".character-reactions .character-artwork-search button").Click();

        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Contains("48 af 48", page.Find(".character-reactions .character-artwork-count").TextContent);
        Assert.EndsWith("/lup-fireworks-player.png", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);
    }

    [Fact]
    public async Task RaymondInfographicsCoverTheSuppliedObjectivesWithAccessibleTextAndOriginalDownloads()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();
        var illustrations = CharacterCatalog.Find("raymond")!.Illustrations;

        Assert.Equal(Enumerable.Range(1, 24), illustrations.SelectMany(item => item.Objectives).Distinct().Order());
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.Contains("16474", page.Find(".character-gallery header").TextContent);
        Assert.DoesNotContain("Game Design", page.Find(".character-gallery header").TextContent);
        foreach (var (figure, item) in page.FindAll(".character-gallery figure").Zip(illustrations))
        {
            Assert.Equal(item.Preview, figure.QuerySelector("img")!.GetAttribute("src"));
            Assert.EndsWith(".webp", item.Preview);
            Assert.Equal(item.Image, figure.QuerySelector(".character-illustration-preview")!.GetAttribute("href"));
            var download = figure.QuerySelector(".character-illustration-download")!;
            Assert.Equal(item.Image, download.GetAttribute("href"));
            Assert.EndsWith(".png", item.Image);
            Assert.Equal($"raymond-{Path.GetFileName(item.Image)}", download.GetAttribute("download"));
            Assert.False(string.IsNullOrWhiteSpace(figure.QuerySelector("img")!.GetAttribute("alt")));
            Assert.Contains(item.Stage!, figure.QuerySelector(".character-illustration-objectives")!.TextContent);
            Assert.All(item.Explanation, paragraph => Assert.Contains(paragraph, figure.TextContent));
            Assert.True(item.Explanation.Count >= 3);
            Assert.NotEmpty(item.Sources);
            foreach (var source in figure.QuerySelectorAll(".character-illustration-sources a"))
            {
                Assert.StartsWith("https://", source.GetAttribute("href"));
                Assert.Equal("_blank", source.GetAttribute("target"));
                Assert.Contains("noopener", source.GetAttribute("rel"));
            }
        }
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
    }

    [Fact]
    public async Task InfographicFiltersKeepThePortraitAndResetWhenChangingProfiles()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();
        page.FindAll(".character-illustration-stages button").Single(button => button.TextContent == "H3").Click();
        Assert.Equal(3, page.FindAll(".character-gallery figure").Count);
        Assert.All(page.FindAll(".character-illustration-objectives"), text => Assert.StartsWith("H3", text.TextContent));
        Assert.Contains("3 af 16", page.Find(".character-illustration-count").TextContent);

        page.Find("#character-illustration-search").Input("findes-ikke-987");
        Assert.Empty(page.FindAll(".character-gallery figure"));
        Assert.Contains("Ingen infografikker", page.Find(".character-illustration-empty").TextContent);
        page.Find(".character-illustration-search button").Click();
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));

        page.FindAll(".character-illustration-stages button").Single(button => button.TextContent == "H2").Click();
        Assert.Equal(3, page.FindAll(".character-gallery figure").Count);
        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));
        Assert.Equal(24, page.FindAll(".character-gallery figure").Count);
        Assert.Empty(page.FindAll(".character-illustration-tools, .character-illustration-explanation"));
        Assert.All(page.FindAll(".character-gallery img"), image => Assert.StartsWith("/images/characters/lupp/", image.GetAttribute("src")));
        Assert.Equal(24, page.FindAll(".character-illustration-download").Count);

        page.Render(parameters => parameters.Add(p => p.Slug, "raymond"));
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.Equal("", page.Find("#character-illustration-search").GetAttribute("value"));
        Assert.Equal("Alle", page.Find(".character-illustration-stages button[aria-pressed='true']").TextContent);
        Assert.EndsWith("/neutral.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task OverviewIncludesAllCharactersAndUsesStableRoutes()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterIndex>();

        Assert.Equal(CharacterCatalog.All.Count, page.FindAll(".character-card").Count);
        Assert.Equal(CharacterCatalog.All.Count, CharacterCatalog.All.Select(character => character.Path).Distinct().Count());
        Assert.Equal("Bilbo Backend", page.Find("a[href='/characters/bilbo'] h2").TextContent);
        Assert.Equal("Gitmejsen", page.Find("a[href='/characters/gitmejsen'] h2").TextContent);
        Assert.Equal("Karla Eremitkrebs", page.Find("a[href='/characters/karla'] h2").TextContent);
        Assert.Equal("Iben Ildflue", page.Find("a[href='/characters/iben'] h2").TextContent);
        Assert.Equal("Bæver", page.Find("a[href='/characters/beaver'] h2").TextContent);
        Assert.Equal("Ræv", page.Find("a[href='/characters/fox'] h2").TextContent);
        Assert.Equal("Docker", page.Find("a[href='/characters/whale'] h2").TextContent);
        Assert.Equal("Gummianden", page.Find("a[href='/characters/gummianden'] h2").TextContent);
        Assert.Equal("Cyberbien", page.Find("a[href='/characters/bee'] h2").TextContent);
        Assert.Equal("Næsebladet", page.Find("a[href='/characters/bat'] h2").TextContent);
    }

    [Fact]
    public async Task IbenProfileBindsLampInBothDirectionsAndLinksTeachingMaterial()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "iben"));

        Assert.Contains("Iben Sanderson Edwards Fowler", page.Find(".character-description").TextContent);
        Assert.Equal("/images/characters/iben/iben-fritlagt.webp", page.Find(".character-main-art img").GetAttribute("src"));
        var appearances = page.FindAll(".character-appearance-picker button");
        Assert.Equal("true", appearances[0].GetAttribute("aria-pressed"));
        appearances[1].Click();
        Assert.Equal("/images/characters/iben/portrait.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(12, page.FindAll(".character-gallery figure pre code").Count);
        Assert.Equal(2, page.FindAll(".character-scenes figure").Count);
        Assert.Equal(2, page.FindAll(".character-residence figure").Count);
        Assert.Equal(2, page.FindAll(".character-collaborations figure").Count);
        Assert.Equal("65 %", page.Find(".binding-lab output").TextContent);
        page.Find("#iben-brightness").Input("33");
        Assert.Equal("33 %", page.Find(".binding-lab output").TextContent);
        Assert.Equal("Skyder → C#", page.Find(".binding-trace strong").TextContent);
        page.Find(".binding-lab button").Click();
        Assert.Equal("0", page.Find("#iben-brightness").GetAttribute("value"));
        Assert.Equal("C# → Skyder", page.Find(".binding-trace strong").TextContent);
        page.Find(".binding-lab button").Click();
        Assert.Equal("100 %", page.Find(".binding-lab output").TextContent);
        page.FindAll(".binding-actions button")[1].Click();
        Assert.Equal("65", page.Find("#iben-brightness").GetAttribute("value"));
        Assert.Equal("brightness = 65", page.Find(".binding-trace code").TextContent);
        Assert.Contains("@bind:event=\"oninput\"", page.Find(".binding-lab pre").TextContent);

        page.Render(parameters => parameters.Add(p => p.Slug, "karla"));
        Assert.Empty(page.FindAll(".binding-lab"));
    }

    [Fact]
    public async Task KarlaProfileShowsFlutterArtworkAndLinksToBothCollaborators()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "karla"));

        Assert.Equal("Karla Eremitkrebs", page.Find("h1").TextContent);
        Assert.Contains("Flutter", page.Find(".character-subtitle").TextContent);
        Assert.Equal("/images/characters/karla/portrait.webp", page.Find(".character-main-art img").GetAttribute("src"));
        Assert.Contains(page.FindAll(".character-links a"), link => link.GetAttribute("href") == "/characters/karla#character-gallery-title");
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(48, page.FindAll(".character-reactions .character-artwork-download").Select(link => link.GetAttribute("href")).Distinct().Count());
        Assert.Equal(2, page.FindAll(".character-scenes figure").Count);
        Assert.Equal(12, page.FindAll(".character-gallery figure").Count);
        Assert.All(page.FindAll(".character-gallery img"), image =>
        {
            Assert.StartsWith("/images/characters/karla/", image.GetAttribute("src"));
            Assert.False(string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
        });
        Assert.Equal("Skalhuset", page.Find(".character-residence h2").TextContent);
        Assert.Equal(new[] { "/images/characters/karla/home/outside.webp", "/images/characters/karla/home/inside.webp" },
            page.FindAll(".character-residence figure img").Select(image => image.GetAttribute("src")));

        Assert.Equal(3, page.FindAll(".character-collaborations figure").Count);
        Assert.Equal("/characters/iben#collaboration-iben-karla", page.Find("#collaboration-iben-karla .character-collaboration-links a").GetAttribute("href"));
        Assert.Equal("/characters/lupp#collaboration-karla-lupp", page.Find("#collaboration-karla-lupp .character-collaboration-links a").GetAttribute("href"));
        Assert.Equal("/characters/bilbo#collaboration-karla-bilbo", page.Find("#collaboration-karla-bilbo .character-collaboration-links a").GetAttribute("href"));

        page.Render(parameters => parameters.Add(p => p.Slug, "lupp"));
        Assert.Equal("/characters/karla#collaboration-karla-lupp", page.Find("#collaboration-karla-lupp .character-collaboration-links a").GetAttribute("href"));
        page.Render(parameters => parameters.Add(p => p.Slug, "bilbo"));
        Assert.Equal("/characters/karla#collaboration-karla-bilbo", page.Find("#collaboration-karla-bilbo .character-collaboration-links a").GetAttribute("href"));
    }

    [Fact]
    public async Task GitmejsenProfileShowsGitExercisesAndSearchableReactions()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "gitmejsen"));

        Assert.Equal("Gitmejsen", page.Find("h1").TextContent);
        Assert.Equal(3, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal(14, page.FindAll(".character-scenes figure").Count);
        Assert.Contains("fire små hilsner fra Mumi-universet", page.Find(".character-scenes header").TextContent);
        Assert.Equal(30, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
        var exercises = page.FindAll(".character-gallery figure");
        Assert.Equal(11, exercises.Count);
        Assert.Contains("git diff --cached", exercises[0].QuerySelector("code")!.TextContent);
        Assert.Contains("git log --graph", exercises[1].QuerySelector("code")!.TextContent);
        Assert.Contains("i en udskiftelig, firkantet frontplade.", exercises[2].QuerySelector("code")!.TextContent);
        Assert.Contains(exercises, exercise => exercise.QuerySelector("code")!.TextContent.Contains("git fetch origin", StringComparison.Ordinal));
        Assert.Contains(exercises, exercise => exercise.QuerySelector("code")!.TextContent.Contains("git bisect reset", StringComparison.Ordinal));
        Assert.All(exercises, exercise => Assert.EndsWith(".png", exercise.QuerySelector("a[download]")!.GetAttribute("href")));

        page.FindAll(".character-appearance-picker button")[1].Click();
        page.Find("#character-reactions-title-search").Input("stash");
        Assert.Single(page.FindAll(".character-reactions figure"));
        Assert.EndsWith("/vinter.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task WhaleProfileLinksAllDevNetObjectivesAndKeepsPortraitWhenFiltering()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "whale"));

        Assert.Equal("Docker", page.Find("h1").TextContent);
        Assert.Equal(4, page.FindAll(".character-appearance-picker button").Count);
        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(6, page.FindAll(".character-scenes figure").Count);
        Assert.Equal(3, page.FindAll(".character-animation-card").Count);
        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        Assert.Equal(Enumerable.Range(1, 7), CharacterCatalog.Find("whale")!.Illustrations
            .SelectMany(item => item.Objectives).Distinct().Order());
        Assert.Contains(page.FindAll(".character-links a"), link =>
            link.GetAttribute("href") == "https://maalpinde.lup.dk/vuf/#deploy-or-die");

        page.FindAll(".character-appearance-picker button")[2].Click();
        page.Find("#character-illustration-search").Input("rollback");
        Assert.NotEmpty(page.FindAll(".character-gallery figure"));
        Assert.True(page.FindAll(".character-gallery figure").Count < 16);
        Assert.EndsWith("/vaerksted.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task DuckProfileKeepsTheSelectedDuckWhileFindingAndPlayingDebuggingMaterials()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "gummianden"));

        var appearances = page.FindAll(".character-appearance-picker button");
        Assert.Equal(6, appearances.Count);
        Assert.Equal(CharacterCatalog.Find("gummianden")!.DisplayPortrait!.Image, page.Find(".character-main-art img").GetAttribute("src"));
        appearances.Single(button => button.TextContent.Contains("Logvagten", StringComparison.Ordinal)).Click();

        Assert.Equal(16, page.FindAll(".character-gallery figure").Count);
        page.Find("#character-illustration-search").Input("breakpoint");
        Assert.InRange(page.FindAll(".character-gallery figure").Count, 1, 15);
        Assert.Contains(page.FindAll(".character-gallery .character-illustration-explanation"), card =>
            card.QuerySelector("code") is not null);

        var animation = page.FindAll(".character-animation-card")[0];
        var poster = animation.QuerySelector("img")!.GetAttribute("src");
        var download = animation.QuerySelector("a[download]")!.GetAttribute("href");
        Assert.EndsWith(".gif", download);
        Assert.NotEqual(download, poster);
        animation.QuerySelector("button")!.Click();
        Assert.Equal(download, page.Find(".character-animation-card img").GetAttribute("src"));
        page.Find(".character-animation-card button").Click();
        Assert.Equal(poster, page.Find(".character-animation-card img").GetAttribute("src"));
        Assert.EndsWith("/logvagten.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task UnknownProfileShowsTheReturnLinkAndRemovesPreviousProfileContent()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));

        page.Render(parameters => parameters.Add(p => p.Slug, "unknown"));

        Assert.Equal("Karakteren blev ikke fundet", page.Find("h1").TextContent);
        Assert.Equal("/characters", page.Find("a.action").GetAttribute("href"));
        Assert.Empty(page.FindAll(".character-detail, .character-appearance-picker, .character-main-art img"));
    }

    [Fact]
    public async Task Raymonds48EmojisCanBeSearchedAndClearedWithoutChangingTheSelectedPortrait()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(parameters => parameters.Add(p => p.Slug, "raymond"));
        page.FindAll(".character-appearance-picker button")[2].Click();

        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Equal(48, page.FindAll(".character-reactions .character-artwork-download").Select(link => link.GetAttribute("href")).Distinct().Count());
        Assert.Single(page.FindAll(".character-reactions .character-artwork-download[href='/images/characters/raymond/reactions/laughing.png?v=097b5eeb']"));
        page.Find(".character-reactions .character-artwork-search input").Input("Tommel op");

        Assert.Single(page.FindAll(".character-reactions figure"));
        Assert.Contains("1 af 48", page.Find(".character-reactions .character-artwork-count").TextContent);
        Assert.Equal("/images/characters/raymond/reactions/thumbs-up.png", page.Find(".character-reactions .character-artwork-download").GetAttribute("href"));
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));

        page.Find(".character-reactions .character-artwork-search input").Input("ingen-match-her-987");
        Assert.Empty(page.FindAll(".character-reactions figure"));
        Assert.Contains("Ingen emojis", page.Find(".character-reactions .character-artwork-empty").TextContent);
        page.Find(".character-reactions .character-artwork-search button").Click();

        Assert.Equal(48, page.FindAll(".character-reactions figure").Count);
        Assert.Contains("48 af 48", page.Find(".character-reactions .character-artwork-count").TextContent);
        Assert.EndsWith("/fancy.webp", page.Find(".character-main-art img").GetAttribute("src"));
    }

    [Fact]
    public async Task Artwork_galleries_start_closed_with_counts_and_stable_link_targets()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "raymond"));
        var profile = CharacterCatalog.Find("raymond")!;

        Assert.Equal(2, page.FindAll("details.character-artwork-gallery").Count);
        Assert.Empty(page.FindAll("details.character-artwork-gallery[open]"));
        Assert.Equal(profile.Reactions.Count.ToString(), page.Find("summary #character-reactions-title .character-artwork-total").TextContent);
        Assert.Equal(profile.Scenes.Count.ToString(), page.Find("summary #character-scenes-title .character-artwork-total").TextContent);
        Assert.NotEmpty(page.FindAll(".character-animation-card"));
        Assert.NotEmpty(page.FindAll(".character-gallery figure"));
    }

    [Fact]
    public async Task Artwork_links_follow_the_fragment_and_wait_for_the_matching_slug()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api, configure: services =>
        {
            var storage = (IJSRuntime)services.Last(service => service.ServiceType == typeof(IJSRuntime)).ImplementationInstance!;
            services.AddSingleton<IJSRuntime>(new GalleryScrollRuntime(storage));
        });
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var js = (GalleryScrollRuntime)context.Services.GetRequiredService<IJSRuntime>();
        navigation.NavigateTo("/characters/raymond#character-reactions-title");
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "raymond"));
        page.WaitForAssertion(() => Assert.Equal(("character-reactions-title", navigation.Uri), Assert.Single(js.Sections)));

        await page.InvokeAsync(() => navigation.NavigateTo("/characters/raymond#character-scenes-title"));
        page.WaitForAssertion(() => Assert.Equal(("character-scenes-title", navigation.Uri), js.Sections.Last()));

        await page.InvokeAsync(() => navigation.NavigateTo("/characters/lupp#character-reactions-title"));
        Assert.Equal(2, js.Sections.Count);
        page.Render(p => p.Add(x => x.Slug, "lupp"));
        page.WaitForAssertion(() => Assert.Equal(("character-reactions-title", navigation.Uri), js.Sections.Last()));
        Assert.Equal(3, js.Sections.Count);
        Assert.Equal("LUPP", page.Find("h1").TextContent);
    }

    [Fact]
    public async Task Arena_section_links_the_public_fighter_to_its_own_collection_entry()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        await context.Auth.LogoutLocalAsync();
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "raymond"));

        var arena = page.Find(".character-arena");
        Assert.Contains("Raymond", arena.QuerySelector("h2")!.TextContent);
        var links = arena.QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToArray();
        Assert.Contains(ArenaFighters.RosterPath, links);
        Assert.Contains(ArenaFighters.PlayPath, links);
        Assert.Contains(ArenaFighters.CollectionPath("raymond"), links);
        Assert.Equal(ArenaFighters.Requirements("raymond"), arena.QuerySelectorAll("li").Select(item => item.TextContent).ToArray());
        Assert.NotEmpty(arena.QuerySelectorAll("li"));

        page.Render(p => p.Add(x => x.Slug, "vatson"));
        Assert.Empty(page.FindAll(".character-arena"));
    }

    private sealed class GalleryScrollRuntime(IJSRuntime storage) : IJSRuntime
    {
        public ConcurrentQueue<(string Id, string Location)> Sections { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        {
            if (identifier != "lupCharacterUi.scrollToSection") return storage.InvokeAsync<TValue>(identifier, ct, args);
            Sections.Enqueue(((string)args![0]!, (string)args[1]!));
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
