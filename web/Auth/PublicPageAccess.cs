using Mercantec.Tech.Web.Games;

namespace Mercantec.Tech.Web.Auth;

/// <summary>UI allowlist. API authorization remains the authority for all personal data.</summary>
public static class PublicPageAccess
{
    public static bool Allows(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0].Trim('/');
        if (path is "" or "login" or "auth/callback" or "privacy" or "help" or "sources" or "extension"
            or "arena" or "games" or "games/arena" or "games/wordle" or "games/avatar-demo" or "games/mine" or "games/upload"
            or "games/social" or "games/social/werewolf" or "games/social/werewolf/cards"
            or "games/dagens-ord" or "games/taljagt" or "games/fremskridt"
            or "spil" or "library" or "search" or "explore/modules" or "subjects" or "badges" or "explore/projects" or "courses/template" or "lup" or "characters")
            return true;

        if (GamesCatalog.IsPublicClassicPath(path)) return true;

        var parts = path.Split('/');
        return parts switch
        {
            ["courses", "template", _] => true,
            ["games", "social", var code] when code.Length == 6 && code.All(char.IsAsciiDigit) => true,
            ["subjects", _] or ["subjects", _, _] => true,
            ["badges", _] => true,
            ["explore", "projects", _] => true,
            ["characters", _] or ["spil", _] => true,
            _ => false
        };
    }
}
