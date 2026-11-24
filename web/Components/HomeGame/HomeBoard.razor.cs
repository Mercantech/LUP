using Mercantec.Tech.Contracts.HomeGame;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.HomeGame;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Components.HomeGame;

public partial class HomeBoard
{
    [Inject] private DemoWorld World { get; set; } = default!;
    [Parameter, EditorRequired] public HomeLayout Layout { get; set; } = default!;
    [Parameter] public HomeWorldSnapshot? Snapshot { get; set; }
    [Parameter] public HomeCell? Avatar { get; set; }
    [Parameter] public string? AvatarId { get; set; }
    [Parameter] public string AvatarActivity { get; set; } = "idle";
    [Parameter] public string ResidentSource { get; set; } = "room";
    [Parameter] public int WalkSpeed { get; set; } = 1;
    [Parameter] public HomeItem? Selected { get; set; }
    [Parameter] public HomeItem? Ghost { get; set; }
    [Parameter] public HomeCell Cursor { get; set; }
    [Parameter] public string Mode { get; set; } = "normal";
    [Parameter] public int Zoom { get; set; } = 100;
    [Parameter] public bool InvalidPreview { get; set; }
    [Parameter] public bool ReduceMotion { get; set; }
    [Parameter] public bool ShowDevices { get; set; }
    [Parameter] public EventCallback<HomeCell> Visit { get; set; }
    [Parameter] public EventCallback<HomeCell> Activate { get; set; }
    [Parameter] public EventCallback<HomeItem> Inspect { get; set; }
    [Parameter] public bool ShowItemActions { get; set; }
    [Parameter] public RenderFragment<HomeItem>? ItemActions { get; set; }
    [Parameter] public EventCallback DismissActions { get; set; }
    [Parameter] public RenderFragment? PlacementActions { get; set; }
    [Parameter] public EventCallback<HomeEdge> ActivateWall { get; set; }
    [Parameter] public EventCallback Turn { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }

    private AvatarDefinition Character => AvatarCatalog.Find(AvatarId) ??
        (World.AvatarProfileReady ? World.SelectedAvatar : AvatarCatalog.All[0]);
    private string Animation => AvatarActivity switch
    {
        "walking" => "walk",
        "reading" or "relaxing" => "sit",
        "resting" => "sleep",
        "lookingOutside" => "think",
        "visitingPlant" => "interact",
        _ => "idle"
    };
    private string _facing = "right";
    private bool _animateStep;
    private HomeCell? _lastAvatar;
    private string? _lastResidentSource;

    private readonly Dictionary<HomeCell, HomeItem> _occupants = [];
    private HashSet<HomeCell> _range = [];
    private HashSet<HomeCell> _ghost = [];
    private HashSet<HomeCell> _selected = [];
    private HashSet<HomeCell> _blocked = [];
    private HashSet<HomeCell> _reachable = [];
    private readonly HashSet<HomeCell> _interactions = [];
    private Dictionary<HomeCell, int> _light = [];
    private HomeLayout? _previousLayout;
    private HomeWorldSnapshot? _previousSnapshot;
    private string? _previousMode;
    private bool _paintPending;
    private (HomeCell Cell, int Lux)? _reading;
    private ElementReference _board;
    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private IJSObjectReference? _lighting;

    private bool MountingWall => Ghost is not null && HomeCatalog.Definition(Ghost.DefinitionId)?.Layer == "wall";

    private IEnumerable<HomeEdge> MountableWalls => Layout.Grid.Walls
        .Where(boundary => !boundary.Open).Select(boundary => boundary.Edge)
        .Where(edge => (edge == HomeCatalog.WindowEdge) == (Ghost?.DefinitionId == HomeCatalog.BlindId));

    private static bool IsOnBackWall(HomeItem item) => item.Placement?.Wall is { Horizontal: true, Y: 0 };

    private HomeCell WallCell(HomeEdge edge)
    {
        var inside = new HomeCell(edge.X, edge.Y);
        if (HomeRules.OnFloor(Layout.Grid, inside)) return inside;
        return edge.Horizontal ? new(edge.X, edge.Y - 1) : new(edge.X - 1, edge.Y);
    }

    private static string WallFace(HomeEdge edge, HomeCell cell) =>
        edge.Horizontal ? (edge.Y == cell.Y ? "back" : "front") : (edge.X == cell.X ? "left" : "right");

    private Task ChooseWallAsync(HomeEdge edge) => ActivateWall.HasDelegate
        ? ActivateWall.InvokeAsync(edge) : Activate.InvokeAsync(WallCell(edge));

    private Task InspectItemAsync(HomeItem item) => Inspect.HasDelegate
        ? Inspect.InvokeAsync(item) : Activate.InvokeAsync(item.Placement!.Anchor);

    private static bool IsProgrammable(HomeItem item) => HomeCatalog.Definition(item.DefinitionId)?.Kind is
        "lamp" or "blind" or "sign" or "pump" or "moisture" or "daylight" or "presence" or "switch" or "indoorLight";

    private IEnumerable<HomeItem> VisibleItems
    {
        get
        {
            foreach (var item in Layout.Items)
            {
                if (item.Placement is not { } placement || item.Id == Ghost?.Id) continue;
                if (Ghost is { } host && placement.HostId == host.Id &&
                    HomeRules.PortPlacement(host, placement.PortId) is { } mounted)
                    yield return item with { Placement = mounted };
                else yield return item;
            }
            if (Ghost?.Placement is not null) yield return Ghost;
        }
    }

    private string GridStyle
    {
        get
        {
            var shade = Snapshot is null ? 0 : .24 * Math.Clamp(1 - Snapshot.AmbientLux / 350d, 0, 1);
            return FormattableString.Invariant($"--columns:{Layout.Grid.Columns};--rows:{Layout.Grid.Rows};--floor-aspect:{Layout.Grid.Columns / (Layout.Grid.Rows * .70):F4};--shade:{shade:F3};width:{Zoom}%");
        }
    }

    protected override void OnInitialized() => World.Changed += OnWorldChanged;

    private void OnWorldChanged()
    {
        if (AvatarId is null) _ = InvokeAsync(StateHasChanged);
    }

    protected override void OnParametersSet()
    {
        if (_lastAvatar != Avatar || _lastResidentSource != ResidentSource)
        {
            _animateStep = _lastResidentSource == ResidentSource && ResidentSource != "replay"
                && _lastAvatar is { } from && Avatar is { } to && Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y) == 1;
            if (_animateStep && Avatar is { } current && _lastAvatar is { } previous && current.X != previous.X)
                _facing = current.X < previous.X ? "left" : "right";
            _lastAvatar = Avatar;
            _lastResidentSource = ResidentSource;
        }
        if (!ReferenceEquals(Layout, _previousLayout) || Mode != _previousMode)
        {
            _occupants.Clear();
            _interactions.Clear();
            _blocked = Mode == "walk" ? HomeRules.WalkBlockers(Layout).ToHashSet() : [];
            _reachable = Mode == "walk" ? HomeRules.Reachable(Layout).ToHashSet() : [];
            foreach (var item in Layout.Items.Where(item => item.Placement is not null)
                         .OrderBy(item => item.DefinitionId == HomeCatalog.RugId ? 0 : 1))
            {
                foreach (var cell in Cells(item)) _occupants[cell] = item;
                if (Mode == "walk") _interactions.UnionWith(HomeRules.Interactions(item).Where(_reachable.Contains));
            }
        }
        if (!ReferenceEquals(Layout, _previousLayout) || !ReferenceEquals(Snapshot, _previousSnapshot) || Mode != _previousMode)
        {
            _light = Snapshot?.LightMap.ToDictionary(reading => reading.Cell, reading => reading.Lux) ?? [];
            _reading = HomeReadingPoint.Measure(Layout, Snapshot);
            _paintPending = true;
        }
        _previousLayout = Layout;
        _previousSnapshot = Snapshot;
        _previousMode = Mode;
        _range = Mode == "range" && Selected?.Placement is not null ? HomeRules.Range(Layout, Selected).ToHashSet() : [];
        _ghost = Ghost?.Placement is null ? [] : Cells(Ghost).ToHashSet();
        _selected = Selected?.Placement is null ? [] : Cells(Selected).ToHashSet();
    }

    private static IReadOnlyCollection<HomeCell> Cells(HomeItem item)
    {
        var cells = HomeRules.Footprint(item);
        return cells.Count == 0 && item.Placement is { } placement ? [placement.Anchor] : cells;
    }

    private static string DeviceMark(string definition) => definition switch
    {
        HomeCatalog.DaylightId => "☀",
        HomeCatalog.PresenceId => "◎",
        HomeCatalog.ControllerId => "⌁",
        _ => ""
    };

    private string ObjectStyle(HomeItem item)
    {
        // A probe is embedded in the pot. A rotating two-cell planter can move
        // its soil port to the far tile, so draw both against the host's bounds.
        HomeItem? host = null;
        if (item.DefinitionId == HomeCatalog.MoistureId && item.Placement?.HostId is { } hostId)
            host = Ghost?.Id == hostId ? Ghost : Layout.Items.FirstOrDefault(candidate => candidate.Id == hostId);
        var cells = Cells(host ?? item);
        var x = cells.Min(cell => cell.X);
        var y = cells.Min(cell => cell.Y);
        var bottom = cells.Max(cell => cell.Y);
        var width = cells.Max(cell => cell.X) - x + 1;
        var height = bottom - y + 1;
        var layerOffset = item.Placement!.HostId is null ? 0 : 1;
        return $"{CellPosition(new(x, y))};--w:{width};--h:{height};--depth:{10 + bottom * 2 + layerOffset};--angle:{item.Placement.Rotation}deg";
    }

    private static string CellPosition(HomeCell cell) => $"--x:{cell.X};--y:{cell.Y}";
    private string ResidentStyle(HomeCell cell) => $"{CellPosition(cell)};--depth:{11 + cell.Y * 2};--step-ms:{460 / Math.Max(1, WalkSpeed)}ms";

    private string CellClass(HomeCell cell)
    {
        var classes = new List<string> { "tile" };
        if (_selected.Contains(cell)) classes.Add("chosen");
        if (_range.Contains(cell)) classes.Add("in-range");
        if (_ghost.Contains(cell)) classes.Add(InvalidPreview ? "bad-preview" : "good-preview");
        if (Mode == "walk")
        {
            if (_blocked.Contains(cell)) classes.Add("blocked");
            else if (!_reachable.Contains(cell)) classes.Add("unreachable");
            else if (_interactions.Contains(cell)) classes.Add("interaction");
        }
        return string.Join(' ', classes);
    }

    private string CellLabel(HomeCell cell, HomeItem? item, int? lux)
    {
        var details = new List<string> { $"Felt {cell.X}, {cell.Y}" };
        if (item is not null) details.Add(item.Name);
        if (cell == Avatar) details.Add($"Din avatar: {Character.Name}");
        if (cell == Layout.Grid.Entry) details.Add("Indgang");
        if (cell == WallCell(HomeCatalog.WindowEdge)) details.Add("Vindue på bagvæggen");
        if (Mode == "light" && lux is not null) details.Add($"{lux} lux");
        if (_range.Contains(cell)) details.Add("Inden for rækkevidde");
        if (Mode == "walk" && _blocked.Contains(cell)) details.Add("Blokeret");
        if (Mode == "walk" && !_blocked.Contains(cell) && !_reachable.Contains(cell)) details.Add("Ingen adgang fra indgangen");
        if (Mode == "walk" && _interactions.Contains(cell)) details.Add("Interaktionsfelt");
        return string.Join(". ", details);
    }

    private bool IsPowered(HomeItem item) => Snapshot?.PoweredAliases.Contains(item.Alias) == true;

    private string PowerAttribute(HomeItem item) => IsPowered(item) ? "true" : "false";

    private string ObjectClass(HomeItem item) => string.Join(' ', new[]
    {
        item.DefinitionId,
        item.Id == Selected?.Id ? "selected" : "",
        IsPowered(item) ? "powered" : "",
        Ghost is not null && (item.Id == Ghost.Id || item.Placement?.HostId == Ghost.Id) ? "ghost" : ""
    });

    private int? Moisture(HomeItem item)
    {
        if (item.DefinitionId == HomeCatalog.PlanterId && Snapshot?.SoilMoisture is { } levels
            && levels.TryGetValue(item.Alias, out var level)) return level;
        if (item.DefinitionId == HomeCatalog.MoistureId && Snapshot?.Sensors.FirstOrDefault(sensor =>
                sensor.Alias == item.Alias)?.Value is { ValueKind: System.Text.Json.JsonValueKind.Number } value
            && value.TryGetInt32(out var reading)) return reading;
        return null;
    }

    private string ObjectLabel(HomeItem item)
    {
        var state = item.DefinitionId switch
        {
            HomeCatalog.BlindId => IsPowered(item) ? "Åbent" : "Lukket",
            HomeCatalog.LampId => IsPowered(item) ? "Tændt" : "Slukket",
            HomeCatalog.SignId => IsPowered(item) ? "Sol" : "Måne",
            HomeCatalog.PlanterId => IsPowered(item) ? "Vander" : "Pumpe slukket",
            HomeCatalog.MoistureId when Moisture(item) is int percent => $"{percent} procent jordfugt",
            _ => null
        };
        return state is null ? item.Name : $"{item.Name}. {state}";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await Js.InvokeAsync<IJSObjectReference>("import", HomeWorkshop.ModulePath);
            await _module.InvokeVoidAsync("keepArrowKeysOnBoard", _board);
            _lighting = await Js.InvokeAsync<IJSObjectReference>("import", "./js/home-room-light.js");
        }
        if (_paintPending && _lighting is not null)
        {
            _paintPending = false;
            var samples = _light.Count == 0 ? [] : Enumerable.Range(0, Layout.Grid.Rows * Layout.Grid.Columns)
                .Select(i => _light.GetValueOrDefault(new(i % Layout.Grid.Columns, i / Layout.Grid.Columns), -1)).ToArray();
            await _lighting.InvokeVoidAsync("paintRoomLight", _canvas, samples, Layout.Grid.Columns, Layout.Grid.Rows,
                Snapshot?.AmbientLux ?? 0, Mode == "light");
        }
    }

    private async Task OnKeyAsync(KeyboardEventArgs args, HomeCell cell)
    {
        if (args.Key is "r" or "R") { await Turn.InvokeAsync(); return; }
        if (args.Key == "Escape") { await Cancel.InvokeAsync(); return; }
        var next = args.Key switch
        {
            "ArrowUp" => cell with { Y = Math.Max(0, cell.Y - 1) },
            "ArrowRight" => cell with { X = Math.Min(Layout.Grid.Columns - 1, cell.X + 1) },
            "ArrowDown" => cell with { Y = Math.Max(0, cell.Y - 1) },
            "ArrowLeft" => cell with { X = Math.Max(0, cell.X - 1) },
            _ => cell
        };
        if (next == cell) return;
        await Visit.InvokeAsync(next);
        if (_module is not null) await _module.InvokeVoidAsync("focusCell", _board, next.X, next.Y);
    }

    public async ValueTask DisposeAsync()
    {
        World.Changed -= OnWorldChanged;
        if (_module is not null) await _module.DisposeAsync();
        if (_lighting is not null) await _lighting.DisposeAsync();
    }
}
