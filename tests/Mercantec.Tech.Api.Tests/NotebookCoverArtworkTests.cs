using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotebookCoverArtworkTests
{
    [Fact]
    public void All_curated_character_emoji_previews_are_packaged_with_the_API()
    {
        var resources = typeof(NotebookCoverArtwork).Assembly.GetManifestResourceNames()
            .Select(name => name.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);

        Assert.All(CharacterEmojiCatalog.All, emoji =>
            Assert.Contains("cover-art/" + emoji.Preview.Split('?')[0].TrimStart('/'), resources));
    }

    [Fact]
    public async Task Uses_matching_course_banner_and_actual_page_emoji_without_loading_unregistered_art()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var storage = new TrackingStorage();
        var artwork = new NotebookCoverArtwork(db, storage);
        var ray = CharacterEmojiCatalog.All.First(emoji => emoji.Name == "raymond_thinking");

        var result = await artwork.ResolveAsync("notion-page", "Vores næste prototype",
            new(["Formålet er at afprøve forskellige valg.", "💡"], [new(Guid.NewGuid(), null), new(ray.Id, ray.Name)],
                "Game Design"), default);

        Assert.Collection(result.References,
            part => Assert.Equal(ReadAsset("images/valgfag/game-design.webp"), part.Bytes),
            part =>
            {
                Assert.Equal(ray.Name, part.Name);
                Assert.Equal(ReadAsset(ray.Preview.Split('?')[0].TrimStart('/')), part.Bytes);
            },
            part => Assert.Equal("emoji U+1F4A1", part.Name));
        Assert.Contains("Raymond", result.Direction);
        Assert.Empty(storage.Keys);
    }

    [Fact]
    public async Task Loads_only_registered_emoji_assets_and_normalizes_uploads_for_the_provider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        const string key = "fixture/custom-emoji.png";
        using var embedded = Mercantec.Tech.Api.Emojis.StandardEmojiAssets.Open("standard-emojis/1f4a1.png")!;
        using var png = new MemoryStream();
        await embedded.CopyToAsync(png);
        var upload = png.ToArray();
        var emojiId = Guid.NewGuid();
        db.CustomEmojis.Add(new()
        {
            Id = emojiId, Name = "prototype_symbol", Key = key, ContentType = "image/png",
            SizeBytes = upload.Length, CreatedBySub = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        var storage = new TrackingStorage(new() { [key] = upload });

        var result = await new NotebookCoverArtwork(db, storage).ResolveAsync("notebook-page", "Refleksion",
            new(["Private note images never become reference files."],
                [new(emojiId, null), new(null, "prototype_symbol"), new(null, "https://example.test/image.png")]), default);

        Assert.Equal(key, Assert.Single(storage.Keys));
        var reference = Assert.Single(result.References, reference => reference.Name == "prototype_symbol");
        Assert.Equal("image/webp", reference.ContentType);
        Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(reference.Bytes, 8, 4));
    }

    private static byte[] ReadAsset(string path)
    {
        var assembly = typeof(NotebookCoverArtwork).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.Replace('\\', '/') == "cover-art/" + path);
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var result = new MemoryStream();
        stream.CopyTo(result);
        return result.ToArray();
    }

    private sealed class TrackingStorage(Dictionary<string, byte[]>? assets = null) : IObjectStorage
    {
        public bool IsConfigured => true;
        public string Bucket => "cover-test";
        public List<string> Keys { get; } = [];

        public Task<ObjectGetResult?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            Keys.Add(key);
            return Task.FromResult(assets?.GetValueOrDefault(key) is { } found
                ? new ObjectGetResult(new MemoryStream(found), "image/png", found.Length) : null);
        }

        public Task<ObjectPutResult> PutAsync(string key, Stream content, string contentType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Uri?> GetPresignedGetUrlAsync(string key, TimeSpan expiry,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
