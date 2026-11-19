using System.Net;
using System.Net.Http.Json;
using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Students;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class ProfilePhotoTests
{
    [Fact]
    public async Task MissingPhotoUsesNamedInitialsWithoutFetching()
    {
        using var api = new PhotoApi();
        using var context = await Context(api);
        var photo = context.Render<StudentPhoto>(p => p.Add(x => x.UserSub, Guid.NewGuid())
            .Add(x => x.DisplayName, "Anna Andersen").Add(x => x.HasCustomPhoto, false));
        Assert.Equal("Anna Andersen", photo.Find(".student-photo").GetAttribute("aria-label"));
        Assert.Contains("AA", photo.Markup);
        Assert.Empty(photo.FindAll("img"));
        Assert.Equal(0, api.Reads);
    }

    [Fact]
    public async Task PhotoFetchesAuthenticatedBytesAndClearsOnLogout()
    {
        using var api = new PhotoApi();
        using var context = await Context(api);
        var photo = context.Render<StudentPhoto>(p => p.Add(x => x.UserSub, LearningUiTestContext.AccountId)
            .Add(x => x.DisplayName, "Anna Andersen"));
        photo.WaitForAssertion(() => Assert.StartsWith("data:image/jpeg;base64,", photo.Find("img").GetAttribute("src")));
        await context.Auth.LogoutLocalAsync();
        photo.WaitForAssertion(() => Assert.Empty(photo.FindAll("img")));
        Assert.Equal(1, api.Reads);
    }

    [Fact]
    public async Task FailedPhotoRequestFallsBackToInitials()
    {
        using var api = new PhotoApi { FailPhoto = true };
        using var context = await Context(api);
        var photo = context.Render<StudentPhoto>(p => p.Add(x => x.UserSub, Guid.NewGuid())
            .Add(x => x.DisplayName, "Søren Olsen"));
        photo.WaitForAssertion(() => Assert.Equal(1, api.Reads));
        Assert.Empty(photo.FindAll("img"));
        Assert.Contains("SO", photo.Markup);
    }

    [Fact]
    public async Task MetadataFailureHasAnAccessibleRetryAndNonJsonFailureStaysFriendly()
    {
        using var api = new PhotoApi { FailMetadata = true };
        using var context = await Context(api);
        var editor = context.Render<ProfilePhotoEditor>();
        editor.WaitForAssertion(() => Assert.Contains("Kunne ikke hente", editor.Find("[role='alert']").TextContent));
        api.FailMetadata = false;
        editor.Find("button").Click();
        editor.WaitForAssertion(() => Assert.Single(editor.FindAll("input[type='file']")));
        Assert.Empty(editor.FindAll("[role='alert']"));
        Assert.Equal(2, api.MetadataReads);
    }

    private static Task<LearningUiTestContext> Context(PhotoApi api) => LearningUiTestContext.CreateAsync(api,
        configure: services => services.AddScoped<ProfilePhotoClient>());

    private sealed class PhotoApi : HttpMessageHandler
    {
        public bool FailPhoto { get; set; }
        public bool FailMetadata { get; set; }
        public int Reads { get; private set; }
        public int MetadataReads { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/me/profile-photo")
            {
                MetadataReads++;
                return Task.FromResult(FailMetadata ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                    { Content = new StringContent("<html>unavailable</html>") }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ProfilePhotoView(false, null)) });
            }
            if (request.RequestUri.AbsolutePath.StartsWith("/api/student-photos/"))
            {
                Reads++;
                var response = new HttpResponseMessage(FailPhoto ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
                    { Content = new ByteArrayContent([1, 2, 3]) };
                response.Content.Headers.ContentType = new("image/jpeg");
                return Task.FromResult(response);
            }
            throw new InvalidOperationException($"Unexpected request {request.RequestUri}");
        }
    }
}
