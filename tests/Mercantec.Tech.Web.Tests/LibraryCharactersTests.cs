using Bunit;
using Mercantec.Tech.Web.Characters;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Public;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using CharacterDetail = Mercantec.Tech.Web.Pages.Characters.Detail;
using CharacterIndex = Mercantec.Tech.Web.Pages.Characters.Index;
using LibraryPage = Mercantec.Tech.Web.Pages.Library;

namespace Mercantec.Tech.Web.Tests;

public sealed class LibraryCharactersTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Character_category_uses_public_profile_portraits_and_stable_links(bool authenticated)
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateContext(api, authenticated);
        var page = RenderCharacters(context);

        Assert.Contains(page.FindAll(".library-categories button[aria-pressed='true']"),
            button => button.TextContent.Contains("Karakterer", StringComparison.Ordinal));
        Assert.Equal(CharacterCatalog.All.Count, page.FindAll("article.character-card").Count);
        foreach (var character in CharacterCatalog.All)
        {
            var card = page.Find($".character-card.character-{character.Slug}");
            Assert.Equal(character.Path, card.QuerySelector("a.character-card-profile")!.GetAttribute("href"));
            Assert.Equal(character.Name, card.QuerySelector("h3")!.TextContent);
            Assert.Contains(character.Role, card.TextContent);
            Assert.Equal(character.DisplayPortrait!.Image, card.QuerySelector(".character-art img")!.GetAttribute("src"));
            Assert.False(string.IsNullOrWhiteSpace(card.QuerySelector(".character-art img")!.GetAttribute("alt")));
        }
        Assert.Empty(page.FindAll(".character-card a a"));
        Assert.Equal(authenticated, page.FindAll(".library-examples").Count == 1);
    }

    [Fact]
    public async Task Material_shortcuts_show_available_media_and_land_on_the_profile_galleries()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateContext(api);
        var page = RenderCharacters(context);
        var expected = new[]
        {
            ("vatson", "character-reactions-title", "63 emojis"),
            ("vatson", "character-scenes-title", "2 miljøer"),
            ("raymond", "character-reactions-title", "48 emojis"),
            ("beaver", "character-reactions-title", "12 emojis"),
            ("beaver", "character-scenes-title", "2 miljøer"),
            ("fox", "character-reactions-title", "12 emojis"),
            ("fox", "character-scenes-title", "2 miljøer"),
            ("lupp", "character-reactions-title", "48 emojis"),
            ("lupp", "character-scenes-title", "2 miljøer"),
            ("raymond", "character-scenes-title", "6 miljøer"),
            ("raymond", "character-animations-title", "3 GIF-animationer"),
            ("raymond", "character-gallery-title", "16 infografikker"),
            ("lupp", "character-gallery-title", "24 illustrationer"),
            ("chameleon", "character-reactions-title", "48 emojis"),
            ("chameleon", "character-scenes-title", "6 miljøer"),
            ("chameleon", "character-animations-title", "3 GIF-animationer"),
            ("chameleon", "character-gallery-title", "16 infografikker"),
            ("whale", "character-reactions-title", "52 emojis"),
            ("whale", "character-scenes-title", "6 miljøer"),
            ("whale", "character-animations-title", "3 GIF-animationer"),
            ("whale", "character-gallery-title", "16 infografikker"),
            ("gummianden", "character-reactions-title", "48 emojis"),
            ("gummianden", "character-scenes-title", "6 miljøer"),
            ("gummianden", "character-animations-title", "3 GIF-animationer"),
            ("gummianden", "character-gallery-title", "16 infografikker"),
            ("bee", "character-reactions-title", "48 emojis"),
            ("bee", "character-scenes-title", "6 miljøer"),
            ("bee", "character-animations-title", "3 GIF-animationer"),
            ("bee", "character-gallery-title", "24 infografikker"),
            ("bat", "character-reactions-title", "48 emojis"),
            ("bat", "character-scenes-title", "6 miljøer"),
            ("bat", "character-animations-title", "3 GIF-animationer"),
            ("bat", "character-gallery-title", "16 infografikker"),
            ("bilbo", "character-reactions-title", "48 emojis"),
            ("bilbo", "character-scenes-title", "6 miljøer"),
            ("bilbo", "character-animations-title", "3 GIF-animationer"),
            ("bilbo", "character-gallery-title", "16 infografikker"),
            ("gitmejsen", "character-reactions-title", "30 emojis"),
            ("gitmejsen", "character-scenes-title", "14 miljøer"),
            ("gitmejsen", "character-animations-title", "3 GIF-animationer"),
            ("gitmejsen", "character-gallery-title", "11 øvelser")
        };
        foreach (var (slug, section, label) in expected)
        {
            var link = page.Find($".character-card-materials a[href='/characters/{slug}#{section}']");
            Assert.Contains(label, link.TextContent);
            Assert.StartsWith($"/images/characters/{slug}/", link.QuerySelector("img")!.GetAttribute("src"));
            var profile = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, slug));
            Assert.Single(profile.FindAll("#" + section));
        }
        Assert.Equal(2, page.FindAll(".character-beaver .character-card-materials, .character-fox .character-card-materials").Count);
        Assert.DoesNotContain(page.FindAll(".character-card-materials img"),
            image => image.GetAttribute("src")!.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Fancy Friday", "fancy-friday")]
    [InlineData("Hawaii onsdag", "hawaii-onsdag")]
    public void Both_outfit_themes_are_searchable_for_every_character(string query, string file)
    {
        var results = PublicCatalog.Search(query).Where(hit => hit.Kind == "Karakter").ToArray();

        Assert.Equal(CharacterCatalog.All.Count, results.Length);
        foreach (var character in CharacterCatalog.All)
        {
            Assert.Contains(results, hit => hit.Href == character.Path);
            var path = $"/images/characters/{character.Slug}/scenes/{file}";
            var scene = Assert.Single(character.Scenes, item => item.Image == path + ".png");
            Assert.Equal(path + ".webp", scene.Preview);
            Assert.False(string.IsNullOrWhiteSpace(scene.Alt));
        }
    }

    [Fact]
    public async Task Search_keeps_character_portraits_and_clear_returns_to_the_selected_category()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateContext(api);
        var page = RenderCharacters(context);

        page.Find("#library-search").Input("emojis");
        var results = page.FindAll(".library-search-character");
        Assert.Equal(CharacterCatalog.All.Count, results.Count);
        Assert.All(results, result =>
        {
            var path = result.GetAttribute("href");
            var character = Assert.Single(CharacterCatalog.All, character => character.Path == path);
            Assert.Equal(character.DisplayPortrait!.Image, result.QuerySelector("img")!.GetAttribute("src"));
        });
        page.FindAll(".library-categories button").Single(button => button.TextContent.Contains("Karakterer", StringComparison.Ordinal)).Click();
        Assert.Equal(CharacterCatalog.All.Count, page.FindAll(".library-search-character").Count);
        Assert.Contains("i Karakterer", page.Find(".library-section-heading [role='status']").TextContent);

        page.Find(".library-search button").Click();

        Assert.Equal("", page.Find("#library-search").GetAttribute("value"));
        Assert.Empty(page.FindAll(".library-search-results"));
        Assert.Equal(CharacterCatalog.All.Count, page.FindAll(".character-card").Count);
        Assert.Contains(page.FindAll(".library-categories button[aria-pressed='true']"),
            button => button.TextContent.Contains("Karakterer", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("emojis", "/characters/vatson")]
    [InlineData("emojis", "/characters/raymond")]
    [InlineData("emojis", "/characters/beaver")]
    [InlineData("emojis", "/characters/fox")]
    [InlineData("emojis", "/characters/lupp")]
    [InlineData("LUPP playtest", "/characters/lupp")]
    [InlineData("GIF", "/characters/raymond")]
    [InlineData("illustrationer", "/characters/lupp")]
    [InlineData("infografikker", "/characters/raymond")]
    [InlineData("Raymond normalisering", "/characters/raymond")]
    [InlineData("Raymond ORM", "/characters/raymond")]
    [InlineData("Raymond SQL injection", "/characters/raymond")]
    [InlineData("Raymond kaffepause", "/characters/raymond")]
    [InlineData("Vatson PLC", "/characters/vatson")]
    [InlineData("Docker rollback", "/characters/whale")]
    [InlineData("Docker Kanarien", "/characters/whale")]
    [InlineData("Gummianden fejlfinding", "/characters/gummianden")]
    [InlineData("Gummianden breakpoint", "/characters/gummianden")]
    [InlineData("Cyberbien honeypot", "/characters/bee")]
    [InlineData("Cyberbien phishing", "/characters/bee")]
    [InlineData("Næsebladet ekko", "/characters/bat")]
    [InlineData("Næsebladet MQTT", "/characters/bat")]
    [InlineData("Bilbo Backend EF Core", "/characters/bilbo")]
    [InlineData("Bilbo Backend idempotens", "/characters/bilbo")]
    [InlineData("Gitmejsen mergekonflikt", "/characters/gitmejsen")]
    [InlineData("topmejse versionsstyring", "/characters/gitmejsen")]
    [InlineData("Gitmejsen bisect", "/characters/gitmejsen")]
    [InlineData("Mumihuset", "/characters/gitmejsen")]
    public void Public_search_finds_media_types_and_named_motifs(string query, string expectedPath)
    {
        var hit = Assert.Single(PublicCatalog.Search(query), hit => hit.Href == expectedPath);
        Assert.Equal("Karakter", hit.Kind);
        Assert.True(Mercantec.Tech.Web.Auth.PublicPageAccess.Allows(hit.Href));
    }

    [Theory]
    [InlineData("bilbo-raymond", "Bilbo Raymond EF Core", "bilbo", "raymond")]
    [InlineData("gitmejsen-bilbo", "Gitmejsen Bilbo mergekonflikt", "gitmejsen", "bilbo")]
    [InlineData("raymond-vatson", "Raymond Vatson nattoget UTC", "raymond", "vatson")]
    [InlineData("raymond-bilbo-left-join", "Raymond Bilbo LEFT JOIN", "raymond", "bilbo")]
    public async Task Collaboration_uses_one_image_and_links_to_the_partner_on_both_profiles(
        string id, string query, string first, string second)
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateContext(api);
        var library = RenderCharacters(context);
        var sharedImage = $"/images/characters/collaborations/{id}.png";
        var sceneId = $"collaboration-{id}";
        var hits = PublicCatalog.Search(query).Where(hit => hit.Kind == "Karakter").ToArray();

        foreach (var (slug, partner) in new[] { (first, second), (second, first) })
        {
            var path = $"/characters/{slug}";
            Assert.Contains(hits, hit => hit.Href == path);
            Assert.Single(library.FindAll($".character-card-materials a[href='{path}#character-collaborations-title']"));

            var profile = context.Render<CharacterDetail>(parameters => parameters.Add(page => page.Slug, slug));
            var scene = profile.Find("#" + sceneId);
            Assert.Equal(sharedImage, scene.QuerySelector("a[download]")!.GetAttribute("href"));
            Assert.Equal($"{id}.png", scene.QuerySelector("a[download]")!.GetAttribute("download"));
            Assert.NotNull(scene.QuerySelector($"a[href='/characters/{partner}#{sceneId}']"));
        }
    }

    [Theory]
    [InlineData("raymond", "raymond-bilbo-left-join", "COUNT(*)", "Alma får 2", "WITH customers")]
    [InlineData("gummianden", "naesebladet-gummianden-timeout", "Intet ekko", "18 cm", "INTET_EKKO")]
    [InlineData("fox", "kamaeleonen-raev-confusion", "2 × 2-matrix", "80 %", null)]
    [InlineData("gitmejsen", "docker-gitmejsen-ci", "assertion", "stopper kommandoen", "dotnet test && docker build")]
    public async Task Collaboration_exercises_show_the_task_and_allow_revealing_the_result(
        string slug, string sceneId, string task, string result, string? starter)
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateContext(api);
        var profile = context.Render<CharacterDetail>(parameters => parameters.Add(page => page.Slug, slug));
        var exercise = profile.Find($"#collaboration-{sceneId} .character-collaboration-exercise");
        var details = exercise.QuerySelector("details")!;

        Assert.Contains(task, exercise.TextContent);
        Assert.Equal("Vis forventet resultat", details.QuerySelector("summary")!.TextContent);
        Assert.Contains(result, details.TextContent);
        Assert.False(details.HasAttribute("open"));
        var code = exercise.QuerySelector("pre code");
        if (starter is null)
            Assert.Null(code);
        else
            Assert.Contains(starter, code!.TextContent);
    }

    [Fact]
    public async Task Each_character_has_two_public_home_views_and_a_library_link()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateContext(api);
        var library = RenderCharacters(context);

        foreach (var character in CharacterCatalog.All)
        {
            var home = Assert.IsType<CharacterHome>(character.Residence);
            Assert.Equal(2, home.Views.Count);
            Assert.False(string.IsNullOrWhiteSpace(home.Description));
            var href = $"{character.Path}#{CharacterHome.SectionId}";
            var link = Assert.Single(library.FindAll($".character-card-materials a[href='{href}']"));
            Assert.Contains("2 billeder af hjemmet", link.TextContent);
            Assert.Contains(PublicCatalog.Search(home.Name), result => result.Href == character.Path);

            var profile = context.Render<CharacterDetail>(p => p.Add(page => page.Slug, character.Slug));
            var section = profile.Find(".character-residence");
            Assert.Equal(home.Name, section.QuerySelector($"#{CharacterHome.SectionId}")!.TextContent);
            var figures = section.QuerySelectorAll("figure");
            Assert.Equal(2, figures.Length);

            for (var index = 0; index < home.Views.Count; index++)
            {
                var view = home.Views[index];
                var image = figures[index].QuerySelector("img")!;
                var download = figures[index].QuerySelector("a[download]")!;
                Assert.Equal(view.Preview, image.GetAttribute("src"));
                Assert.Equal(view.Alt, image.GetAttribute("alt"));
                Assert.Equal(view.Image, download.GetAttribute("href"));
            }
        }
    }

    [Fact]
    public async Task Character_overview_and_each_profile_link_back_to_the_library_category()
    {
        using var api = new CharacterQuizTestApi();
        using var context = await CreateContext(api);
        var overview = context.Render<CharacterIndex>();
        Assert.Contains(overview.FindAll("a"), link => link.GetAttribute("href") == "/library?category=characters" && link.TextContent.Contains("Oversigt", StringComparison.Ordinal));
        foreach (var character in CharacterCatalog.All)
        {
            var profile = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, character.Slug));
            Assert.Contains(profile.FindAll("a"), link => link.GetAttribute("href") == "/library?category=characters" && link.TextContent.Contains("Oversigt", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("raymond", "character-home-title")]
    [InlineData("raymond", "character-animations-title")]
    [InlineData("raymond", "animation-find-row")]
    [InlineData("raymond", "animation-sort-rows")]
    [InlineData("raymond", "character-gallery-title")]
    [InlineData("vatson", "character-reactions-title")]
    [InlineData("beaver", "character-reactions-title")]
    [InlineData("fox", "character-reactions-title")]
    [InlineData("lupp", "character-reactions-title")]
    [InlineData("lupp", "character-gallery-title")]
    [InlineData("bee", "character-gallery-title")]
    [InlineData("bilbo", "collaboration-bilbo-raymond")]
    [InlineData("raymond", "character-collaborations-title")]
    public async Task Gallery_deep_links_scroll_after_render_without_repeating_on_profile_interactions(string slug, string section)
    {
        using var api = new CharacterQuizTestApi();
        var javascript = new ScrollJavascript();
        using var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<IJSRuntime>(javascript));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/characters/{slug}#{section}");
        var location = navigation.Uri;

        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, slug));

        Assert.Equal(new[] { (section, location) }, javascript.Sections);
        Assert.Single(page.FindAll("#" + section));
        if (page.FindAll(".character-appearance-picker button") is { Count: > 1 } appearances)
            appearances[1].Click();
        page.Render(p => p.Add(x => x.Slug, slug));
        Assert.Equal(new[] { (section, location) }, javascript.Sections);
    }

    [Fact]
    public async Task A_new_gallery_location_scrolls_once_to_the_new_target()
    {
        using var api = new CharacterQuizTestApi();
        var javascript = new ScrollJavascript();
        using var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<IJSRuntime>(javascript));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/characters/raymond#character-animations-title");
        var animationLocation = navigation.Uri;
        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, "raymond"));

        await page.InvokeAsync(() => navigation.NavigateTo("/characters/raymond#character-reactions-title"));
        var reactionLocation = navigation.Uri;
        page.Render(p => p.Add(x => x.Slug, "raymond"));
        page.Render(p => p.Add(x => x.Slug, "raymond"));

        Assert.Equal(new[]
        {
            ("character-animations-title", animationLocation),
            ("character-reactions-title", reactionLocation)
        }, javascript.Sections);
    }

    [Theory]
    [InlineData("raymond", "#not-a-gallery")]
    [InlineData("beaver", "#character-animations-title")]
    [InlineData("beaver", "#animation-find-row")]
    [InlineData("lupp", "#character-animations-title")]
    [InlineData("missing", "#character-animations-title")]
    [InlineData("raymond", "")]
    public async Task Unknown_or_unavailable_gallery_targets_do_not_scroll(string slug, string fragment)
    {
        using var api = new CharacterQuizTestApi();
        var javascript = new ScrollJavascript();
        using var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<IJSRuntime>(javascript));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/characters/{slug}{fragment}");

        var page = context.Render<CharacterDetail>(p => p.Add(x => x.Slug, slug));
        page.Render(p => p.Add(x => x.Slug, slug));

        Assert.Empty(javascript.Sections);
    }

    private sealed class ScrollJavascript : IJSRuntime
    {
        public List<(string Section, string Location)> Sections { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identifier == "lupCharacterUi.scrollToSection")
            {
                Assert.NotNull(args);
                Assert.Equal(2, args.Length);
                Sections.Add((Assert.IsType<string>(args[0]), Assert.IsType<string>(args[1])));
            }
            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private static async Task<LearningUiTestContext> CreateContext(CharacterQuizTestApi api, bool authenticated = false)
    {
        var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<LearningContext>());
        if (!authenticated) await context.Auth.LogoutLocalAsync();
        return context;
    }

    private static IRenderedComponent<LibraryPage> RenderCharacters(LearningUiTestContext context)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/library?category=characters");
        return context.Render<LibraryPage>();
    }
}
