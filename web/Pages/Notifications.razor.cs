using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Navigation;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages;

public partial class Notifications : IDisposable
{
    [Inject] private AuthService Auth { get; set; } = null!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = null!;
    [Inject] private ActionCenterService Actions { get; set; } = null!;
    private IEnumerable<ActionCenterItem> Items(string severity) => ActionCenterPresentation.Ordered(Actions.Items.Where(i => i.Severity == severity));
    private static string Href(ActionCenterItem item) => ActionCenterPresentation.Href(item);
    private static string KindLabel(ActionCenterItem item) => ActionCenterPresentation.KindLabel(item);
    private static string Icon(ActionCenterItem item) => ActionCenterPresentation.Icon(item);
    private static string DateLabel(DateTimeOffset deadline) => ActionCenterPresentation.DateLabel(deadline);
    private static string OpenLabel(ActionCenterItem item) => item.Kind switch
    {
        "poll" => "Svar", "review" => "Giv feedback", "gift" => "Åbn gave", "feedback" => "Læs feedback",
        "notebook-comment" or "notebook-share" or "mention" => "Åbn notesbog", "calendar-change" => "Åbn kalender",
        _ => "Åbn opgave"
    };
    private Task Open(ActionCenterItem item) => item.Kind == "calendar-change" ? Actions.MarkReadAsync(item.Kind, item.ResourceId) : Task.CompletedTask;
    protected override void OnInitialized() { Actions.Changed += StatusChanged; }
    protected override Task OnAfterRenderAsync(bool firstRender) => firstRender ? Actions.EnsureAsync() : Task.CompletedTask;
    private Task Refresh() => Actions.RefreshAsync(force: true);
    private void StatusChanged() => _ = InvokeAsync(StateHasChanged);
    public void Dispose() { Actions.Changed -= StatusChanged; }
}
