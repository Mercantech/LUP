using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Components;

public abstract partial class NotebookWorkspacePage : ComponentBase
{
    [Inject] private IJSRuntime SidebarJs { get; set; } = default!;
    protected ElementReference _workspaceElement;
    protected bool _sidebarOpen = true, _sidebarMobile, _sidebarReady, _workspaceFocused, _scrollCenterToTop;
    protected bool SidebarEnhanced => _sidebarReady && _sidebarModule is not null;
    private bool _sidebarStarting, _sidebarNeedsSync, _sidebarDisposed;
    private int _sidebarInteractions;
    private int _sidebarGeneration;
    private IJSObjectReference? _sidebarModule;
    private Task<IJSObjectReference>? _sidebarModuleTask;
    private ElementReference _sidebarMountedElement;
    private DotNetObjectReference<NotebookWorkspacePage>? _callbacks;
    private sealed record SidebarPreferences(bool Mobile, bool DesktopOpen);

    protected static async Task WaitForCoverContent(
        Func<(IJSObjectReference? Module, ElementReference Element, string Status)> editor,
        CancellationToken cancellationToken)
    {
        const string message = "Indholdet skal være gemt i skyen. Kontrollér forbindelsen, og prøv igen, når siden er gemt.";
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wait.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            while (true)
            {
                wait.Token.ThrowIfCancellationRequested();
                var (module, element, status) = editor();
                // With no local edits, the cover can use the server document even
                // while the editor is mounting or reconnecting.
                if (module is null || !await module.InvokeAsync<bool>("hasUnsavedChanges", wait.Token, element))
                    return;
                if (status is "offline" or "error") throw new InvalidOperationException(message);
                await Task.Delay(150, wait.Token);
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(message, ex);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException(message, ex);
        }
    }

    protected async Task InitializeSidebar()
    {
        if (_sidebarDisposed || _sidebarStarting || string.IsNullOrEmpty(_workspaceElement.Id) || _sidebarReady && _sidebarMountedElement.Id == _workspaceElement.Id) return;
        _sidebarStarting = true;
        var generation = ++_sidebarGeneration;
        var interactions = _sidebarInteractions;
        var element = _workspaceElement;
        try
        {
            _sidebarModuleTask ??= SidebarJs.InvokeAsync<IJSObjectReference>("import", "/js/notion-sidebar.js?v=cover-menu-20261122").AsTask();
            var module = await _sidebarModuleTask;
            if (generation != _sidebarGeneration) return;
            if (_sidebarModule is not null && !string.IsNullOrEmpty(_sidebarMountedElement.Id))
                await _sidebarModule.InvokeVoidAsync("destroy", _sidebarMountedElement);
            if (generation != _sidebarGeneration) return;
            _sidebarModule = module;
            _sidebarMountedElement = element;
            _callbacks ??= DotNetObjectReference.Create(this);
            var state = await module.InvokeAsync<SidebarPreferences?>("mount", element, _callbacks);
            if (generation != _sidebarGeneration) return;
            if (state is not null)
            {
                _sidebarMobile = state.Mobile;
                if (interactions == _sidebarInteractions) _sidebarOpen = !state.Mobile && state.DesktopOpen;
            }
            _sidebarNeedsSync = true;
        }
        catch (JSException) when (generation == _sidebarGeneration)
        {
            _sidebarMountedElement = element;
            _sidebarModule = null;
            if (_sidebarModuleTask?.IsFaulted == true) _sidebarModuleTask = null;
        }
        catch (JSException) { /* A superseded workspace has already been detached. */ }
        finally
        {
            _sidebarStarting = false;
            if (generation == _sidebarGeneration) _sidebarReady = true;
            if (!_sidebarDisposed) await InvokeAsync(StateHasChanged);
        }
    }
    protected void SetSidebarOpen(bool open)
    {
        ++_sidebarInteractions;
        if (open && _workspaceFocused) { _workspaceFocused = false; _ = SyncWorkspaceFocus(); }
        if (_sidebarOpen == open) return;
        _sidebarOpen = open; _sidebarNeedsSync = true;
    }
    protected void ToggleSidebar() => SetSidebarOpen(_workspaceFocused || !_sidebarOpen);
    protected async Task ToggleWorkspaceFocus()
    {
        _workspaceFocused = !_workspaceFocused;
        await SyncWorkspaceFocus();
    }
    private async Task SyncWorkspaceFocus()
    {
        if (_sidebarModule is null || _sidebarDisposed) return;
        try { await _sidebarModule.InvokeVoidAsync("setFocus", _sidebarMountedElement, _workspaceFocused); }
        catch (JSException) { /* The document remains available without enhanced layout controls. */ }
    }
    protected async Task UnmountSidebar()
    {
        ++_sidebarGeneration;
        var element = _sidebarMountedElement;
        _sidebarMountedElement = default;
        _sidebarReady = false;
        _pendingShortcutFocus = null;
        if (_sidebarModule is not null && !string.IsNullOrEmpty(element.Id))
        {
            try { await _sidebarModule.InvokeVoidAsync("destroy", element); }
            catch (JSException) { }
        }
    }
    [JSInvokable] public Task CloseNotionSidebar() => InvokeAsync(() => { if (!_sidebarDisposed) { SetSidebarOpen(false); StateHasChanged(); } });
    [JSInvokable] public Task OnNotionFocusMode(bool focused) => InvokeAsync(() =>
    {
        if (_sidebarDisposed || _workspaceFocused == focused) return;
        _workspaceFocused = focused; StateHasChanged();
    });
    [JSInvokable] public Task OnNotionSidebarViewport(bool mobile, bool desktopOpen) => InvokeAsync(() =>
    {
        if (_sidebarDisposed) return;
        _sidebarMobile = mobile; ++_sidebarInteractions;
        _sidebarOpen = !mobile && desktopOpen; _sidebarNeedsSync = true;
        StateHasChanged();
    });

    protected async Task SyncSidebarAfterRender()
    {
        if (_sidebarNeedsSync && _sidebarModule is not null && !_sidebarDisposed)
        {
            _sidebarNeedsSync = false;
            try { await _sidebarModule.InvokeVoidAsync("setOpen", _sidebarMountedElement, _sidebarOpen); }
            catch (JSException) { /* The visible toggle still works without browser enhancements. */ }
        }
        if (_scrollCenterToTop && _sidebarModule is not null && !_sidebarDisposed)
        {
            _scrollCenterToTop = false;
            try { await _sidebarModule.InvokeVoidAsync("scrollCenterTop", _sidebarMountedElement); }
            catch (JSException) { /* The center remains scrollable without the enhanced shortcut. */ }
        }
        if (_pendingShortcutFocus is string command && _sidebarModule is not null && !_sidebarDisposed)
        {
            _pendingShortcutFocus = null;
            try { await _sidebarModule.InvokeVoidAsync("focusShortcutTarget", _sidebarMountedElement, command); }
            catch (JSException) { /* Navigation and creation also work via the sidebar controls. */ }
        }
    }

    protected async ValueTask DisposeSidebar()
    {
        _sidebarDisposed = true;
        try
        {
            await UnmountSidebar();
            if (_sidebarModuleTask is not null) await (await _sidebarModuleTask).DisposeAsync();
        }
        catch (JSException) { }
        _callbacks?.Dispose();
    }
}
