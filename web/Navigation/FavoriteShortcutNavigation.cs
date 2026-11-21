using System.Web;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Navigation;

public static class FavoriteShortcutNavigation
{
    public static IReadOnlyList<FavoriteShortcut> Defaults(bool teacher) => teacher
        ? TeacherDefaults
        : StudentDefaults;

    private static readonly FavoriteShortcut[] TeacherDefaults =
    [
        new("/courses", "Mine hold", "Undervisning, opgaver og afstemninger.", "people"),
        new("/teacher/students", "Elever", "Skoleforløb, arbejdsplads og sidst beståede forløb på tværs af hold.", "people"),
        new("/lectio", "Lectio", "Elever, hold og Auth-kobling.", "calendar"),
        new("/notifications", "Til opfølgning", "Afleveringer, der venter på feedback.", "check"),
        new("/library", "Oversigt", "Undervisningsplaner, fag og projekter.", "folder"),
        new("/teacher/notebooks", "Elevnotesbøger", "Læs og kommentér elevernes noter.", "book"),
        new("/teacher/notebook-templates", "Logbogsskabeloner", "Fælles skabeloner til logbøger.", "file")
    ];

    private static readonly FavoriteShortcut[] StudentDefaults =
    [
        new("/courses", "Mine hold", "Din undervisning, dine opgaver og dine grupper.", "people"),
        new("/notifications", "Mine handlinger", "Se opgaver og beskeder, der kræver din opmærksomhed.", "check"),
        new("/learning", "Min læring", "Forløb, fag og emner.", "book"),
        new("/student/calendar", "Min kalender", "Se dit skema og dine aftaler.", "calendar"),
        new("/projects/mine", "Mine projekter", "Fra idé til løsning.", "code"),
        new("/notebook", "Min notesbog", "Åbn dine faglige noter og din logbog.", "book")
    ];

    public static string? Destination(Uri uri)
    {
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Equals("/electives", StringComparison.OrdinalIgnoreCase)) path = "/valgfag";
        if (!FavoriteShortcutRules.IsAllowedHref(path)) return null;

        var query = HttpUtility.ParseQueryString(uri.Query);
        Guid? Id(string name) => Guid.TryParse(query[name], out var id) && id != Guid.Empty ? id : null;
        var segments = path.ToLowerInvariant().Split('/');

        if (segments is ["", "courses", var workspace] && Guid.TryParse(workspace, out var workspaceId))
        {
            var view = WorkspaceNavigation.NormalizeView(query["view"]);
            return view == "overview" && Id("course") is null ? path : WorkspaceNavigation.View(workspaceId,
                view, Id("course"), questId: Id("quest"), status: query["status"], eventId: Id("event"),
                materialId: Id("material"), groupId: Id("group"));
        }

        var contextIds = segments is ["", "notes" or "quests" or "polls", ..]
            or ["", "teacher", "polls", ..] or ["", "afleveringer"];
        var queryKeys = contextIds ? new[] { "workspace", "course", "hold", "submission", "scopeId" }
            : segments is ["", "notebook", ..] ? ["owner"] : Array.Empty<string>();
        var context = new List<string>();
        foreach (var key in queryKeys)
            if (Id(key) is Guid id) context.Add($"{key}={id:D}");
        if (segments[1] == "notes" && bool.TryParse(query["published"], out var published) && published)
            context.Add("published=true");
        if (segments is ["", "notes", "connections"] && query["scopeKind"] is "collection" or "notebook" or "hold")
            context.Add("scopeKind=" + query["scopeKind"]);
        if (segments is ["", "valgfag"] && query["vis"] is "aktuelle" or "mine" or "historik" or "administrer")
            context.Add("vis=" + query["vis"]);

        var destination = context.Count == 0 ? path : path + "?" + string.Join('&', context);
        return FavoriteShortcutRules.IsAllowedHref(destination) ? destination : null;
    }

    public static FavoriteShortcut Describe(string href, string pageTitle, bool teacher)
    {
        var known = Defaults(teacher).FirstOrDefault(item => Matches(item.Href, href));
        if (known is not null) return known;

        var uri = new Uri("https://lup.invalid" + href);
        var path = uri.AbsolutePath.ToLowerInvariant();
        var title = string.Join(' ', pageTitle.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var query = HttpUtility.ParseQueryString(uri.Query);
        if (title.Length > 0 && path.StartsWith("/courses/", StringComparison.Ordinal))
        {
            var section = query["view"] switch
            {
                "content" => "Undervisning", "quests" => "Opgaver", "calendar" => "Kalender", "groups" => "Klassen",
                "submissions" => "Afleveringer", "materials" => "Materialer", "settings" => "Indstillinger", "polls" => "Afstemninger",
                _ => null
            };
            if (section is not null) title += " · " + section;
        }
        if (path == "/valgfag")
        {
            title = query["vis"] switch
            {
                "aktuelle" => "Aktuelle valgfagshold",
                "mine" => "Mine valgfagstilmeldinger",
                "historik" => "Tidligere valgfagshold",
                "administrer" => "Administrér valgfag",
                _ => "Valgfag"
            };
        }

        if (title.Length > FavoriteShortcutRules.MaxTitleLength)
        {
            var length = FavoriteShortcutRules.MaxTitleLength - 1;
            if (char.IsHighSurrogate(title[length - 1])) length--;
            title = title[..length].TrimEnd() + "…";
        }

        var icon = path.Split('/')[1] switch
        {
            "courses" or "holds" or "people" => "people",
            "notes" or "notebook" or "journal" or "learning" => "book",
            "games" or "arena" => "game",
            "videnstjek" or "quests" => "target",
            "projects" => "code",
            _ when path.EndsWith("/calendar", StringComparison.Ordinal) || path == "/teacher/planner" => "calendar",
            _ => "file"
        };
        return new(href, title, "", icon);
    }

    public static bool Matches(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
