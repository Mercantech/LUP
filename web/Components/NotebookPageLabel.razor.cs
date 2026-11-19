using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Mercantec.Tech.Web.Emojis;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Components;

public partial class NotebookPageLabel
{
    [Inject] private EmojiClient Emojis { get; set; } = null!;
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public Guid? IconEmojiId { get; set; }
    [Parameter] public bool HasChildren { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private static readonly ConditionalWeakTable<IReadOnlyList<CustomEmojiItem>, IReadOnlyDictionary<string, CustomEmojiItem>> CatalogIndexes = new();
    private (string, Guid?)? _input;
    private int _version;
    private int _imageVersion;
    private bool _retried;
    private bool _refreshing;
    private readonly HashSet<Guid> _failed = [];
    private IReadOnlyList<CustomEmojiItem> _catalog = [];
    private CustomEmojiItem? _icon;
    private string? _glyph;
    private bool _glyphOnly;
    private IReadOnlyList<EmojiTextPart> _parts = [];

    protected override async Task OnParametersSetAsync()
    {
        if (_input == (Title, IconEmojiId)) return;
        _input = (Title, IconEmojiId);
        var version = ++_version;
        _retried = false;
        _refreshing = false;
        _failed.Clear();
        _catalog = [];
        Resolve();
        var firstColon = Title.IndexOf(':');
        if (IconEmojiId.HasValue || (firstColon >= 0 && Title.LastIndexOf(':') > firstColon + 2))
            await Load(version);
    }

    private async Task Load(int version)
    {
        IReadOnlyList<CustomEmojiItem> catalog = [];
        try { catalog = await Emojis.List(); }
        catch (EmojiException) { /* Keep the literal title and its native fallback. */ }
        if (version != _version) return;
        _catalog = catalog;
        Resolve();
    }

    private async Task ImageFailed(CustomEmojiItem item)
    {
        if (_refreshing || !_catalog.Contains(item)) return;
        if (_retried)
        {
            _failed.Add(item.Id);
            Resolve();
            return;
        }
        _retried = true;
        _refreshing = true;
        var version = _version;
        Emojis.Invalidate();
        try { await Load(version); }
        finally
        {
            if (version == _version)
            {
                _refreshing = false;
                _imageVersion++;
            }
        }
    }

    private void Resolve()
    {
        var available = _failed.Count == 0 ? _catalog : _catalog.Where(item => !_failed.Contains(item.Id)).ToArray();
        var label = Title.Trim();
        List<EmojiTextPart> parts = available.Count > 0 && label.Contains(':')
            ? EmojiTextParts.Split(label, CatalogIndexes.GetValue(available, static items => EmojiTextParts.Index(items))).ToList()
            : [new(label, null)];
        var explicitIcon = available.FirstOrDefault(item => item.Id == IconEmojiId);
        _icon = explicitIcon ?? parts.FirstOrDefault(part => part.Emoji is not null).Emoji;
        _glyph = null;
        if (_icon is not null)
        {
            // Older titles sometimes use the same shortcode as a frame around the entire title.
            if (parts.Count > 1 && parts[0].Emoji is { } frame && frame.Id != _icon.Id && parts[^1].Emoji?.Id == frame.Id)
            {
                TrimIcon(parts, frame.Id, fromStart: true);
                TrimIcon(parts, frame.Id, fromStart: false);
                if (parts.Count == 0) parts.Add(new(EmojiLabel(frame), null));
            }
            if (explicitIcon is null)
            {
                var index = parts.FindIndex(part => part.Emoji?.Id == _icon.Id);
                if (index > 0 && index < parts.Count - 1)
                {
                    var before = parts[index - 1].Text;
                    var after = parts[index + 1];
                    if (before.Length > 0 && char.IsWhiteSpace(before[^1]) && after.Emoji is null)
                        parts[index + 1] = new(after.Text.TrimStart(), null);
                    parts.RemoveAt(index);
                }
            }
            TrimIcon(parts, _icon.Id, fromStart: true);
            TrimIcon(parts, _icon.Id, fromStart: false);
        }
        else
        {
            var indexes = StringInfo.ParseCombiningCharacters(label);
            if (indexes.Length > 0)
            {
                var first = StringInfo.GetNextTextElement(label);
                var last = label[indexes[^1]..];
                _glyph = IsEmoji(first) ? first : IsEmoji(last) ? last : null;
                if (_glyph is not null)
                {
                    if (first == _glyph) label = label[first.Length..].TrimStart();
                    if (last == _glyph && label.Length > 0) label = label[..^last.Length].TrimEnd();
                    parts = [new(label, null)];
                }
            }
        }
        var empty = parts.All(part => part.Emoji is null && string.IsNullOrWhiteSpace(part.Text));
        _glyphOnly = empty && _glyph is not null;
        if (empty && _icon is not null)
            parts = [new(EmojiLabel(_icon), null)];
        _parts = parts;
    }

    private static void TrimIcon(List<EmojiTextPart> parts, Guid id, bool fromStart)
    {
        while (parts.Count > 0)
        {
            var edge = fromStart ? 0 : parts.Count - 1;
            var part = parts[edge];
            if (part.Emoji is not null && part.Emoji.Id != id) return;
            if (part.Emoji is null)
            {
                var trimmed = fromStart ? part.Text.TrimStart() : part.Text.TrimEnd();
                if (trimmed.Length > 0)
                {
                    parts[edge] = new(trimmed, null);
                    return;
                }
            }
            parts.RemoveAt(edge);
        }
    }

    private static bool IsEmoji(string element)
    {
        if (element.Contains('\uFE0E')) return false;
        var rune = Rune.GetRuneAt(element, 0);
        if (element.EndsWith('\u20E3')) return rune.Value is '#' or '*' or >= '0' and <= '9';
        if (element.Contains('\uFE0F'))
            return Rune.IsSymbol(rune) || rune.Value is 0x203C or 0x2049 or 0x3030 or 0x303D;
        if (Rune.GetUnicodeCategory(rune) != UnicodeCategory.OtherSymbol) return false;
        return rune.Value is >= 0x1F000 and <= 0x1FAFF
            or >= 0x2600 and <= 0x27BF or >= 0x23E9 and <= 0x23EC
            or 0x231A or 0x231B or 0x23F0 or 0x23F3 or 0x25FD or 0x25FE or 0x2B1B or 0x2B1C or 0x2B50 or 0x2B55;
    }

    private static string EmojiLabel(CustomEmojiItem item) => string.IsNullOrWhiteSpace(item.AltText) ? item.Name : item.AltText;
}
