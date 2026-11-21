using System.Text.Json.Serialization;

namespace Mercantec.Tech.Contracts;

public sealed record FavoriteShortcut(string Href, string Title, string Description, string Icon);

public sealed record FavoriteShortcutsDto(Guid? Revision, [property: JsonRequired] IReadOnlyList<FavoriteShortcut> Items);

public static class FavoriteShortcutRules
{
    public const string SubjectHeader = "X-LUP-Expected-Subject";
    public const int MaxCount = 40;
    public const int MaxHrefLength = 768;
    public const int MaxTitleLength = 160;
    public const int MaxDescriptionLength = 240;
    public const int MaxIconLength = 32;

    private static readonly string[] BlockedRoutes =
    [
        "/api", "/auth", "/login", "/logout", "/signout", "/admin", "/swagger",
        "/teacher/view-as", "/not-found", "/_framework", "/_content"
    ];

    public static bool IsAllowedHref(string? href) => DestinationKey(href) is not null;

    public static bool IsValidList(IReadOnlyList<FavoriteShortcut>? items)
    {
        if (items is null || items.Count > MaxCount) return false;

        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item is null
                || DestinationKey(item.Href) is not { } destination
                || !destinations.Add(destination)
                || !IsValidText(item.Title, MaxTitleLength, required: true)
                || !IsValidText(item.Description, MaxDescriptionLength, required: false)
                || !IsValidText(item.Icon, MaxIconLength, required: true))
                return false;
        }

        return true;
    }

    private static bool IsValidText(string? value, int maxLength, bool required) =>
        value is not null && value.Length <= maxLength
        && (!required || !string.IsNullOrWhiteSpace(value))
        && !value.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029');

    private static string? DestinationKey(string? href)
    {
        if (href is not { Length: > 1 and <= MaxHrefLength } || href[0] != '/'
            || href.Contains('\\') || href.Any(char.IsWhiteSpace) || href.Any(char.IsControl))
            return null;

        var pathEnd = href.IndexOfAny(['?', '#']);
        var path = pathEnd < 0 ? href : href[..pathEnd];

        // Validate the decoded route so escapes cannot disguise separators or a reserved destination.
        for (var depth = 0; depth < 8; depth++)
        {
            if (path.Contains("//", StringComparison.Ordinal) || path.IndexOfAny(['\\', '?', '#']) >= 0
                || path.Any(char.IsControl) || path.Split('/').Any(segment => segment is "." or ".."))
                return null;

            var decoded = Uri.UnescapeDataString(path);
            if (decoded == path)
            {
                path = path.TrimEnd('/');
                if (path.Length == 0 || path.Equals("/teacher", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/student", StringComparison.OrdinalIgnoreCase)
                    || BlockedRoutes.Any(blocked => path.Equals(blocked, StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith(blocked + "/", StringComparison.OrdinalIgnoreCase)))
                    return null;

                return pathEnd < 0 ? path : path + href[pathEnd..];
            }

            path = decoded;
        }

        return null;
    }
}
