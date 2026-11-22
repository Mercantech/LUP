using System.Text.RegularExpressions;
using Mercantec.Tech.Api.Emojis;

namespace Mercantec.Tech.Api.Notebooks;

public sealed record NotebookCoverEmoji(Guid? Id, string? Shortcode);

public sealed record NotebookCoverSource(IReadOnlyList<string> Excerpts, IReadOnlyList<NotebookCoverEmoji> Emojis,
    string? CourseTitle = null);

internal static partial class NotebookCoverEmojiSignals
{
    public const int MaxTextLength = 4096;
    private const int MaxSignals = 16;

    [GeneratedRegex(@"(?<![\p{L}\p{N}_]):([a-zA-Z0-9_-]{2,32}):", RegexOptions.CultureInvariant)]
    private static partial Regex Shortcodes();

    public static IEnumerable<NotebookCoverEmoji> InText(string text)
    {
        foreach (Match match in Shortcodes().Matches(text[..Math.Min(text.Length, MaxTextLength)]))
            yield return new NotebookCoverEmoji(null, match.Groups[1].Value.ToLowerInvariant());
    }

    public static NotebookCoverEmoji[] Unique(IEnumerable<NotebookCoverEmoji> candidates) =>
        candidates.Select(emoji => new NotebookCoverEmoji(emoji.Id == Guid.Empty ? null : emoji.Id,
                EmojiName.Normalize(emoji.Shortcode)))
            .Where(emoji => emoji.Id.HasValue || emoji.Shortcode is not null)
            .DistinctBy(emoji => (emoji.Id, Shortcode: emoji.Id.HasValue ? null : emoji.Shortcode))
            .Take(MaxSignals)
            .ToArray();
}
