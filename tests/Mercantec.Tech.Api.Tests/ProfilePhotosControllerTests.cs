using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Api.Students;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Mercantec.Tech.Api.Tests;

public sealed class ProfilePhotosControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private readonly PhotoStorage _storage = new();
    private readonly Guid _owner = Guid.NewGuid();
    private DbContextOptions<AppDbContext> Options => new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        await _connection.OpenAsync();
        _db = new(Options);
        await _db.Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }

    [Fact]
    public async Task PhotosArePrivateToOwnerAndTeachers()
    {
        var own = As(_owner);
        Assert.IsType<OkObjectResult>(await own.Upload(Form(Png()), default, _owner.ToString()));
        Assert.IsType<UnauthorizedResult>(await As(null).Read(_owner, default));
        Assert.IsType<ForbidResult>(await As(Guid.NewGuid()).Read(_owner, default));
        Assert.IsType<FileStreamResult>(await own.Read(_owner, default));
        var teacher = As(Guid.NewGuid(), teacher: true);
        Assert.IsType<FileStreamResult>(await teacher.Read(_owner, default));
        Assert.Equal("private, no-store", teacher.Response.Headers.CacheControl);
        Assert.IsType<UnauthorizedResult>(await As(null).Upload(Form(Png()), default));
        Assert.IsType<UnauthorizedResult>(await As(null).Delete(default));
    }

    [Fact]
    public async Task UploadAndDeleteAreBoundToExpectedAccount()
    {
        Assert.IsType<ConflictObjectResult>(await As(_owner).Upload(Form(Png()), default));
        Assert.IsType<ConflictObjectResult>(await As(_owner).Upload(Form(Png()), default, Guid.NewGuid().ToString()));
        Assert.IsType<OkObjectResult>(await As(_owner).Upload(Form(Png()), default, _owner.ToString()));
        Assert.IsType<ConflictObjectResult>(await As(_owner).Delete(default, Guid.NewGuid().ToString()));
        Assert.Single(_storage.Objects);
        Assert.Single(await _db.ProfilePhotos.ToListAsync());
    }

    [Fact]
    public async Task ReplacingAndRemovingPhotoCleansPrivateObjects()
    {
        var own = As(_owner);
        var first = Read(await own.Upload(Form(Png()), default, _owner.ToString()));
        Assert.True(first.HasCustomPhoto);
        var firstKey = Assert.Single(_storage.Objects).Key;
        Assert.True(Read(await own.Upload(Form(Png(800, 400)), default, _owner.ToString())).HasCustomPhoto);
        Assert.DoesNotContain(firstKey, _storage.Objects.Keys);
        var stored = Assert.Single(_storage.Objects).Value;
        using var codec = SKCodec.Create(SKData.CreateCopy(stored));
        Assert.Equal(512, codec.Info.Width);
        Assert.Equal(256, codec.Info.Height);
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.False(Read(await own.Delete(default, _owner.ToString())).HasCustomPhoto);
        Assert.Empty(_storage.Objects);
        Assert.Empty(await _db.ProfilePhotos.ToListAsync());
        Assert.IsType<NotFoundResult>(await own.Read(_owner, default));
    }

    [Fact]
    public async Task CorruptUnsupportedAndOversizedInputsNeverReachStorage()
    {
        foreach (var bytes in new[] { "<svg/>"u8.ToArray(), Png()[..30], new byte[ProfilePhotoRules.MaxBytes + 1] })
            Assert.IsType<BadRequestObjectResult>(await As(_owner).Upload(Form(bytes), default, _owner.ToString()));
        Assert.Empty(_storage.Objects);
        Assert.Empty(await _db.ProfilePhotos.ToListAsync());
    }

    [Fact]
    public async Task InterruptedObjectWriteRemovesObjectEvenWhenStorageWroteBeforeThrowing()
    {
        _storage.FailAfterPut = true;
        await Assert.ThrowsAsync<IOException>(() => As(_owner).Upload(Form(Png()), default, _owner.ToString()));
        Assert.Empty(_storage.Objects);
        Assert.Empty(await _db.ProfilePhotos.ToListAsync());
    }

    [Fact]
    public async Task CancellationAfterDatabaseCommitPreservesCommittedPhoto()
    {
        using var interrupted = new CanceledAfterCommitContext(Options);
        await Assert.ThrowsAsync<OperationCanceledException>(() => As(_owner, db: interrupted)
            .Upload(Form(Png()), default, _owner.ToString()));
        var persisted = Assert.Single(await _db.ProfilePhotos.AsNoTracking().ToListAsync());
        Assert.Equal(persisted.StorageKey, Assert.Single(_storage.Objects).Key);
    }

    [Fact]
    public async Task WrappedDatabaseFailureAfterCommitPreservesCommittedPhoto()
    {
        using var interrupted = new CanceledAfterCommitContext(Options, wrap: true);
        Assert.IsType<ConflictObjectResult>(await As(_owner, db: interrupted)
            .Upload(Form(Png()), default, _owner.ToString()));
        var persisted = Assert.Single(await _db.ProfilePhotos.AsNoTracking().ToListAsync());
        Assert.Equal(persisted.StorageKey, Assert.Single(_storage.Objects).Key);
    }

    [Fact]
    public async Task ConcurrentReplacementProtectsNewPhotoFromStaleDelete()
    {
        await As(_owner).Upload(Form(Png()), default, _owner.ToString());
        using var second = new AppDbContext(Options);
        await As(_owner, db: second).Upload(Form(Png(80, 40)), default, _owner.ToString());
        var currentKey = Assert.Single(_storage.Objects).Key;
        Assert.IsType<ConflictObjectResult>(await As(_owner).Delete(default, _owner.ToString()));
        Assert.Equal(currentKey, Assert.Single(_storage.Objects).Key);
        Assert.Single(await second.ProfilePhotos.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(1, 40, 20, "RGBY")]
    [InlineData(2, 40, 20, "GRYB")]
    [InlineData(3, 40, 20, "YBGR")]
    [InlineData(4, 40, 20, "BYRG")]
    [InlineData(5, 20, 40, "RBGY")]
    [InlineData(6, 20, 40, "BRYG")]
    [InlineData(7, 20, 40, "YGBR")]
    [InlineData(8, 20, 40, "GYRB")]
    public async Task ExifOrientationIsAppliedAndMetadataRemoved(byte orientation, int width, int height, string corners)
    {
        using var bitmap = new SKBitmap(40, 20);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var paint = new SKPaint();
            paint.Color = SKColors.Red; canvas.DrawRect(0, 0, 20, 10, paint);
            paint.Color = SKColors.Lime; canvas.DrawRect(20, 0, 20, 10, paint);
            paint.Color = SKColors.Blue; canvas.DrawRect(0, 10, 20, 10, paint);
            paint.Color = SKColors.Yellow; canvas.DrawRect(20, 10, 20, 10, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        var raw = jpeg.ToArray();
        byte[] exif = [0xff,0xe1,0,34, 0x45,0x78,0x69,0x66,0,0, 0x49,0x49,0x2a,0,8,0,0,0,
            1,0, 0x12,1,3,0,1,0,0,0,orientation,0,0,0, 0,0,0,0];
        var input = raw[..2].Concat(exif).Concat(raw[2..]).ToArray();
        var normalized = await ProfilePhotoImages.NormalizeAsync(input, default);
        Assert.NotNull(normalized);
        Assert.DoesNotContain("Exif", System.Text.Encoding.Latin1.GetString(normalized));
        using var output = SKCodec.Create(SKData.CreateCopy(normalized));
        Assert.Equal(width, output.Info.Width);
        Assert.Equal(height, output.Info.Height);
        Assert.Equal(SKEncodedOrigin.TopLeft, output.EncodedOrigin);
        using var result = SKBitmap.Decode(normalized);
        var actual = string.Concat(new[] { result.GetPixel(width / 4, height / 4), result.GetPixel(3 * width / 4, height / 4),
            result.GetPixel(width / 4, 3 * height / 4), result.GetPixel(3 * width / 4, 3 * height / 4) }.Select(ClosestColor));
        Assert.Equal(corners, actual);
    }

    private static char ClosestColor(SKColor pixel) => new[] { ('R', SKColors.Red), ('G', SKColors.Lime),
        ('B', SKColors.Blue), ('Y', SKColors.Yellow) }.MinBy(pair =>
        Math.Pow(pixel.Red - pair.Item2.Red, 2) + Math.Pow(pixel.Green - pair.Item2.Green, 2) + Math.Pow(pixel.Blue - pair.Item2.Blue, 2)).Item1;

    private ProfilePhotosController As(Guid? sub, bool teacher = false, AppDbContext? db = null)
    {
        var claims = new List<Claim>();
        if (sub is Guid id) claims.Add(new("sub", id.ToString()));
        if (teacher) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(db ?? _db, _storage, NullLogger<ProfilePhotosController>.Instance)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity(claims, sub is null ? null : "test")) } }
        };
    }
    private static ProfilePhotoView Read(IActionResult result) => Assert.IsType<ProfilePhotoView>(Assert.IsType<OkObjectResult>(result).Value);
    private static FormFile Form(byte[] bytes) => new(new MemoryStream(bytes), 0, bytes.Length, "file", "photo.png")
        { Headers = new HeaderDictionary(), ContentType = "image/png" };
    private static byte[] Png(int width = 40, int height = 40)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
    private sealed class PhotoStorage : IObjectStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = [];
        public bool FailAfterPut { get; set; }
        public bool IsConfigured => true;
        public string Bucket => "private-test";
        public async Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, cancellationToken);
            Objects[key] = buffer.ToArray();
            if (FailAfterPut) throw new IOException("Upload outcome was unknown to the caller");
            return new(key, Bucket, contentType, buffer.Length);
        }
        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<ObjectGetResult?>(
            Objects.TryGetValue(key, out var bytes) ? new(new MemoryStream(bytes), "image/jpeg", bytes.Length) : null);
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { Objects.Remove(key); return Task.CompletedTask; }
        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }

    private sealed class CanceledAfterCommitContext(DbContextOptions<AppDbContext> options, bool wrap = false) : AppDbContext(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await base.SaveChangesAsync(cancellationToken);
            if (wrap) throw new DbUpdateException("Unknown commit outcome", new IOException("Connection interrupted after commit"));
            throw new OperationCanceledException("Connection interrupted after commit");
        }
    }
}
