using System.Globalization;
using System.Text.RegularExpressions;

namespace Mercantec.Tech.Contracts.HomeGame;

public static partial class HomeRules
{
    [GeneratedRegex(@"^[a-z][a-z0-9_]{0,47}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex AliasPattern();

    public static HomeBuildResult Apply(
        HomeLayout layout, HomeBuildAction action, HomeCell? avatarCell = null, bool freePurchase = false)
    {
        if (action.Kind is "floorStyle" or "wallpaperStyle")
        {
            var floor = action.Kind == "floorStyle";
            var styles = floor ? HomeCatalog.FloorStyles : HomeCatalog.WallpaperStyles;
            if (action.Value is null || !styles.ContainsKey(action.Value))
                return Reject("INVALID_STYLE", "Vælg en overflade fra udvalget.");
            var decorated = floor ? layout with { FloorStyle = action.Value } : layout with { WallpaperStyle = action.Value };
            return new(decorated with { Revision = checked(layout.Revision + 1) }, 0, null);
        }
        var items = layout.Items.ToList();
        var goldDelta = 0;
        if (action.Kind == "buy")
        {
            var definition = HomeCatalog.Definition(action.DefinitionId);
            if (definition is not { Purchasable: true })
                return Reject("INVALID_ITEM", "Genstanden findes ikke i butikken.");
            if (action.Placement is null)
                return Reject("INVALID_PLACEMENT", "Vælg en placering før du køber.");
            var alias = AvailableAlias(items, definition.Id);
            var price = freePurchase ? 0 : definition.Price;
            items.Add(new(Guid.NewGuid().ToString("N"), definition.Id, alias, definition.Name,
                action.Placement, GoldPaid: price));
            goldDelta = -price;
        }
        else
        {
            var index = items.FindIndex(item => item.Id == action.ItemId);
            if (index < 0) return Reject("ITEM_NOT_FOUND", "Genstanden er ikke i dit hjem eller på dit lager.");
            var item = items[index];
            var definition = HomeCatalog.Definition(item.DefinitionId);
            if (definition is null) return Reject("INVALID_ITEM", "Genstandstypen findes ikke.");
            switch (action.Kind)
            {
                case "place":
                case "move":
                case "rotate":
                    if (action.Placement is null)
                        return Reject("INVALID_PLACEMENT", "Vælg en placering til genstanden.");
                    items[index] = item with { Placement = action.Placement };
                    break;
                case "store":
                    items[index] = item with { Placement = null };
                    StoreMounted(items, item.Id);
                    break;
                case "sell":
                    if (item.Starter) return Reject("STARTER_ITEM", "Startudstyr kan lægges på lager.");
                    goldDelta = item.GoldPaid;
                    items.RemoveAt(index);
                    StoreMounted(items, item.Id);
                    break;
                case "alias":
                    if (!ValidAlias(action.Value))
                        return Reject("INVALID_ALIAS", "Brug små bogstaver, tal og understregning, og begynd med et bogstav.");
                    if (items.Any(other => other.Id != item.Id && other.Alias == action.Value))
                        return Reject("ALIAS_IN_USE", "Det kodealias bruges allerede i dit hjem eller på lager.");
                    items[index] = item with { Alias = action.Value! };
                    break;
                case "rename":
                    var name = action.Value?.Trim();
                    if (name is not { Length: > 0 and <= 60 } || name.Any(char.IsControl))
                        return Reject("INVALID_NAME", "Skriv et navn på op til 60 tegn.");
                    items[index] = item with { Name = name };
                    break;
                default:
                    return Reject("INVALID_ACTION", "Den handling findes ikke.");
            }
        }
        var next = layout with { Revision = checked(layout.Revision + 1), Items = NormalizeMounts(items) };
        if (Validate(next, avatarCell) is { } error) return new(null, 0, error);
        if (action.Kind is ("buy" or "place" or "move" or "rotate") && action.Placement?.Wall == HomeCatalog.WindowEdge)
        {
            var mounted = next.Items.Single(item => item.Placement?.Wall == HomeCatalog.WindowEdge);
            // Older homes could mount an ordinary wall device at this edge. Keep it until the owner moves it.
            if (mounted.DefinitionId != HomeCatalog.BlindId &&
                !layout.Items.Any(item => item.Id == mounted.Id && item.Placement?.Wall == HomeCatalog.WindowEdge))
                return Reject("INVALID_MOUNT", "Vinduet er til rullegardinet. Vælg et frit vægstykke.");
        }
        return new(next, goldDelta, null);
    }

    public static bool ValidAlias(string? alias) => alias is { Length: > 0 and <= 48 } && AliasPattern().IsMatch(alias);

    private static HomeBuildResult Reject(string code, string message) => new(null, 0, new(code, message, []));

    private static string AvailableAlias(IEnumerable<HomeItem> items, string basis)
    {
        var aliases = items.Select(item => item.Alias).ToHashSet(StringComparer.Ordinal);
        if (!aliases.Contains(basis)) return basis;
        for (var number = 2; ; number++)
        {
            var alias = basis + "_" + number.ToString(CultureInfo.InvariantCulture);
            if (!aliases.Contains(alias)) return alias;
        }
    }

    private static void StoreMounted(List<HomeItem> items, string hostId)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Placement?.HostId == hostId)
                items[index] = items[index] with { Placement = null };
        }
    }

    private static IReadOnlyList<HomeItem> NormalizeMounts(IReadOnlyList<HomeItem> items)
    {
        return items.Select(item =>
        {
            if (item.Placement is not { } placement || HomeCatalog.Definition(item.DefinitionId)?.Layer != "surface")
                return item;
            var host = items.FirstOrDefault(candidate => candidate.Id == placement.HostId);
            return host is not null && PortPlacement(host, placement.PortId) is { } canonical
                ? item with { Placement = canonical }
                : item;
        }).ToArray();
    }

    public static HomePlacement? PortPlacement(HomeItem host, string? portId)
    {
        if (host.Placement is not { Rotation: 0 or 90 or 180 or 270 } hostPlacement ||
            HomeCatalog.Definition(host.DefinitionId) is not { Layer: "floor", Ports: { } ports } definition ||
            portId is null || !ports.TryGetValue(portId, out var port)) return null;
        return new(hostPlacement.Anchor + Rotate(port, definition.Width, definition.Height, hostPlacement.Rotation),
            hostPlacement.Rotation, HostId: host.Id, PortId: portId);
    }

    public static bool AcceptsPort(string definitionId, HomeItem host, string? portId) =>
        PortPlacement(host, portId) is not null &&
        (definitionId == HomeCatalog.MoistureId ? host.DefinitionId == HomeCatalog.PlanterId && portId == "soil"
            : host.DefinitionId != HomeCatalog.PlanterId);

    public static HomeRuleError? Validate(HomeLayout layout, HomeCell? avatarCell = null)
    {
        var cells = new HashSet<(string Layer, HomeCell Cell)>();
        var wallSlots = new HashSet<HomeEdge>();
        var surfaceSlots = new HashSet<(string Host, string Port)>();
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var closedEdges = ClosedEdges(layout.Grid);

        foreach (var item in layout.Items)
        {
            if (!ValidAlias(item.Alias) || !aliases.Add(item.Alias))
                return new("INVALID_ALIAS", "Hver genstand i hjemmet og på lager skal have sit eget gyldige kodealias.", []);
            if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id) ||
                HomeCatalog.Definition(item.DefinitionId) is not { } definition)
                return new("INVALID_ITEM", "Boligen indeholder en ugyldig genstand.", []);
            if (item.Placement is not { } placement) continue;
            if (placement.Rotation is not (0 or 90 or 180 or 270))
                return new("INVALID_ROTATION", "Vælg 0, 90, 180 eller 270 grader.", [placement.Anchor]);
            if (!OnFloor(layout.Grid, placement.Anchor))
                return new("OUT_OF_BOUNDS", "Placeringen ligger uden for boligen.", [placement.Anchor]);

            if (definition.Layer == "wall")
            {
                if (placement.Wall is not { } wall || !closedEdges.Contains(wall) ||
                    WallRotation(placement.Anchor, wall) != placement.Rotation ||
                    placement.HostId is not null || placement.PortId is not null)
                    return new("INVALID_MOUNT", "Vælg en vægkant, og lad enheden vende ind i rummet.", [placement.Anchor]);
                if (!wallSlots.Add(wall))
                    return new("OCCUPIED", "Vægpladsen har allerede en enhed.", [placement.Anchor]);
                if (item.DefinitionId == HomeCatalog.BlindId && wall != HomeCatalog.WindowEdge)
                    return new("INVALID_MOUNT", "Montér rullegardinet på vinduet.", [placement.Anchor]);
                continue;
            }

            if (definition.Layer == "surface")
            {
                var host = layout.Items.FirstOrDefault(candidate => candidate.Id == placement.HostId);
                if (host is null || !AcceptsPort(item.DefinitionId, host, placement.PortId) ||
                    PortPlacement(host, placement.PortId) != placement)
                    return new("INVALID_MOUNT", "Vælg en monteringsport på et placeret møbel.", [placement.Anchor]);
                if (!surfaceSlots.Add((host.Id, placement.PortId!)))
                    return new("OCCUPIED", "Monteringsporten har allerede en enhed.", [placement.Anchor]);
                continue;
            }

            if (placement.Wall is not null || placement.HostId is not null || placement.PortId is not null)
                return new("INVALID_MOUNT", "Placér gulvgenstanden direkte på gulvet.", [placement.Anchor]);
            var footprint = Footprint(item);
            var outside = footprint.Where(cell => !OnFloor(layout.Grid, cell)).ToArray();
            if (outside.Length > 0)
                return new("OUT_OF_BOUNDS", "En del af genstanden er uden for boligen.", outside);
            foreach (var cell in footprint)
            {
                if (!cells.Add((definition.Layer, cell)))
                    return new("OCCUPIED", "En anden genstand optager allerede feltet på samme lag.", [cell]);
                foreach (var direction in Directions.Take(2))
                {
                    var adjacent = cell + direction;
                    if (footprint.Contains(adjacent) && closedEdges.Contains(EdgeBetween(cell, adjacent)))
                        return new("CROSSES_WALL", "Genstanden krydser en fast væg.", [cell, adjacent]);
                }
            }
        }

        var reachable = Reachable(layout).ToHashSet();
        if (reachable.Count == 0)
            return new("BLOCKED_ACCESS", "Hold indgangen fri.", [layout.Grid.Entry]);
        if (avatarCell is { } avatar && !reachable.Contains(avatar))
            return new("BLOCKED_ACCESS", "Placeringen spærrer for avataren eller vejen tilbage til indgangen.",
                [avatar, layout.Grid.Entry]);
        foreach (var item in layout.Items.Where(item => item.Placement is not null))
        {
            var interactionCells = Interactions(item);
            if (interactionCells.Count > 0 && !AccessibleInteractions(layout, item).Any(reachable.Contains))
                return new("BLOCKED_ACCESS", $"Avataren skal kunne komme frem til {item.Name}.", interactionCells);
        }
        return null;
    }

    public static HomeRuleError? AssessmentPrerequisites(HomeLayout layout, HomeTask task)
    {
        if (!layout.Items.Any(item => item.DefinitionId == HomeCatalog.ControllerId && item.Placement is not null))
            return new("CONTROLLER_NOT_INSTALLED", "Placér hjemmecontrolleren fra lageret.", []);
        var desk = layout.Items.FirstOrDefault(item => item.Role == HomeCatalog.PrimaryDeskRole);
        if (desk?.Placement is not { } deskPlacement)
            return new("DEVICE_NOT_INSTALLED", "Placér dit skrivebord fra lageret.", []);
        if (task.Id == "P02" && (deskPlacement.Anchor != new HomeCell(2, 6) || deskPlacement.Rotation != 0))
            return new("GOAL_PLACEMENT", "Placér skrivebordet på (2, 6) med rotation 0°.", [new(2, 6), new(3, 6)]);
        foreach (var definition in task.RequiredDefinitions)
        {
            if (!layout.Items.Any(item => item.DefinitionId == definition && item.Placement is not null))
                return new("DEVICE_NOT_INSTALLED", $"Placér {HomeCatalog.Definition(definition)?.Name} før vurdering.", []);
        }
        return null;
    }
}
