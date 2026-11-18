using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Components;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Notebook;

public partial class Index
{
    private NotionPageLink[] _treePages = [];
    private NotebookTreeDialogs? _treeDialogs;
    private Guid? _draggingPage;
    private readonly HashSet<Guid> _expandedPages = [];
    private bool HasFilters => !string.IsNullOrWhiteSpace(_search) || !string.IsNullOrWhiteSpace(_tag) || _kind.Length > 0;
    private bool CanReorderTree => !HasFilters;
    private int PageCount => !HasFilters && _current is not null ? _treePages.Length : _total;
    private NotebookTreeItem[] TreeItems => _treePages.Select(p => new NotebookTreeItem(p.Id, p.Title, p.ParentPageId, _notebookId)).ToArray();
    private CancellationToken TreeToken => _requests?.Token ?? _life.Token;

    private Task OpenTreeActions(Guid id) => _treeDialogs?.Show(id) ?? Task.CompletedTask;
    protected override NotebookTreeTarget? FindTreeTarget(Guid? current, string key)
    {
        if (!CanUse || _loading || _filtering) return null;
        if (TemplateMode)
            return IsTeacher ? NotebookTreeUi.KeyboardTarget(VisibleTemplates.ToList(), current, key, t => t.Id, _ => null) : null;
        var rows = OrderedPages();
        return NotebookTreeUi.KeyboardTarget(rows, current, key, r => r.Page.Id, r => r.Page.ParentPageId,
            rows.Count > 60 ? _virtualRows : null);
    }
    private Task OpenTreeTrash() => _treeDialogs?.ShowTrash() ?? Task.CompletedTask;
    private Task CreateTreeChild(Guid id) { _expandedPages.Add(id); BeginCreateAt(id); return Task.CompletedTask; }
    private void ToggleBranch(Guid id)
    {
        if (!_expandedPages.Remove(id)) _expandedPages.Add(id);
        _expansionVersion++;
    }
    private void RevealAncestors(Guid page)
    {
        var links = _treePages.ToDictionary(p => p.Id);
        var seen = new HashSet<Guid>();
        while (links.TryGetValue(page, out var link) && link.ParentPageId is Guid parent && seen.Add(parent))
        {
            _expandedPages.Add(parent);
            page = parent;
        }
    }
    private sealed record SidebarRow(NotionPageLink Page, int Depth, bool HasChildren, string? Detail);
    private List<SidebarRow> _visibleRows = [], _virtualRows = [];
    private SidebarRow? _activeRow;
    private int _expansionVersion;
    private (NotebookPageSummary[] Pages, NotionPageLink[] Links, Guid? Current, bool Filtered, bool Searching, int Expansion)? _rowState;

    private List<SidebarRow> OrderedPages()
    {
        var state = (_pages, _treePages, _current?.DocumentId, HasFilters, !string.IsNullOrWhiteSpace(_search), _expansionVersion);
        if (_rowState == state) return _visibleRows;
        _rowState = state;
        var summaries = _pages.ToDictionary(p => p.Id);
        var links = !HasFilters && _current is not null ? _treePages
            : _pages.Select(p => new NotionPageLink(p.Id, p.Title, p.ParentPageId, p.SortOrder)).ToArray();
        var sorted = links.OrderBy(p => p.SortOrder ?? int.MaxValue)
            .ThenByDescending(p => summaries.GetValueOrDefault(p.Id)?.PeriodStart).ThenBy(p => p.Title).ThenBy(p => p.Id).ToArray();
        var ids = sorted.Select(p => p.Id).ToHashSet();
        var children = sorted.ToLookup(p => p.ParentPageId);
        var seen = new HashSet<Guid>();
        _visibleRows = [];
        void Walk(NotionPageLink root)
        {
            var pending = new Stack<(NotionPageLink Page, int Depth, bool Visible)>();
            pending.Push((root, 0, true));
            while (pending.TryPop(out var item))
            {
                if (!seen.Add(item.Page.Id)) continue;
                if (item.Visible)
                {
                    var summary = summaries.GetValueOrDefault(item.Page.Id);
                    var detail = string.IsNullOrWhiteSpace(_search) ? null : summary?.Snippet;
                    if (string.IsNullOrWhiteSpace(detail) && summary?.Kind == "journal")
                        detail = summary.PeriodStart?.ToString("d. MMM yyyy") ?? "Logbog";
                    _visibleRows.Add(new(item.Page, Math.Min(item.Depth, 5), children.Contains(item.Page.Id), detail));
                }
                var showChildren = item.Visible && (HasFilters || _expandedPages.Contains(item.Page.Id));
                foreach (var child in children[item.Page.Id].Reverse())
                    pending.Push((child, item.Depth + 1, showChildren));
            }
        }

        foreach (var page in sorted.Where(p => p.ParentPageId is not Guid parent || !ids.Contains(parent))) Walk(page);
        foreach (var page in sorted) if (!seen.Contains(page.Id)) Walk(page);
        _activeRow = _visibleRows.FirstOrDefault(row => row.Page.Id == _current?.DocumentId);
        _virtualRows = _visibleRows.Count > 60 ? _visibleRows.Where(row => row != _activeRow).ToList() : [];
        return _visibleRows;
    }
    private void StartTreeDrag(Guid id) { if (Own && !_busy) _draggingPage = id; }
    private void EndTreeDrag() => _draggingPage = null;
    private async Task DropTreePage((Guid? Target, string Position) drop)
    {
        var source = _draggingPage;
        EndTreeDrag();
        if (source is Guid id && NotebookTreeUi.Placement(TreeItems, id, drop.Target, drop.Position) is { } move)
            await Work(async _ => await MoveTreePage(id, move));
    }
    private async Task<bool> MoveTreePage(Guid id, MoveNotebookPage move)
    {
        if (!Own) return false;
        await Client.MovePage(id, move, TreeToken);
        if (move.ParentPageId is Guid parent) _expandedPages.Add(parent);
        await RefreshTree();
        _notice = "Siden er flyttet.";
        await InvokeAsync(StateHasChanged);
        return true;
    }
    private Task<NotebookPageDeletionPreview> PreviewTreeDelete(Guid id) => Client.DeletePreview(id, TreeToken);
    private async Task<bool> DeleteTreePage(Guid id, int descendants)
    {
        if (!Own || !await CanLeave()) return false;
        var branch = NotebookTreeUi.Branch(TreeItems, id);
        await Client.DeletePage(id, descendants, TreeToken);
        if (_current is { } page && branch.Contains(page.DocumentId))
        {
            await Unmount();
            _current = null;
            if (PageId is not null) Nav.NavigateTo(_kind == "journal" ? "/journal" : "/notebook");
            else await Load();
        }
        else await RefreshTree();
        _notice = "Siden er flyttet til papirkurven.";
        await InvokeAsync(StateHasChanged);
        return true;
    }
    private Task<TrashedNotebookPage[]> ListTreeTrash() => _notebookId is Guid id ? Client.Trash(id, TreeToken) : Task.FromResult<TrashedNotebookPage[]>([]);
    private async Task RestoreTreePage(Guid id)
    {
        if (!Own) return;
        await Client.RestorePage(id, TreeToken);
        await RefreshTree();
        _notice = "Siden er gendannet.";
        await InvokeAsync(StateHasChanged);
    }
    private async Task RefreshTree()
    {
        if (_notebookId is not Guid id) return;
        var ct = TreeToken;
        var generation = _generation;
        var query = (_search, _tag, _kind);
        var pagesTask = Client.Pages(id, _search, _tag, _kind, 0, ct);
        var treeTask = Client.Tree(id, ct);
        await Task.WhenAll(pagesTask, treeTask);
        if (generation != _generation || ct.IsCancellationRequested) return;
        var result = await pagesTask;
        _treePages = await treeTask;
        if (query == (_search, _tag, _kind)) { _pages = result.Items; _total = result.Total; }
        if (_current is not null) _current = _current with { Pages = _treePages };
        if (_mounted && _editor is not null && _current is not null)
            await _editor.InvokeVoidAsync("setContext", _editorElement, EditorContext());
    }
}
