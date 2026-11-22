using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Components;

public abstract partial class NotebookWorkspacePage
{
    protected NotebookShortcutHelp? _shortcutHelp;
    protected bool _creating;
    private string? _pendingShortcutFocus;

    protected abstract bool ShortcutCanCreatePage { get; }
    protected abstract bool ShortcutCanCreateChild { get; }
    protected abstract void BeginCreate(bool child);
    protected abstract NotebookTreeTarget? FindTreeTarget(Guid? current, string key);
    protected virtual Task UpdatePublishedPage() => Task.CompletedTask;

    [JSInvokable]
    public Task OnNotebookShortcut(string command) => InvokeAsync(async () =>
    {
        if (!_sidebarReady || _sidebarDisposed) return;
        switch (command)
        {
            case "toggle-sidebar":
                ToggleSidebar();
                break;
            case "search":
            case "explorer":
                SetSidebarOpen(true);
                _pendingShortcutFocus = command;
                break;
            case "new-page" when ShortcutCanCreatePage:
            case "new-child" when ShortcutCanCreateChild:
                if (_creating) SetSidebarOpen(true);
                else BeginCreate(command == "new-child");
                _pendingShortcutFocus = command;
                break;
            case "help" when _shortcutHelp is not null:
                await _shortcutHelp.OpenAsync();
                return;
            case "update-publication":
                var update = UpdatePublishedPage();
                StateHasChanged();
                await update;
                break;
            default:
                return;
        }
        StateHasChanged();
    });

    [JSInvokable]
    public NotebookTreeTarget? GetNotebookTreeTarget(string id, string key)
    {
        if (!_sidebarReady || _sidebarDisposed || !_sidebarOpen) return null;
        return FindTreeTarget(Guid.TryParse(id, out var current) ? current : null, key);
    }
}
