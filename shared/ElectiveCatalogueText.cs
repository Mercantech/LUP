using System.Text.RegularExpressions;

namespace Mercantec.Tech.Contracts;

public static partial class ElectiveCatalogueText
{
    public static string? Source(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsNotionSource(uri)
            ? null : url.Trim();
    }

    public static bool IsNotionSource(Uri uri) => NotionHostname().IsMatch(uri.IdnHost);

    public static string Clean(string text)
    {
        if (!text.Contains("notion", StringComparison.OrdinalIgnoreCase))
            return text;

        var labelled = NotionMarkdownLink().Replace(text, "${label}");
        return Addresses().Replace(labelled, match => match.Groups["remove"].Success ? "" : match.Value).Trim();
    }

    private const string Host = @"(?:[a-z0-9-]+\.)*(?:notion\.(?:site|so|com)|notionusercontent\.com)";
    private const string NotionUrl = @"(?:(?:https?:)?//)?" + Host + @"(?![\w-]|\.[\w-])(?::[0-9]+)?(?:[/?#][^\s<>""')\]]*)?";

    [GeneratedRegex(@"\A" + Host + @"\.?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NotionHostname();

    // Consume other HTTP links intact, including a Notion address embedded in their query or path.
    [GeneratedRegex(@"(?<![\w@/.-])(?<remove>" + NotionUrl + @")|(?:https?:)?//[^\s<>""')\]]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Addresses();

    [GeneratedRegex(@"\[(?<label>[^\[\]\r\n]*)\]\(" + NotionUrl + @"\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NotionMarkdownLink();
}
