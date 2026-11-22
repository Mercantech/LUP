using System.Collections.Frozen;
using System.Text;
using Amazon.S3;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Notebooks;
using Mercantec.Tech.Api.Storage;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Integrations.OpenAI;

public sealed record NotebookCoverArtReference(string Name, string Role, string ContentType, byte[] Bytes);
public sealed record NotebookCoverArtDirection(string Direction, IReadOnlyList<NotebookCoverArtReference> References,
    IReadOnlyList<string>? EmojiContext = null, string? CharacterSlug = null);

public sealed class NotebookCoverArtwork(AppDbContext db, IObjectStorage storage)
{
    private const string ResourcePrefix = "cover-art/";
    private const int MaxEmojiSignals = 32;
    private const int MaxEmojiImages = 3;
    private static readonly System.Reflection.Assembly ArtAssembly = typeof(NotebookCoverArtwork).Assembly;
    private static readonly FrozenDictionary<string, string> ArtResources = ArtAssembly.GetManifestResourceNames()
        .Where(resource => resource.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        .ToFrozenDictionary(resource => resource[ResourcePrefix.Length..].Replace('\\', '/'), StringComparer.Ordinal);
    private static readonly FrozenDictionary<Guid, CharacterEmojiAsset> CharactersById =
        CharacterEmojiCatalog.All.ToFrozenDictionary(emoji => emoji.Id);
    private static readonly FrozenDictionary<string, CharacterEmojiAsset> CharactersByName =
        CharacterEmojiCatalog.All.ToFrozenDictionary(emoji => emoji.Name, StringComparer.OrdinalIgnoreCase);

    public async Task<NotebookCoverArtDirection> ResolveAsync(string kind, string title, NotebookCoverSource source,
        CancellationToken ct, string? selectedCharacter = null, string? illustrationDescription = null)
    {
        var signals = source.Emojis.Take(MaxEmojiSignals)
            .Select(signal => signal with { Shortcode = EmojiName.Normalize(signal.Shortcode) })
            .Where(signal => signal.Id.HasValue || signal.Shortcode is not null)
            .Distinct()
            .ToArray();
        var known = signals.Select(GetCharacter).ToArray();
        var ids = signals.Where(signal => signal.Id.HasValue).Select(signal => signal.Id!.Value).Distinct().ToArray();
        var names = signals.Select(signal => signal.Shortcode).Concat(known.Select(character => character?.Name))
            .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var uploaded = signals.Length == 0 ? [] : await db.CustomEmojis.AsNoTracking()
            .Where(emoji => ids.Contains(emoji.Id) || names.Contains(emoji.Name))
            .ToArrayAsync(ct);
        var byId = uploaded.ToDictionary(emoji => emoji.Id);
        var byName = uploaded.ToDictionary(emoji => emoji.Name, StringComparer.OrdinalIgnoreCase);
        var publicCharacters = CharacterEmojiPolicy.Available(uploaded).Select(character => character.Id).ToHashSet();
        var used = new HashSet<Guid>();
        var characterSlugs = new List<string>();
        if (selectedCharacter is not null) characterSlugs.Add(selectedCharacter);
        var referencedCharacters = new HashSet<string>(StringComparer.Ordinal);
        var emojiImages = new List<NotebookCoverArtReference>();
        var emojiContext = new List<string>();
        using var mediaBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        mediaBudget.CancelAfter(TimeSpan.FromSeconds(3));
        for (var index = 0; index < signals.Length; index++)
        {
            var signal = signals[index];
            var custom = signal.Id is Guid id ? byId.GetValueOrDefault(id)
                : signal.Shortcode is { } name ? byName.GetValueOrDefault(name) : null;
            if (custom is not null)
            {
                if (used.Add(custom.Id))
                {
                    emojiContext.Add(string.IsNullOrWhiteSpace(custom.AltText) ? custom.Name : $"{custom.Name}: {custom.AltText}");
                    if (emojiImages.Count < MaxEmojiImages && await ReadEmojiAsync(custom, mediaBudget.Token, ct) is { } image)
                        emojiImages.Add(new(custom.Name, "emoji used on the source page", "image/webp", image));
                }
                continue;
            }

            if (known[index] is not { } character || !publicCharacters.Contains(character.Id) || !used.Add(character.Id))
                continue;
            characterSlugs.Add(character.CharacterSlug);
            emojiContext.Add($"{character.Name}: {character.Alt}");
            if (emojiImages.Count < MaxEmojiImages && ReadEmbedded(character.Preview) is { } preview)
            {
                referencedCharacters.Add(character.CharacterSlug);
                emojiImages.Add(new(character.Name, "LUP character emoji used on the source page", "image/webp", preview));
            }
        }

        if (emojiImages.Count < MaxEmojiImages)
        {
            var glyphs = string.Join('\n', source.Excerpts.Prepend(title)).EnumerateRunes()
                .Where(rune => rune.Value >= 0x2000).Distinct();
            foreach (var glyph in glyphs)
            {
                var key = $"{StandardEmojiAssets.KeyPrefix}{glyph.Value:x}.png";
                if (uploaded.Any(emoji => used.Contains(emoji.Id) && emoji.Key == key)) continue;
                await using var embedded = StandardEmojiAssets.Open(key);
                if (embedded is null) continue;
                if (mediaBudget.IsCancellationRequested) break;
                if (await NormalizeEmojiAsync(embedded, ct) is not { } image) continue;
                emojiImages.Add(new($"emoji U+{glyph.Value:X}", "emoji symbol used on the source page", "image/webp", image));
                if (emojiImages.Count == MaxEmojiImages) break;
            }
        }

        ct.ThrowIfCancellationRequested();
        var style = NotebookCoverStyle.Select(kind, title, source.Excerpts, characterSlugs, source.CourseTitle, illustrationDescription);
        var references = new List<NotebookCoverArtReference>();
        if (ReadEmbedded(style.StyleAssetPath) is { } banner)
            references.Add(new("lup-valgfag", "LUP style and composition reference", "image/webp", banner));
        if (style.CharacterAssetPath is { } characterPath
            && !referencedCharacters.Contains(style.CharacterSlug ?? "")
            && ReadEmbedded(characterPath) is { } characterImage)
            references.Add(new(style.CharacterSlug ?? "lupp", style.CharacterSlug is null
                ? "LUP character materials and shape language" : "canonical LUP character design", "image/webp", characterImage));
        references.AddRange(emojiImages);
        return new(style.PromptDirection, references, emojiContext, style.CharacterSlug);
    }

    private static CharacterEmojiAsset? GetCharacter(NotebookCoverEmoji signal) => signal.Id is Guid id
        ? CharactersById.GetValueOrDefault(id)
        : signal.Shortcode is { } name ? CharactersByName.GetValueOrDefault(name) : null;

    private static byte[]? ReadEmbedded(string path)
    {
        var query = path.IndexOf('?');
        var key = (query >= 0 ? path[..query] : path).TrimStart('/');
        if (!ArtResources.TryGetValue(key, out var resource)) return null;
        using var stream = ArtAssembly.GetManifestResourceStream(resource);
        if (stream is null || stream.Length is <= 0 or > EmojiName.MaxBytes) return null;
        var image = new byte[(int)stream.Length];
        stream.ReadExactly(image);
        return image;
    }

    private async Task<byte[]?> ReadEmojiAsync(CustomEmoji emoji, CancellationToken budget, CancellationToken request)
    {
        if (budget.IsCancellationRequested || emoji.SizeBytes is <= 0 or > EmojiName.MaxBytes) return null;
        try
        {
            if (StandardEmojiAssets.IsStandard(emoji.Key))
            {
                await using var embedded = StandardEmojiAssets.Open(emoji.Key);
                return embedded is null ? null : await NormalizeEmojiAsync(embedded, budget);
            }

            if (!storage.IsConfigured) return null;
            await using var obj = await storage.GetAsync(emoji.Key, budget);
            return obj is null ? null : await NormalizeEmojiAsync(obj.Content, budget);
        }
        catch (Exception ex) when (!request.IsCancellationRequested
            && ex is HttpRequestException or IOException or AmazonS3Exception or OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<byte[]?> NormalizeEmojiAsync(Stream source, CancellationToken ct)
    {
        var buffer = new byte[EmojiName.MaxBytes + 1];
        var bytes = await source.ReadAtLeastAsync(buffer, buffer.Length, false, ct);
        return bytes is > 0 and <= EmojiName.MaxBytes ? NotebookCoverImage.NormalizeEmoji(buffer[..bytes]) : null;
    }
}
