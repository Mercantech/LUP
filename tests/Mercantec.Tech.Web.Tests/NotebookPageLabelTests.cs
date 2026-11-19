using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Emojis;
using ErrorEventArgs = Microsoft.AspNetCore.Components.Web.ErrorEventArgs;

namespace Mercantec.Tech.Web.Tests;

public sealed class NotebookPageLabelTests
{
    private static readonly CustomEmojiItem Aspire = new(Guid.NewGuid(), "aspire-dotnet", "image/png", 75,
        DateTimeOffset.UtcNow, "/api/emojis/aspire/content?ticket=first", AltText: "Aspire");
    private static readonly CustomEmojiItem Docker = new(Guid.NewGuid(), "whale", "image/webp", 100,
        DateTimeOffset.UtcNow, "/images/characters/whale/reactions/dockerfile.webp", AltText: "Docker");

    [Fact]
    public async Task Legacy_edge_icons_move_to_the_icon_slot_while_inline_emoji_and_details_stay_in_the_label()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<NotebookPageLabel>(p => p
            .Add(x => x.Title, ":Aspire-Dotnet: Deploy :whale: nu :ukendt: :aspire-dotnet:")
            .Add(x => x.HasChildren, true).AddChildContent("<small>Overordnet side</small>"));

        page.WaitForAssertion(() => Assert.Equal(Aspire.ContentUrl, page.Find(".nb-page-icon img").GetAttribute("src")));
        Assert.Equal(Docker.ContentUrl, page.Find(".notion-page-label img").GetAttribute("src"));
        Assert.Contains("nu :ukendt:", page.Find(".notion-page-label").TextContent);
        Assert.Equal("Overordnet side", page.Find("small").TextContent);
        Assert.Empty(page.FindAll(".nb-page-icon svg"));
        Assert.Equal(1, api.Requests);
    }

    [Fact]
    public async Task Explicit_icon_takes_precedence_and_a_missing_icon_falls_back_to_the_title()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<NotebookPageLabel>(p => p.Add(x => x.Title, "Kør :whale:").Add(x => x.IconEmojiId, Aspire.Id));
        page.WaitForAssertion(() => Assert.Equal(Aspire.ContentUrl, page.Find(".nb-page-icon img").GetAttribute("src")));
        Assert.Equal("Docker", page.Find(".notion-page-label img").GetAttribute("alt"));

        page.Render(p => p.Add(x => x.Title, ":whale:").Add(x => x.IconEmojiId, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Equal(Docker.ContentUrl, page.Find(".nb-page-icon img").GetAttribute("src")));
        Assert.Equal("Docker", page.Find(".notion-page-label").TextContent.Trim());
        Assert.Equal(1, api.Requests);
    }

    [Fact]
    public async Task Choosing_another_icon_removes_a_legacy_frame_but_keeps_other_meaningful_inline_emoji()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<NotebookPageLabel>(p => p
            .Add(x => x.Title, ":aspire-dotnet: Kør :aspire-dotnet: i dag :aspire-dotnet:").Add(x => x.IconEmojiId, Docker.Id));
        page.WaitForAssertion(() => Assert.Equal(Docker.ContentUrl, page.Find(".nb-page-icon img").GetAttribute("src")));
        Assert.Equal(Aspire.ContentUrl, page.Find(".notion-page-label img").GetAttribute("src"));
        Assert.Equal("Kør  i dag", page.Find(".notion-page-label").TextContent.Trim());
        page.Render(p => p.Add(x => x.Title, ":aspire-dotnet: :aspire-dotnet:").Add(x => x.IconEmojiId, Docker.Id));
        Assert.Equal("Aspire", page.Find(".notion-page-label").TextContent.Trim());
    }

    [Fact]
    public async Task The_promoted_inline_shortcode_occupies_only_the_icon_slot()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<NotebookPageLabel>(p => p.Add(x => x.Title, "Kør :aspire-dotnet: via :whale: nu"));
        page.WaitForAssertion(() => Assert.Equal(Aspire.ContentUrl, page.Find(".nb-page-icon img").GetAttribute("src")));
        Assert.Equal(Docker.ContentUrl, page.Find(".notion-page-label img").GetAttribute("src"));
        Assert.Equal("Kør via  nu", page.Find(".notion-page-label").TextContent.Trim());
    }

    [Theory]
    [InlineData("🐳 Deployment 🐳", "🐳", "Deployment")]
    [InlineData("Om 👩🏽‍💻", "👩🏽‍💻", "Om")]
    [InlineData("👧 Gruppe 👨‍👩‍👧", "👧", "Gruppe 👨‍👩‍👧")]
    [InlineData("🇩🇰 Skole", "🇩🇰", "Skole")]
    [InlineData("⚙️", "⚙️", "⚙️")]
    [InlineData("2. uge", null, "2. uge")]
    [InlineData("Årsager: 5", null, "Årsager: 5")]
    public async Task Native_graphemes_and_fallback_icons_work_without_fetching_the_catalog(string title, string? glyph, string label)
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<NotebookPageLabel>(p => p.Add(x => x.Title, title));
        Assert.Equal(label, page.Find(".notion-page-label").TextContent.Trim());
        if (glyph is null)
        {
            var file = page.Find(".nb-page-icon path").GetAttribute("d");
            page.Render(p => p.Add(x => x.Title, title).Add(x => x.HasChildren, true));
            Assert.NotEqual(file, page.Find(".nb-page-icon path").GetAttribute("d"));
        }
        else
        {
            Assert.Equal(glyph, page.Find(".nb-page-icon").TextContent.Trim());
            Assert.Empty(page.FindAll(".nb-page-icon svg"));
        }
        Assert.Equal(0, api.Requests);
    }

    [Fact]
    public async Task A_failed_image_gets_one_fresh_ticket_then_uses_the_next_available_icon()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<NotebookPageLabel>(p => p.Add(x => x.Title, "🐳 Drift").Add(x => x.IconEmojiId, Aspire.Id));
        page.WaitForAssertion(() => Assert.Equal(Aspire.ContentUrl, page.Find("img").GetAttribute("src")));
        var fresh = Aspire with { ContentUrl = "/api/emojis/aspire/content?ticket=fresh" };
        api.Items = [fresh, Docker];
        await page.Find("img").TriggerEventAsync("onerror", new ErrorEventArgs());
        Assert.Equal(fresh.ContentUrl, page.Find("img").GetAttribute("src"));
        await page.Find("img").TriggerEventAsync("onerror", new ErrorEventArgs());
        Assert.Equal("🐳", page.Find(".nb-page-icon").TextContent.Trim());
        Assert.Equal("Drift", page.Find(".notion-page-label").TextContent.Trim());
        Assert.Equal(2, api.Requests);
    }

    private sealed class EmojiApi : HttpMessageHandler
    {
        public CustomEmojiItem[] Items = [Aspire, Docker];
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("/api/emojis", request.RequestUri!.AbsolutePath);
            Interlocked.Increment(ref Requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Items) });
        }
    }
}
