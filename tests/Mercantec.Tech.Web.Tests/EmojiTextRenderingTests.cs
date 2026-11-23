using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Emojis;
using Mercantec.Tech.Web.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class EmojiTextRenderingTests
{
    private static readonly CustomEmojiItem Raymond = new(Guid.NewGuid(), "raymond_applause", "image/png", 12,
        DateTimeOffset.UtcNow, "/api/emojis/raymond", AltText: "Raymond klapper");

    [Fact]
    public async Task Saved_notebook_favorite_renders_the_emoji_in_its_title()
    {
        using var api = new EmojiApi
        {
            Favorites = new FavoriteShortcutsDto(Guid.NewGuid(),
                [new("/notes/blazor", "Blazor :raymond_applause:", "", "book")])
        };
        using var context = await LearningUiTestContext.CreateAsync(api, teacher: true,
            configure: services => services.AddSingleton<FavoriteShortcutsState>());
        var favorites = context.Render<FavoriteShortcutsPanel>();

        favorites.WaitForAssertion(() =>
        {
            var title = favorites.Find("a[href='/notes/blazor'] h3");
            Assert.Equal("Blazor", title.TextContent.Trim());
            var emoji = title.QuerySelector("img.custom-emoji");
            Assert.NotNull(emoji);
            Assert.Equal(Raymond.ContentUrl, emoji.GetAttribute("src"));
            Assert.Equal(Raymond.AltText, emoji.GetAttribute("alt"));
        });
    }

    [Fact]
    public async Task Returning_to_a_loaded_title_during_another_pending_load_restores_the_correct_label()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        const string original = "Titel A :raymond_applause:";
        var title = context.Render<EmojiText>(p => p.Add(x => x.Text, original));
        title.WaitForAssertion(() => Assert.Single(title.FindAll("img")));

        context.Services.GetRequiredService<EmojiClient>().Invalidate();
        api.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            title.Render(p => p.Add(x => x.Text, "Titel B :raymond_applause:"));
            title.WaitForAssertion(() => Assert.Equal(2, api.Requests));
            title.Render(p => p.Add(x => x.Text, original));
            Assert.Equal(original, title.Find(".emoji-text").TextContent);
        }
        finally { api.Gate.TrySetResult(); }
        title.WaitForAssertion(() =>
        {
            Assert.Single(title.FindAll("img"));
            Assert.Equal("Titel A ", title.Find(".emoji-text").TextContent);
        });
        Assert.Equal(2, api.Requests);
    }

    [Fact]
    public async Task A_late_catalog_response_cannot_replace_a_newer_title()
    {
        using var api = new EmojiApi { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var context = await LearningUiTestContext.CreateAsync(api);
        var title = context.Render<EmojiText>(p => p.Add(x => x.Text, "Gammel :raymond_applause:"));
        await api.Requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Gammel :raymond_applause:", title.Find(".emoji-text").TextContent);

        title.Render(p => p.Add(x => x.Text, "Ny titel 🐘"));
        Assert.Equal("Ny titel 🐘", title.Find(".emoji-text").TextContent);
        var rendersBeforeCatalog = title.RenderCount;
        api.Gate.SetResult();

        await api.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        title.WaitForAssertion(() =>
        {
            Assert.True(title.RenderCount > rendersBeforeCatalog);
            Assert.Equal("Ny titel 🐘", title.Find(".emoji-text").TextContent);
        });
        Assert.Empty(title.FindAll("img"));
        Assert.Equal(1, api.Requests);
    }

    [Fact]
    public async Task Titles_render_known_emojis_and_preserve_unicode_unknown_codes_and_literal_text()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var title = context.Render<EmojiText>(p => p.Add(x => x.Text, "SQL 🐘 :raymond_applause: :ukendt: <b>tekst</b>"));

        title.WaitForAssertion(() => Assert.Equal(Raymond.ContentUrl, title.Find("img").GetAttribute("src")));
        var label = title.Find(".emoji-text");
        Assert.Contains("SQL 🐘", label.TextContent);
        Assert.Contains(":ukendt:", label.TextContent);
        Assert.Contains("<b>tekst</b>", label.TextContent);
        Assert.Empty(label.QuerySelectorAll("b"));
    }

    private sealed class EmojiApi : HttpMessageHandler
    {
        public FavoriteShortcutsDto? Favorites { get; init; }
        public TaskCompletionSource? Gate { get; set; }
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/api/me/favorite-shortcuts" && Favorites is { } favorites)
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(favorites) };

            if (request.RequestUri!.AbsolutePath == "/api/emojis")
            {
                Interlocked.Increment(ref Requests); Requested.TrySetResult();
                if (Gate is not null) await Gate.Task.WaitAsync(ct);
                Completed.TrySetResult();
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Raymond }) };
            }
            return new(HttpStatusCode.NotFound);
        }
    }
}
