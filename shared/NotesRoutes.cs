using System.Text.RegularExpressions;

namespace Mercantec.Tech.Contracts;

public static partial class NotesRoutes
{
    public const int SlugMaxLength = 64;

    public static string Collection(Guid id, string? slug = null) =>
        slug is null ? $"/notes/collections/{id:D}" : $"/notes/{slug}";

    public static string? NormalizeSlug(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    public static bool IsValidSlug(string slug) =>
        slug.Length <= SlugMaxLength
        && slug is not ("graph" or "connections" or "holds" or "collections")
        && !Guid.TryParse(slug, out _)
        && SlugPattern().IsMatch(slug);

    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
