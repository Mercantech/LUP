using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Mercantec.Tech.Api.Tests;

public sealed class LectioStudentPhotoTests
{
    [Fact]
    public async Task TeacherCanReadUnregisteredStudentPhotoUsingValidatedBearerAndPrivateThumbnail()
    {
        using var api = new PhotoSource();
        using var http = Client(api);
        var controller = Controller(http, teacher: true);
        var photo = Assert.IsType<FileContentResult>(await controller.StudentPhoto("123456", default));
        Assert.Equal("image/jpeg", photo.ContentType);
        using var codec = SKCodec.Create(SKData.CreateCopy(photo.FileContents));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal(512, codec.Info.Width);
        Assert.Equal(256, codec.Info.Height);
        Assert.Equal("Bearer validated-token", api.Authorization);
        Assert.Equal("/api/people/students/123456/photo", api.PhotoPath);
        Assert.Equal(0, api.IdentityReads);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"]);
    }

    [Fact]
    public async Task StudentsCanReadOnlyTheirOwnLinkedLectioPhoto()
    {
        using var api = new PhotoSource();
        using var http = Client(api);
        Assert.IsType<FileContentResult>(await Controller(http).StudentPhoto("123456", default));
        Assert.Equal(1, api.IdentityReads);
        Assert.Equal(1, api.PhotoReads);
        Assert.IsType<ForbidResult>(await Controller(http).StudentPhoto("999999", default));
        Assert.Equal(2, api.IdentityReads);
        Assert.Equal(1, api.PhotoReads);
        api.OwnLectioId = null;
        Assert.IsType<ForbidResult>(await Controller(http).StudentPhoto("123456", default));
        Assert.Equal(1, api.PhotoReads);
    }

    [Fact]
    public async Task MissingIdentityOrBearerNeverContactsCollector()
    {
        using var api = new PhotoSource();
        using var http = Client(api);
        Assert.IsType<UnauthorizedResult>(await Controller(http, anonymous: true).StudentPhoto("123456", default));
        Assert.IsType<UnauthorizedObjectResult>(await Controller(http, teacher: true, bearer: false).StudentPhoto("123456", default));
        Assert.Equal(0, api.PhotoReads + api.IdentityReads);
    }

    [Theory]
    [InlineData(404, typeof(NotFoundResult))]
    [InlineData(403, typeof(ForbidResult))]
    [InlineData(401, typeof(UnauthorizedResult))]
    public async Task PhotoAbsenceAndAccessFailuresArePreserved(int status, Type resultType)
    {
        using var api = new PhotoSource { Status = (HttpStatusCode)status };
        using var http = Client(api);
        Assert.IsType(resultType, await Controller(http, teacher: true).StudentPhoto("123456", default));
    }

    [Fact]
    public async Task UpstreamFailureDoesNotExposeItsHtmlOrInternalError()
    {
        using var api = new PhotoSource { Status = HttpStatusCode.ServiceUnavailable };
        using var http = Client(api);
        var result = Assert.IsType<ObjectResult>(await Controller(http, teacher: true).StudentPhoto("123456", default));
        Assert.Equal(503, result.StatusCode);
        Assert.DoesNotContain("private upstream", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task HtmlAndInvalidRasterAreRejectedInsteadOfForwarded()
    {
        using var api = new PhotoSource { ContentType = "text/html", Bytes = "<html>private upstream</html>"u8.ToArray() };
        using var http = Client(api);
        var html = Assert.IsType<ObjectResult>(await Controller(http, teacher: true).StudentPhoto("123456", default));
        Assert.Equal(503, html.StatusCode);
        api.ContentType = "image/png";
        var invalid = Assert.IsType<ObjectResult>(await Controller(http, teacher: true).StudentPhoto("123456", default));
        Assert.Equal(502, invalid.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredAndChunkedOversizedPhotosAreBounded(bool chunked)
    {
        using var api = new PhotoSource { Bytes = new byte[2 * 1024 * 1024 + 1], Chunked = chunked };
        using var http = Client(api);
        var result = Assert.IsType<ObjectResult>(await Controller(http, teacher: true).StudentPhoto("123456", default));
        Assert.Equal(503, result.StatusCode);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("123/456")]
    [InlineData("123?url=https://other.test")]
    public async Task InvalidPhotoIdentityCannotChangeCollectorRoute(string id)
    {
        using var api = new PhotoSource();
        using var http = Client(api);
        Assert.IsType<BadRequestObjectResult>(await Controller(http, teacher: true).StudentPhoto(id, default));
        Assert.Equal(0, api.PhotoReads + api.IdentityReads);
    }

    private static HttpClient Client(PhotoSource source) => new(source) { BaseAddress = new Uri("https://collector.test/api/") };
    private static LectioController Controller(HttpClient http, bool teacher = false, bool anonymous = false, bool bearer = true)
    {
        var claims = new List<Claim>();
        if (!anonymous) claims.Add(new("sub", Guid.NewGuid().ToString()));
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, anonymous ? null : "test")) };
        if (bearer) context.Request.Headers.Authorization = "Bearer validated-token";
        return new(new LectioCollectorClient(http, NullLogger<LectioCollectorClient>.Instance), new MembershipClaimService(null!))
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private sealed class PhotoSource : HttpMessageHandler
    {
        public string? OwnLectioId { get; set; } = "123456";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string ContentType { get; set; } = "image/png";
        public byte[] Bytes { get; set; } = Png();
        public bool Chunked { get; set; }
        public int PhotoReads { get; private set; }
        public int IdentityReads { get; private set; }
        public string? Authorization { get; private set; }
        public string? PhotoPath { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Authorization = request.Headers.Authorization?.ToString();
            if (request.RequestUri!.AbsolutePath == "/api/me")
            {
                IdentityReads++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new { lectio = OwnLectioId is null ? null : new { lectioId = OwnLectioId } }) });
            }
            PhotoPath = request.RequestUri.AbsolutePath;
            PhotoReads++;
            var response = new HttpResponseMessage(Status);
            if (Status != HttpStatusCode.OK) response.Content = new StringContent("private upstream error details");
            else
            {
                response.Content = Chunked ? new StreamContent(new NonSeekableStream(Bytes)) : new ByteArrayContent(Bytes);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
            }
            return Task.FromResult(response);
        }
        private static byte[] Png()
        {
            using var bitmap = new SKBitmap(800, 400); bitmap.Erase(SKColors.CornflowerBlue);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream input = new(bytes, writable: false);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => input.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) input.Dispose(); base.Dispose(disposing); }
    }
}
