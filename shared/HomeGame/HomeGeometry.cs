namespace Mercantec.Tech.Contracts.HomeGame;

public static partial class HomeRules
{
    // These offsets also define BFS tie-breaking.
    private static readonly HomeCell[] Directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    public static bool OnFloor(HomeGrid grid, HomeCell cell) =>
        cell.X >= 0 && cell.X < grid.Columns && cell.Y >= 0 && cell.Y < grid.Rows
        && grid.Rooms.Values.Any(cells => cells.Contains(cell));

    public static IEnumerable<HomeCell> FloorCells(HomeGrid grid) =>
        grid.Rooms.Values.SelectMany(cells => cells).OrderBy(c => c.Y).ThenBy(c => c.X);

    public static HomeCell Rotate(HomeCell offset, int width, int height, int rotation) => rotation switch
    {
        0 => offset,
        90 => new(height - 1 - offset.Y, offset.X),
        180 => new(width - 1 - offset.X, height - 1 - offset.Y),
        270 => new(offset.Y, width - 1 - offset.X),
        _ => throw new ArgumentOutOfRangeException(nameof(rotation))
    };

    public static IReadOnlyList<HomeCell> Footprint(HomeItem item)
    {
        if (item.Placement is not { } placement || HomeCatalog.Definition(item.DefinitionId) is not { } definition)
            return [];
        if (placement.Rotation is not (0 or 90 or 180 or 270)) return [];
        return Enumerable.Range(0, definition.Height)
            .SelectMany(y => Enumerable.Range(0, definition.Width).Select(x => new HomeCell(x, y)))
            .Select(offset => placement.Anchor + Rotate(offset, definition.Width, definition.Height, placement.Rotation))
            .ToArray();
    }

    public static IReadOnlyList<HomeCell> Interactions(HomeItem item)
    {
        if (item.Placement is not { } placement ||
            HomeCatalog.Definition(item.DefinitionId) is not { InteractionCells: { } interactions } definition)
            return [];
        if (placement.Rotation is not (0 or 90 or 180 or 270)) return [];
        return interactions
            .Select(offset => placement.Anchor + Rotate(offset, definition.Width, definition.Height, placement.Rotation))
            .ToArray();
    }

    public static HomeEdge EdgeBetween(HomeCell a, HomeCell b)
    {
        if (a.X == b.X) return new(a.X, Math.Max(a.Y, b.Y), true);
        return new(Math.Max(a.X, b.X), a.Y, false);
    }

    public static int? WallRotation(HomeCell inside, HomeEdge wall)
    {
        if (wall.Horizontal && wall.X == inside.X)
        {
            if (wall.Y == inside.Y) return 180;
            if (wall.Y == inside.Y + 1) return 0;
        }
        if (!wall.Horizontal && wall.Y == inside.Y)
        {
            if (wall.X == inside.X) return 90;
            if (wall.X == inside.X + 1) return 270;
        }
        return null;
    }

    public static HomeEdge? FindWall(HomeLayout layout, HomeCell inside, int? rotation = null)
    {
        if (!OnFloor(layout.Grid, inside)) return null;
        foreach (var direction in Directions)
        {
            var edge = EdgeBetween(inside, inside + direction);
            if (rotation is { } requested && WallRotation(inside, edge) != requested) continue;
            if (layout.Grid.Walls.Any(wall => wall.Edge == edge && !wall.Open)) return edge;
        }
        return null;
    }

    public static IReadOnlySet<HomeCell> WalkBlockers(HomeLayout layout) =>
        Blockers(layout, definition => definition.BlocksWalk);

    public static IReadOnlySet<HomeCell> SightBlockers(HomeLayout layout, bool presence = false) =>
        Blockers(layout, definition => presence ? definition.BlocksPresence : definition.BlocksLight);

    private static IReadOnlySet<HomeCell> Blockers(HomeLayout layout, Func<HomeItemDefinition, bool> blocks)
    {
        return layout.Items
            .Where(item => item.Placement is not null &&
                HomeCatalog.Definition(item.DefinitionId) is { } definition && blocks(definition))
            .SelectMany(Footprint)
            .ToHashSet();
    }

    public static IReadOnlySet<HomeEdge> ClosedEdges(HomeGrid grid) =>
        grid.Walls.Where(wall => !wall.Open).Select(wall => wall.Edge).ToHashSet();

    public static IReadOnlyList<HomeCell> Reachable(HomeLayout layout, HomeCell? start = null) =>
        Routes(layout, start ?? layout.Grid.Entry).Keys.ToArray();

    private static Dictionary<HomeCell, (HomeCell? Previous, int Distance)> Routes(HomeLayout layout, HomeCell start)
    {
        var blockedCells = WalkBlockers(layout);
        var blockedEdges = ClosedEdges(layout.Grid);
        var routes = new Dictionary<HomeCell, (HomeCell? Previous, int Distance)>();
        if (!OnFloor(layout.Grid, start) || blockedCells.Contains(start)) return routes;
        routes[start] = (null, 0);
        var search = new Queue<HomeCell>();
        search.Enqueue(start);
        while (search.TryDequeue(out var cell))
        {
            foreach (var offset in Directions)
            {
                var next = cell + offset;
                if (routes.ContainsKey(next) || !OnFloor(layout.Grid, next) ||
                    blockedCells.Contains(next) || blockedEdges.Contains(EdgeBetween(cell, next))) continue;
                routes[next] = (cell, routes[cell].Distance + 1);
                search.Enqueue(next);
            }
        }
        return routes;
    }

    public static IReadOnlyList<HomeCell>? FindPath(HomeLayout layout, HomeCell start, IEnumerable<HomeCell> goals)
    {
        var routes = Routes(layout, start);
        var target = goals.Where(routes.ContainsKey)
            .OrderBy(cell => routes[cell].Distance).ThenBy(cell => cell.Y).ThenBy(cell => cell.X)
            .Select(cell => (HomeCell?)cell).FirstOrDefault();
        if (target is null) return null;
        var path = new List<HomeCell>();
        for (var cell = target; cell is { } step; cell = routes[step].Previous)
            path.Add(step);
        path.Reverse();
        return path;
    }

    public static IReadOnlyList<HomeCell> AccessibleInteractions(HomeLayout layout, HomeItem item)
    {
        var occupied = Footprint(item);
        var walls = ClosedEdges(layout.Grid);
        return Interactions(item).Where(cell => OnFloor(layout.Grid, cell) && Directions
            .Select(direction => cell + direction)
            .Any(neighbor => occupied.Contains(neighbor) && !walls.Contains(EdgeBetween(cell, neighbor))))
            .ToArray();
    }

    public static int DistanceSquared(HomeCell source, HomeCell target)
    {
        var dx = target.X - source.X;
        var dy = target.Y - source.Y;
        return dx * dx + dy * dy;
    }

    public static bool HasSight(HomeLayout layout, HomeCell source, HomeCell target, bool presence = false) =>
        HasSight(source, target, ClosedEdges(layout.Grid), SightBlockers(layout, presence));

    internal static bool HasSight(
        HomeCell source, HomeCell target, IReadOnlySet<HomeEdge> walls, IReadOnlySet<HomeCell> blockers)
    {
        var dx = target.X - source.X;
        var dy = target.Y - source.Y;
        var mx = Math.Abs(dx);
        var my = Math.Abs(dy);
        var stepX = Math.Sign(dx);
        var stepY = Math.Sign(dy);
        var passedX = 0;
        var passedY = 0;
        var current = source;
        bool Blocked(HomeCell cell) => cell != source && cell != target && blockers.Contains(cell);

        while (current != target)
        {
            // Compare when the next x and y grid boundaries are crossed without float rounding.
            var crossingX = (2 * passedX + 1) * my;
            var crossingY = (2 * passedY + 1) * mx;
            var adjacentX = new HomeCell(current.X + stepX, current.Y);
            var adjacentY = new HomeCell(current.X, current.Y + stepY);
            if (crossingX < crossingY)
            {
                if (walls.Contains(EdgeBetween(current, adjacentX))) return false;
                current = adjacentX;
                passedX++;
            }
            else if (crossingY < crossingX)
            {
                if (walls.Contains(EdgeBetween(current, adjacentY))) return false;
                current = adjacentY;
                passedY++;
            }
            else
            {
                var diagonal = new HomeCell(adjacentX.X, adjacentY.Y);
                if (walls.Contains(EdgeBetween(current, adjacentX)) ||
                    walls.Contains(EdgeBetween(current, adjacentY)) ||
                    walls.Contains(EdgeBetween(adjacentX, diagonal)) ||
                    walls.Contains(EdgeBetween(adjacentY, diagonal)) ||
                    Blocked(adjacentX) || Blocked(adjacentY)) return false;
                current = diagonal;
                passedX++;
                passedY++;
            }
            if (Blocked(current)) return false;
        }
        return true;
    }

    public static IReadOnlyList<HomeCell> Range(HomeLayout layout, HomeItem item)
    {
        if (item.Placement is not { } placement || HomeCatalog.Definition(item.DefinitionId) is not { } definition)
            return [];
        if (definition.Kind == "lamp")
            return LightContribution(layout, item).Keys.ToArray();
        if (definition.Kind != "presence") return [];
        var edges = ClosedEdges(layout.Grid);
        var blockers = SightBlockers(layout, presence: true);
        return FloorCells(layout.Grid).Where(cell =>
            DistanceSquared(placement.Anchor, cell) <= definition.Radius * definition.Radius &&
            HasSight(placement.Anchor, cell, edges, blockers)).ToArray();
    }

    public static int LampLux(HomeItemDefinition lamp, int distanceSquared)
    {
        var radiusSquared = lamp.Radius * lamp.Radius;
        if (radiusSquared <= 0 || distanceSquared >= radiusSquared) return 0;
        var denominator = 100 * radiusSquared;
        var numerator = lamp.NominalLux * lamp.BrightnessPercent * (radiusSquared - distanceSquared);
        return (numerator + denominator / 2) / denominator;
    }

    public static IReadOnlyDictionary<HomeCell, int> LightContribution(HomeLayout layout, HomeItem item)
    {
        if (item.Placement is not { } placement ||
            HomeCatalog.Definition(item.DefinitionId) is not { Kind: "lamp" } definition)
            return new Dictionary<HomeCell, int>();
        var edges = ClosedEdges(layout.Grid);
        var blockers = SightBlockers(layout);
        var light = new Dictionary<HomeCell, int>();
        foreach (var cell in FloorCells(layout.Grid))
        {
            var contribution = LampLux(definition, DistanceSquared(placement.Anchor, cell));
            if (contribution > 0 && HasSight(placement.Anchor, cell, edges, blockers))
                light[cell] = contribution;
        }
        return light;
    }

    public static int Comfort(int lux) => lux switch
    {
        < 300 => 100 * Math.Max(lux, 0) / 300,
        <= 600 => 100,
        _ => Math.Max(0, 100 - (lux - 600 + 3) / 4)
    };
}
