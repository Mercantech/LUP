using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Navigation;

namespace Mercantec.Tech.Web.Activity;

/// <summary>Shared destinations and copy for the full overview and header preview.</summary>
public static class ActionCenterPresentation
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public static IEnumerable<ActionCenterItem> Ordered(IEnumerable<ActionCenterItem> items) => items
        .OrderBy(i => i.Severity == "important" ? 0 : 1).ThenBy(i => i.DeadlineUtc ?? DateTimeOffset.MaxValue)
        .ThenBy(i => i.HoldName).ThenBy(i => i.Title);
    public static string Href(ActionCenterItem item)
    {
        if (item.Kind == "mention" && SafeMentionHref(item.Href))
            return item.Href + (item.Href!.Contains('?') ? "&" : "?") + $"noticeKind=mention&notice={item.ResourceId:D}";
        if (item.Kind == "social-game-invite" && IsLobbyCode(item.State)) return $"/games/social/{item.State}";
        if (item.Kind == "gift") return $"/gifts/{item.ResourceId:D}";
        if (item.Kind is "notebook-comment" or "notebook-share" && item.TargetId is Guid document)
        {
            var path = item.State == "shared-notebook" ? $"/notebook?owner={document:D}" : $"/notebook/{document:D}";
            var separator = path.Contains('?') ? "&" : "?";
            return path + separator + $"noticeKind={item.Kind}&notice={item.ResourceId:D}" + (item.Kind == "notebook-comment" ? "&comments=true" : "");
        }
        if (item.HoldId is not Guid hold || hold == Guid.Empty || item.WorkspaceId is not Guid workspace || workspace == Guid.Empty) return "/notifications";
        return item.Kind switch
        {
            "calendar-change" => WorkspaceNavigation.View(workspace, "calendar", eventId: item.State == "moved" ? item.TargetId : null),
            "poll" => WorkspaceNavigation.Poll(item.ResourceId, workspace, item.CourseId, hold),
            "review" => WorkspaceNavigation.View(workspace, "submissions", item.CourseId, questId: item.ResourceId, status: "submitted"),
            "feedback" when item.TargetId is Guid quest => WorkspaceNavigation.Quest(quest, workspace, item.CourseId, hold, item.SubmissionId) + $"&notice={item.ResourceId:D}",
            _ => WorkspaceNavigation.Quest(item.ResourceId, workspace, item.CourseId, hold, item.SubmissionId)
        };
    }
    public static string KindLabel(ActionCenterItem item) => item.Kind switch
    {
        "poll" => "Afstemning",
        "social-game-invite" => "Invitation til Werewolf",
        "gift" => "Uåbnet gave",
        "feedback" => "Feedback på din aflevering",
        "notebook-comment" => "Kommentar i notesbogen",
        "notebook-share" => "Delt med dig",
        "mention" => "Du er nævnt i denne side",
        "calendar-change" => item.State == "cancelled" ? "Aktiviteten er aflyst" : "Aktiviteten er flyttet",
        "quest" => item.State switch { "daily" => "Daily quest", "side" => "Side quest", "changes-requested" => "Feedback på en valgfri quest", _ => "Valgfri quest" },
        "review" => item.Count == 1 ? "1 aflevering til vurdering" : $"{item.Count} afleveringer til vurdering",
        _ => item.State == "changes-requested" ? "Ret din aflevering" : item.State == "overdue" ? "Afleveringsfristen er overskredet" : "Du mangler at aflevere"
    };
    public static string Icon(ActionCenterItem item) => item.Kind switch { "social-game-invite" => "people", "poll" => "chart", "review" => "check", "gift" => "gift", "feedback" or "notebook-comment" or "mention" => "message", "notebook-share" => "book", "calendar-change" => "calendar", _ => "target" };
    public static bool IsLobbyCode(string? code) => code is { Length: 6 } && code.All(char.IsAsciiDigit);
    public static bool SafeMentionHref(string? href) => href is not null && !href.Any(char.IsControl) && !href.Contains('\\') &&
        (href.StartsWith("/notes/", StringComparison.Ordinal) || href.StartsWith("/notion/", StringComparison.Ordinal) || href.StartsWith("/notebook/", StringComparison.Ordinal));
    public static string DateLabel(DateTimeOffset deadline) => TimeZoneInfo.ConvertTime(deadline, Copenhagen).ToString("d. MMM yyyy 'kl.' HH:mm", Danish);
}
