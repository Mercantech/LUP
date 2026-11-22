using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class GeneratedCharacterArtworkTests
{
    [Fact]
    public async Task Library_shows_character_and_provenance_and_downloads_the_original_on_request()
    {
        using var api = new ArtworkApi();
        var js = new ArtworkJavaScript();
        using var context = await LearningUiTestContext.CreateAsync(api, configure: services => services.AddSingleton<IJSRuntime>(js));
        var gallery = context.Render<GeneratedCharacterArtwork>(p => p.Add(x => x.ShowEmpty, true));

        gallery.WaitForAssertion(() => Assert.Single(gallery.FindAll(".generated-art-preview img")));
        Assert.Contains("Raymond", gallery.Find(".generated-character-group h3").TextContent);
        Assert.Contains("Datahåndbogen", gallery.Find(".generated-art-details").TextContent);
        Assert.Contains("SQL og relationer", gallery.Find(".generated-art-details").TextContent);
        Assert.Equal(api.Cover.SourceHref, gallery.Find(".generated-art-actions a").GetAttribute("href"));
        Assert.DoesNotContain(api.Requests, path => path.EndsWith("/content", StringComparison.Ordinal));

        gallery.Find(".generated-art-actions button").Click();
        gallery.WaitForAssertion(() => Assert.Contains($"raymond-cover-{api.Cover.Id:D}.webp", js.Downloads));
        Assert.Contains(api.Cover.ImageUrl, api.Requests);

        gallery.FindAll("button").Single(button => button.TextContent.Trim() == "Vis flere billeder").Click();
        gallery.WaitForAssertion(() => Assert.Equal(2, gallery.FindAll(".generated-character-group").Count));
        Assert.Contains("AI-illustration", gallery.Markup);
        Assert.Contains("/api/character-artwork?offset=12", api.Requests);
    }

    [Fact]
    public async Task Switching_to_student_preview_or_logging_out_removes_the_old_account_artwork()
    {
        using var api = new ArtworkApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var gallery = context.Render<GeneratedCharacterArtwork>(p => p.Add(x => x.CharacterSlug, "raymond"));
        gallery.WaitForAssertion(() => Assert.Contains("Datahåndbogen", gallery.Markup));
        Assert.Contains("/api/character-artwork?offset=0&characterSlug=raymond", api.Requests);

        await gallery.InvokeAsync(() => context.ViewAs.Enter("qa-preview", "Test"));
        gallery.WaitForAssertion(() => Assert.Empty(gallery.FindAll(".generated-character-art")));
        await gallery.InvokeAsync(() => context.ViewAs.Exit());
        gallery.WaitForAssertion(() => Assert.Contains("Datahåndbogen", gallery.Markup));

        await context.Auth.LogoutLocalAsync();
        gallery.WaitForAssertion(() => Assert.Empty(gallery.FindAll(".generated-character-art")));
        var requestCount = api.Requests.Count;
        gallery.Render(p => p.Add(x => x.CharacterSlug, "lupp"));
        Assert.Equal(requestCount, api.Requests.Count);
    }

    private sealed class ArtworkApi : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();
        public CharacterGeneratedArtItem Cover { get; } = Item("raymond", "cover", "Datahåndbogen", "SQL og relationer");
        private CharacterGeneratedArtItem Illustration { get; } = Item("lupp", "image", "Spildesign", "Baner og mekanikker");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            Requests.Enqueue(uri.PathAndQuery);
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (uri.AbsolutePath.EndsWith("/content", StringComparison.Ordinal))
            {
                response.Content = new ByteArrayContent([1, 2, 3]);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
            }
            else if (uri.Query.Contains("offset=12", StringComparison.Ordinal))
                response.Content = JsonContent.Create(new CharacterGeneratedArtPage([Illustration], null));
            else
                response.Content = JsonContent.Create(new CharacterGeneratedArtPage([Cover], 12));
            return Task.FromResult(response);
        }

        private static CharacterGeneratedArtItem Item(string slug, string kind, string book, string page)
        {
            var (id, sourceId) = (Guid.NewGuid(), Guid.NewGuid());
            return new(id, slug, kind, "notion-page", sourceId, book, page, $"/notes/{sourceId:D}?published=true",
                DateTimeOffset.UtcNow, $"/api/character-artwork/{id:D}/content");
        }
    }

    private sealed class ArtworkJavaScript : IJSRuntime, IJSObjectReference
    {
        private readonly TestJavaScriptRuntime _auth = new(new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim("sub", LearningUiTestContext.AccountId.ToString())])));
        private int _nextUrl;
        public ConcurrentQueue<string> Downloads { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        {
            if (identifier == "import") return ValueTask.FromResult((TValue)(object)this);
            if (identifier == "createCoverUrl") return ValueTask.FromResult((TValue)(object)$"blob:artwork-{++_nextUrl}");
            if (identifier == "downloadCover") Downloads.Enqueue((string)args![1]!);
            return _auth.InvokeAsync<TValue>(identifier, ct, args);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
