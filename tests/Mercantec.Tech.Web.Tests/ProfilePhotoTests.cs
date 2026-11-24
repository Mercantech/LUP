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
    public async Task UnregisteredStudentCanShowLectioPhotoWithoutLocalAccount()
    {
        using var api = new PhotoApi();
        using var context = await Context(api);
        var photo = context.Render<StudentPhoto>(p => p.Add(x => x.LectioId, "123456")
            .Add(x => x.DisplayName, "Anna Andersen").Add(x => x.HasCustomPhoto, false));
        photo.WaitForAssertion(() => Assert.Equal("data:image/jpeg;base64,CQgH", photo.Find("img").GetAttribute("src")));
        Assert.Equal(0, api.Reads);
        Assert.Equal(1, api.LectioReads);
    }

    [Fact]
    public async Task CustomPhotoTakesPriorityOverLectio()
    {
        using var api = new PhotoApi();
        using var context = await Context(api);
        var photo = context.Render<StudentPhoto>(p => p.Add(x => x.UserSub, Guid.NewGuid()).Add(x => x.LectioId, "123456")
            .Add(x => x.DisplayName, "Anna Andersen").Add(x => x.HasCustomPhoto, true));
        photo.WaitForAssertion(() => Assert.Equal("data:image/jpeg;base64,AQID", photo.Find("img").GetAttribute("src")));
        Assert.Equal(1, api.Reads);
        Assert.Equal(0, api.LectioReads);
    }

    [Fact]
    public async Task MissingCustomPhotoFallsBackToLectioAndMissingLectioUsesInitials()
    {
        using var api = new PhotoApi { CustomStatus = HttpStatusCode.NotFound };
        using var context = await Context(api);
        var photo = context.Render<StudentPhoto>(p => p.Add(x => x.UserSub, Guid.NewGuid()).Add(x => x.LectioId, "123456")
            .Add(x => x.DisplayName, "Anna Andersen").Add(x => x.HasCustomPhoto, true));
        photo.WaitForAssertion(() => Assert.Equal("data:image/jpeg;base64,CQgH", photo.Find("img").GetAttribute("src")));
        Assert.Equal(1, api.Reads);
        Assert.Equal(1, api.LectioReads);
        api.LectioStatus = HttpStatusCode.NotFound;
        var unavailable = context.Render<StudentPhoto>(p => p.Add(x => x.LectioId, "999999")
            .Add(x => x.DisplayName, "Bo Bak").Add(x => x.HasCustomPhoto, false));
        unavailable.WaitForAssertion(() => Assert.Equal(2, api.LectioReads));
        Assert.Empty(unavailable.FindAll("img"));
        Assert.Contains("BB", unavailable.Markup);
    }

    [Fact]
    public async Task RemovingOwnPhotoReturnsToLinkedLectioPhotoAndPinsTheAccount()
    {
        using var api = new PhotoApi { HasCustomPhoto = true, OwnLectioId = "123456" };
        using var context = await Context(api);
        var editor = context.Render<ProfilePhotoEditor>();
        editor.WaitForAssertion(() => Assert.Equal("data:image/jpeg;base64,AQID", editor.Find("img").GetAttribute("src")));
        Assert.Contains("Et billede, du selv uploader, får forrang", editor.Markup);
        editor.Find("button").Click();
        editor.WaitForAssertion(() => Assert.Equal("data:image/jpeg;base64,CQgH", editor.Find("img").GetAttribute("src")));
        Assert.Equal(LearningUiTestContext.AccountId.ToString(), api.DeleteSubject);
        Assert.Equal(1, api.LectioReads);
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
        public bool HasCustomPhoto { get; set; }
        public string? OwnLectioId { get; set; }
        public HttpStatusCode CustomStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode LectioStatus { get; set; } = HttpStatusCode.OK;
        public int Reads { get; private set; }
        public int LectioReads { get; private set; }
        public int MetadataReads { get; private set; }
        public string? DeleteSubject { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/me/profile-photo")
            {
                if (request.Method == HttpMethod.Delete)
                {
                    DeleteSubject = Assert.Single(request.Headers.GetValues(ProfileNameRules.SubjectHeader));
                    HasCustomPhoto = false;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ProfilePhotoView(false, null)) });
                }
                MetadataReads++;
                return Task.FromResult(FailMetadata ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                    { Content = new StringContent("<html>unavailable</html>") }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ProfilePhotoView(HasCustomPhoto, null)) });
            }
            if (request.RequestUri.AbsolutePath == "/api/lectio/me")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(new { lectio = OwnLectioId is null ? null : new { lectioId = OwnLectioId } }) });
            }
            if (request.RequestUri.AbsolutePath.StartsWith("/api/lectio/students/"))
            {
                LectioReads++;
                var response = new HttpResponseMessage(LectioStatus) { Content = new ByteArrayContent([9, 8, 7]) };
                response.Content.Headers.ContentType = new("image/jpeg");
                return Task.FromResult(response);
            }
            if (request.RequestUri.AbsolutePath.StartsWith("/api/student-photos/"))
            {
                Reads++;
                var response = new HttpResponseMessage(FailPhoto ? HttpStatusCode.Forbidden : CustomStatus)
                    { Content = new ByteArrayContent([1, 2, 3]) };
                response.Content.Headers.ContentType = new("image/jpeg");
                return Task.FromResult(response);
            }
            throw new InvalidOperationException($"Unexpected request {request.RequestUri}");
        }
    }
}
