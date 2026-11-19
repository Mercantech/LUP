using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Components;

public sealed record NotebookTreeItem(Guid Id, string Title, Guid? ParentId, Guid? ScopeId = null, Guid? IconEmojiId = null);
public sealed record NotebookTreeTarget(Guid Id, int Index);

public static class NotebookTreeUi
{
    public static NotebookTreeTarget? KeyboardTarget<T>(List<T> rows, Guid? current, string key,
        Func<T, Guid> id, Func<T, Guid?> parent, List<T>? virtualRows = null)
    {
        if (rows.Count == 0) return null;
        var index = current.HasValue ? rows.FindIndex(row => id(row) == current) : -1;
        var target = key switch
        {
            "Home" => 0,
            "End" => rows.Count - 1,
            "ArrowUp" when index > 0 => index - 1,
            "ArrowDown" when index >= 0 => index + 1,
            "ArrowLeft" when index >= 0 && parent(rows[index]) is Guid parentId
                => rows.FindIndex(row => id(row) == parentId),
            "ArrowRight" when index >= 0 && index + 1 < rows.Count && parent(rows[index + 1]) == current
                => index + 1,
            _ => -1
        };
        if (target < 0 || target >= rows.Count) return null;
        var row = rows[target];
        return new(id(row), virtualRows?.IndexOf(row) ?? -1);
    }

    public static HashSet<Guid> Branch(IEnumerable<NotebookTreeItem> items, Guid root)
    {
        var children = items.ToLookup(p => p.ParentId);
        var branch = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(root);
        while (pending.TryPop(out var id))
            if (branch.Add(id))
                foreach (var child in children[id]) pending.Push(child.Id);
        return branch;
    }

    public static MoveNotebookPage? Placement(NotebookTreeItem[] items, Guid page, Guid? target, string position)
    {
        if (position == "root") return new(null);
        var source = items.FirstOrDefault(p => p.Id == page);
        var destination = items.FirstOrDefault(p => p.Id == target);
        if (source is null || destination is null || source.ScopeId != destination.ScopeId || Branch(items, page).Contains(destination.Id))
            return null;
        return position switch
        {
            "before" => new(destination.ParentId, BeforePageId: destination.Id),
            "after" => new(destination.ParentId, AfterPageId: destination.Id),
            _ => new(destination.Id)
        };
    }
}
