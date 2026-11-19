using System.Globalization;
using System.Text;
using System.Text.Json;
using Mercantec.Tech.Contracts.HomeGame;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.HomeGame;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Components.HomeGame;

public partial class HomeWorkshop : IAsyncDisposable
{
    [Parameter, EditorRequired] public string Subject { get; set; } = "";
    [Parameter] public string? InitialView { get; set; }
    [Inject] private HomeGameClient Client { get; set; } = default!;
    [Inject] private DemoWorld World { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private static readonly string[] Languages = ["python", "csharp", "arduino"];
    private static readonly (string Id, string Name)[] Overlays =
        [("normal", "Normal"), ("range", "Rækkevidde"), ("walk", "Gang og adgang"), ("light", "Målinger")];
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, string> _drafts = new(StringComparer.Ordinal);
    private IJSObjectReference? _module;
    private readonly Dictionary<string, CancellationTokenSource> _autosaves = new(StringComparer.Ordinal);
    private HomeGameState? _state;
    private HomeRunStatus? _run;
    private HomeReplayPlayer? _replay;
    private HomeLayout? _previewLayout;
    private HomeWorldSnapshot? _previewLighting;
    private HomeWorldSnapshot? _measuredFrame;
    private (HomeCell Cell, int Lux)? _roomReading;
    private HomeBuildAction? _placement;
    private HomeBuildRequest? _retryBuild;
    private HomeItem? _ghost;
    private HomeCell _cursor = new(0, 4);
    private string _mode = "test";
    private bool _showDevices;
    private bool _roomCollapsed;
    private HomeSimulation? _manualSimulation;
    private HomeWorldSnapshot? _manualSnapshot;
    private (HomeCell Cell, int Lux)? _beforeLight;
    private string _exampleChoice = "on";
    private ElementReference _roomView;
    private bool _focusPlacement;
    private bool _purchaseSiteSelected;
    private string _shelf = "stock";
    private string _overlay = "light";
    private string _language = "python";
    private string _intent = "auto";
    private int _zoom = 100;
    private int _fontSize = 15;
    private int _speed = 1;
    private int _rotation;
    private string? _selectedId;
    private string? _sellId;
    private string? _previewError;
    private string? _error;
    private string? _notice;
    private string _itemName = "";
    private string _itemAlias = "";
    private string _reflection = "";
    private string _question = "";
    private Guid _helpHold;
    private Guid? _helpOperation;
    private bool _working;
    private bool _conflict;
    private bool _stale;
    private bool _reduceMotion;
    private bool _disposed;
    private long _driverVersion;

    private CancellationToken Token => _lifetime.Token;
    private HomeLayout DisplayLayout => _replay?.Layout ?? _state!.Layout;
    private HomeWorldSnapshot? Snapshot => _replay?.Snapshot ?? (_stale ? null : _run?.Snapshot);
    private HomeWorldSnapshot LightingSnapshot
    {
        get
        {
            if (_manualSnapshot is { } manual) return manual;
            if (Snapshot is { } snapshot) return snapshot;
            if (_previewLighting is null || !ReferenceEquals(_previewLayout, DisplayLayout))
            {
                _previewLayout = DisplayLayout;
                var simulation = new HomeSimulation(DisplayLayout, HomeCatalog.FreeScenario, "room-preview");
                simulation.BeginTick();
                _previewLighting = simulation.Snapshot(includeLightMap: true);
            }
            return _previewLighting;
        }
    }

    private (HomeCell Cell, int Lux)? RoomReading
    {
        get
        {
            var frame = LightingSnapshot;
            if (!ReferenceEquals(_measuredFrame, frame))
            {
                _roomReading = HomeReadingPoint.Measure(DisplayLayout, frame);
                _measuredFrame = frame;
            }
            return _roomReading;
        }
    }

    private HomeItem? Selected => _state?.Layout.Items.FirstOrDefault(item => item.Id == _selectedId);
    private HomeItem? BoardSelection => _replay is null ? Selected : DisplayLayout.Items.FirstOrDefault(item => item.Id == _selectedId);
    private bool HasDesk => DisplayLayout.Items.Any(item => item.Role == HomeCatalog.PrimaryDeskRole && item.Placement is not null);
    private string Alias(string definition, string fallback) =>
        Selected is { } selected && selected.DefinitionId == definition ? selected.Alias :
        DisplayLayout.Items.Where(item => item.DefinitionId == definition).OrderByDescending(item => item.Placement is not null)
            .ThenByDescending(item => item.Starter).Select(item => item.Alias).FirstOrDefault() ?? fallback;
    private string LampAlias => Alias(HomeCatalog.LampId, "desk_lamp");
    private string DaylightAlias => Alias(HomeCatalog.DaylightId, "daylight");
    private string LampExample => HomeCodeGuide.Lamp(_language, LampAlias);
    private string DaylightExample => HomeCodeGuide.Daylight(_language, LampAlias, DaylightAlias);

    private IEnumerable<HomeItemDefinition> ShopItems => HomeCatalog.Items.Where(item => item.Purchasable);
    private IEnumerable<HomeItem> RoomItems => DisplayLayout.Items.Where(item => item.Placement is not null);
    private static bool IsOutput(HomeItem item) => HomeCatalog.IsOutput(HomeCatalog.Definition(item.DefinitionId)?.Kind);
    private static bool IsProgrammable(HomeItem item) => IsOutput(item) || HomeCatalog.Definition(item.DefinitionId)?.Kind is "presence" or "daylight" or "switch" or "moisture" or "indoorLight";
    private static (string On, string Off, string Active, string Inactive) Labels(string definition) => definition switch
    {
        HomeCatalog.BlindId => ("Åbn", "Luk", "Åbent", "Lukket"),
        HomeCatalog.SignId => ("Vis sol", "Vis måne", "Sol", "Måne"),
        HomeCatalog.PlanterId => ("Start pumpen", "Stop pumpen", "Vander", "Pumpe stoppet"),
        _ => ("Tænd", "Sluk", "Tændt", "Slukket")
    };

    private bool IsOn(HomeItem item) => LightingSnapshot.PoweredAliases.Contains(item.Alias);
    private string DeviceState(HomeItem item)
    {
        if (IsOutput(item)) return IsOn(item) ? Labels(item.DefinitionId).Active : Labels(item.DefinitionId).Inactive;
        var frame = item.DefinitionId == HomeCatalog.PresenceId && ResidentSource == "room" && _residentFrame is not null
            ? _residentFrame : LightingSnapshot;
        var reading = frame.Sensors.FirstOrDefault(sensor => sensor.Alias == item.Alias);
        return reading is null ? "" : $"{ValueText(reading.Value)} {reading.Unit}".TrimEnd();
    }

    private HomeItem? RuleSensor(HomeItem output) => output.DefinitionId switch
    {
        HomeCatalog.PlanterId => RoomItems.FirstOrDefault(i => i.DefinitionId == HomeCatalog.MoistureId && i.Placement?.HostId == output.Id),
        HomeCatalog.SignId => RoomItems.FirstOrDefault(i => i.DefinitionId == HomeCatalog.PresenceId),
        HomeCatalog.LampId or HomeCatalog.BlindId => RoomItems.FirstOrDefault(i => i.DefinitionId == HomeCatalog.DaylightId),
        _ => null
    };

    private string DeviceExample(HomeItem item)
    {
        if (!IsOutput(item)) return HomeCodeGuide.SensorExample(_language, item.Alias, item.DefinitionId is HomeCatalog.SwitchId or HomeCatalog.PresenceId);
        if (_exampleChoice == "rule" && RuleSensor(item) is { } sensor)
        {
            var kind = item.DefinitionId switch { HomeCatalog.BlindId => "blind", HomeCatalog.SignId => "sign", HomeCatalog.PlanterId => "pump", _ => "lamp" };
            return HomeCodeGuide.Rule(_language, item.Alias, sensor.Alias, kind);
        }
        return HomeCodeGuide.Output(_language, item.Alias, _exampleChoice != "off");
    }
    private IEnumerable<HomeItem> ShelfItems => DisplayLayout.Items.Where(item => (item.Placement is null) == (_shelf == "stock"));
    private bool HasSwitch => DisplayLayout.Items.Any(item => item.DefinitionId == HomeCatalog.SwitchId && item.Placement is not null);
    private bool HasController => _state?.Layout.Items.Any(item => item.DefinitionId == HomeCatalog.ControllerId && item.Placement is not null) == true;
    private bool IsAdvancing => !_stale && _run?.Run.Status is "running" or "queued" or "compiling";
    private bool IsActive => !_stale && IsUnfinished(_run?.Run.Status);
    private string Source => DraftSource(_language);
    private string Clock => TimeSpan.FromMilliseconds(Snapshot?.TimeMs ?? 0).ToString(@"mm\:ss\.f", CultureInfo.InvariantCulture);
    private string SaveStatus => IsDirty() ? (_working ? "Gemmer ændringer…" : "Lokale ændringer") :
        _state?.Drafts.Any(draft => draft.Language == _language) == true ? "Gemt på LUP" : "Udkast";
    private string PlacementName => _ghost?.Name ?? HomeCatalog.Definition(_placement?.DefinitionId)?.Name ?? Selected?.Name ?? "Genstand";
    private string RunLabel => _stale && IsUnfinished(_run?.Run.Status) ? "Tidligere forsøg" : _run?.Run.Status switch
    {
        "queued" => "I kø",
        "compiling" => "Gør programmet klar",
        "running" => "Kører",
        "paused" => "Pauset",
        "completed" => "Forsøg gennemført",
        "cancelled" => "Forsøg stoppet",
        "expired" => "Forsøget er udløbet",
        "error" => "Forsøget stoppede med en fejl",
        _ => "Klar"
    };

    private static bool IsUnfinished(string? status) => status is "queued" or "compiling" or "running" or "paused";

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => PerformAsync(async () =>
    {
        var first = _state is null;
        _state = await Client.LoadAsync(Subject, Token);
        ResetAudition();
        _conflict = false;
        _reflection = _state.Reflection;
        _helpHold = _state.Classrooms.FirstOrDefault()?.HoldId ?? Guid.Empty;
        if (first)
        {
            if (InitialView == "butik")
            {
                _mode = "build";
                _shelf = "shop";
            }
            else if (!RoomItems.Any()) _mode = "build";
            var suggestedLanguage = _state.Classrooms.FirstOrDefault()?.SuggestedLanguage ?? _state.LastRun?.Language;
            if (suggestedLanguage is not null && Languages.Contains(suggestedLanguage))
                _language = suggestedLanguage;
            _cursor = _state.Layout.Grid.Entry;
            await RestoreDraftsAsync();
        }

        _run = _state.LastRun is { Assessment: false } summary ? await Client.RunAsync(Subject, summary.Id, Token) : null;
        _replay = null;
        _stale = _run is { } run && !MatchesCurrentVersion(run.Run);
        if (_run is { } current)
        {
            _speed = current.Speed;
            if (IsActive)
            {
                _language = current.Run.Language;
                _runIntent = current.Snapshot?.Resident switch
                {
                    { IsAutonomous: true } => "auto",
                    { Want: "leave" } => "away",
                    { Want: "rest" } => "relax",
                    { Want: var want } when IsResidentIntent(want) => want,
                    _ => "read"
                };
                _mode = "test";
            }
        }
        if (IsAdvancing)
        {
            _mode = "test";
            Drive();
        }
        if (first) await OpenResidentRoomAsync();
        else ResetResidentRoom();
        UpdatePreview();
    });

    private bool MatchesCurrentVersion(HomeRunSummary run)
    {
        var state = _state!;
        return run.Revision == state.Layout.Revision && state.Drafts.Any(draft => draft.VersionId == run.ProgramVersionId);
    }

    private async Task PerformAsync(Func<Task> action)
    {
        if (_working || _disposed) return;
        _working = true;
        ClearMessage();
        try
        {
            await action();
        }
        catch (HomeGameRequestException ex)
        {
            _error = ex.Message;
            _conflict = ex.Conflict;
        }
        catch (OperationCanceledException) when (_disposed) { }
        finally { _working = false; }
    }

    private void ClearMessage()
    {
        _error = null;
        _notice = null;
    }

    private string Pressed(string mode) => _mode == mode ? "true" : "false";
    private string ShelfClass(string shelf) => _shelf == shelf ? "secondary on" : "secondary";
    private Task ChooseShelf(string shelf)
    {
        CancelPlacement();
        _shelf = shelf;
        if (shelf != "shop" || _state?.IsTeacherSandbox != false) return Task.CompletedTask;
        return PerformAsync(async () =>
        {
            var current = await Client.LoadAsync(Subject, Token);
            _state = _state! with { Gold = current.Gold };
        });
    }

    private Task ChangeModeAsync(string mode)
    {
        if (mode != "build") CancelPlacement();
        if (mode != "test") _replay = null;
        _mode = mode;
        _overlay = mode == "build" ? "normal" : "light";
        _roomCollapsed = false;
        return Task.CompletedTask;
    }

    private bool CanBuy(HomeItemDefinition definition) => _state!.IsTeacherSandbox || _state.Gold >= definition.Price;

    private string PriceLabel(HomeItemDefinition definition)
    {
        if (_state!.IsTeacherSandbox) return "Tilføj gratis";
        return CanBuy(definition) ? $"{definition.Price} guld" : $"Mangler {definition.Price - _state.Gold} guld";
    }

    private static string SizeLabel(HomeItemDefinition definition) => definition.Id switch
    {
        HomeCatalog.BlindId => "Monteres ved vinduet på bagvæggen",
        HomeCatalog.MoistureId => "Sættes i en plantekasse",
        _ when definition.Layer == "wall" => "Monteres på væg",
        _ => $"{definition.Width} × {definition.Height} felter"
    };
    private string PlacementLabel => _placement is { Kind: "buy", DefinitionId: { } id } && HomeCatalog.Definition(id) is { } definition
        ? _state!.IsTeacherSandbox ? "Placér gratis" : $"Placér · {definition.Price} guld"
        : "Placér";

    private void Select(HomeItem item)
    {
        if (_selectedId != item.Id) _exampleChoice = "on";
        _selectedId = item.Id;
        _itemName = item.Name;
        _itemAlias = item.Alias;
        _sellId = null;
    }

    private async Task ActivateCellAsync(HomeCell cell)
    {
        _purchaseSiteSelected = false;
        VisitCell(cell);
        if (_placement is not null)
        {
            if (_placement.Kind == "buy") _purchaseSiteSelected = _previewError is null;
            else await ConfirmPlacementAsync();
            return;
        }
        var item = DisplayLayout.Items.LastOrDefault(item => item.Placement is { } placed &&
            (placed.Anchor == cell || HomeRules.Footprint(item).Contains(cell)));
        if (item is null) return;
        Select(item);
    }

    private void VisitCell(HomeCell cell)
    {
        if (_purchaseSiteSelected || _cursor == cell) return;
        _cursor = cell;
        UpdatePreview();
    }

    private void StartPurchase(HomeItemDefinition definition)
    {
        _purchaseSiteSelected = false;
        _selectedId = null;
        _rotation = 0;
        _placement = new("buy", DefinitionId: definition.Id);
        _focusPlacement = true;
        _cursor = InitialPlacementCell(definition, _cursor);
        _roomCollapsed = false;
        UpdatePreview();
    }

    private async Task MoveInspectedAsync(HomeItem item)
    {
        await ChangeModeAsync("build");
        _shelf = "room";
        StartPlacement(item);
    }

    private void StartPlacement(HomeItem item)
    {
        _purchaseSiteSelected = false;
        Select(item);
        _rotation = item.Placement?.Rotation ?? 0;
        _cursor = item.Placement?.Anchor ?? InitialPlacementCell(HomeCatalog.Definition(item.DefinitionId)!, _cursor);
        _placement = new("place", item.Id, item.DefinitionId);
        _focusPlacement = true;
        _roomCollapsed = false;
        UpdatePreview();
    }

    private HomeCell InitialPlacementCell(HomeItemDefinition definition, HomeCell current)
    {
        if (definition.Id == HomeCatalog.BlindId) return new(HomeCatalog.WindowEdge.X, HomeCatalog.WindowEdge.Y);
        if (definition.Id == HomeCatalog.MoistureId && RoomItems.FirstOrDefault(i => i.DefinitionId == HomeCatalog.PlanterId &&
            !RoomItems.Any(probe => probe.Placement?.HostId == i.Id && probe.Placement?.PortId == "soil")) is { } host)
            return HomeRules.PortPlacement(host, "soil")?.Anchor ?? current;
        if (definition.Layer == "wall")
        {
            var candidate = HomeRules.FloorCells(DisplayLayout.Grid).FirstOrDefault(cell =>
                HomeRules.FindWall(DisplayLayout, cell) is { } edge && edge != HomeCatalog.WindowEdge &&
                !RoomItems.Any(i => i.Placement?.Wall == edge));
            return candidate;
        }
        return current;
    }

    private Task ActivateWallAsync(HomeEdge edge)
    {
        var grid = DisplayLayout.Grid;
        var cell = new HomeCell(Math.Min(edge.X, grid.Columns - 1), Math.Min(edge.Y, grid.Rows - 1));
        if (_placement is not null && HomeRules.WallRotation(cell, edge) is int rotation)
        {
            _cursor = cell;
            _rotation = rotation;
            UpdatePreview();
        }
        return ActivateCellAsync(cell);
    }

    private bool CanTurnPreview => _placement is { DefinitionId: { } id } && CanRotate(id, _cursor);
    private bool CanRotate(string id, HomeCell anchor)
    {
        var layer = HomeCatalog.Definition(id)?.Layer;
        if (layer == "surface" || id == HomeCatalog.BlindId) return false;
        return layer != "wall" || new[] { 0, 90, 180, 270 }.Count(r => HomeRules.FindWall(DisplayLayout, anchor, r) is not null) > 1;
    }

    private Task RotateItemAsync(HomeItem item)
    {
        StartPlacement(item);
        TurnPreview();
        return ConfirmPlacementAsync();
    }

    private void TurnPreview()
    {
        if (_placement is null) return;
        if (!CanTurnPreview) return;
        var rotation = _ghost?.Placement?.Rotation ?? _rotation;
        if (HomeCatalog.Definition(_placement.DefinitionId)?.Layer == "wall")
        {
            _rotation = new[] { 90, 180, 270 }.Select(delta => (rotation + delta) % 360)
                .FirstOrDefault(r => HomeRules.FindWall(DisplayLayout, _cursor, r) is not null, rotation);
        }
        else _rotation = (rotation + 90) % 360;
        UpdatePreview();
    }

    private void CancelPlacement()
    {
        _purchaseSiteSelected = false;
        _focusPlacement = false;
        _placement = null;
        _ghost = null;
        _previewError = null;
    }

    private void ConfirmSale(HomeItem item) => _sellId = item.Id;

    private void UpdatePreview()
    {
        if (_placement is not { } action || _state is null) return;
        var definition = HomeCatalog.Definition(action.DefinitionId)!;
        HomeEdge? mount = null;
        var rotation = _rotation;
        if (definition.Layer == "wall")
        {
            mount = HomeRules.FindWall(DisplayLayout, _cursor, _rotation) ?? HomeRules.FindWall(DisplayLayout, _cursor);
            rotation = mount is { } wall ? HomeRules.WallRotation(_cursor, wall) ?? rotation : rotation;
        }
        var placement = new HomePlacement(_cursor, rotation, mount);
        if (definition.Id == HomeCatalog.MoistureId && RoomItems.FirstOrDefault(item =>
                item.DefinitionId == HomeCatalog.PlanterId && HomeRules.Footprint(item).Contains(_cursor)) is { } planter)
            placement = HomeRules.PortPlacement(planter, "soil") ?? placement;
        _placement = action with { Placement = placement };
        _ghost = new("preview", definition.Id, "", Selected?.Name ?? definition.Name, placement);
        var result = HomeRules.Apply(DisplayLayout, _placement, avatarCell: IsActive ? AvatarCell : null, freePurchase: _state.IsTeacherSandbox);
        _previewError = result.Error?.Message;
    }

    private Task ConfirmPlacementAsync() => _placement is { } action && _previewError is null
        ? BuildAsync(action) : Task.CompletedTask;

    private Task BuildAsync(HomeBuildAction action) => PerformAsync(async () =>
    {
        if (IsAdvancing)
        {
            await SendControlAsync(new("pause"));
            if (IsAdvancing) return;
        }
        if (_retryBuild?.Action != action || _retryBuild.Revision != _state!.Layout.Revision)
            _retryBuild = new(Guid.NewGuid(), _state!.Layout.Revision, action);
        var previousGold = _state!.Gold;
        var oldItems = _state!.Layout.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        _state = await Client.BuildAsync(Subject, _retryBuild, Token);
        ResetResidentRoom();
        ResetAudition();
        _retryBuild = null;
        _stale = _run is not null;
        _sellId = null;
        CancelPlacement();
        if (action.Kind == "buy" && _state.Layout.Items.FirstOrDefault(item => !oldItems.Contains(item.Id)) is { } bought)
            Select(bought);
        if (action.Kind is "place" or "buy")
        {
            _shelf = "room";
            if (Selected?.DefinitionId is HomeCatalog.LampId or HomeCatalog.PresenceId)
                _overlay = "range";
        }
        if (action.Kind == "store") _shelf = "stock";
        _notice = action.Kind switch
        {
            "buy" when _state.IsTeacherSandbox => "Prøveudstyret er placeret.",
            "buy" => $"Køb og placering gemt. Din saldo er {_state.Gold} guld.",
            "undo" => "Seneste byggehandling fortrudt.",
            "sell" when _state.Gold > previousGold => "Genstanden er solgt. Guldet er tilbage på din LUP-saldo.",
            "sell" => "Genstanden er fjernet fra dit hjem.",
            _ => "Indretningen er gemt."
        };
        if (_state.Gold != previousGold)
            await World.ReloadAvatarProfileAsync();
    });

    private string SavedSource(string language) => _state?.Drafts.FirstOrDefault(draft => draft.Language == language)?.Source
        ?? InitialSource(language);

    private string InitialSource(string language)
    {
        var output = RoomItems.FirstOrDefault(i => i.DefinitionId == HomeCatalog.LampId) ?? RoomItems.FirstOrDefault(IsOutput);
        return output is not null
            ? HomeCodeGuide.Output(language, output.Alias, true)
            : HomeCodeGuide.ReadClock(language);
    }
    private string DraftSource(string language) => _drafts.GetValueOrDefault(language) ?? SavedSource(language);
    private bool IsDirty() => !string.Equals(Source, SavedSource(_language), StringComparison.Ordinal);
    private Task ChooseLanguageAsync(ChangeEventArgs args)
    {
        var language = args.Value?.ToString();
        if (language is not null && Languages.Contains(language))
            _language = language;
        if (IsDirty()) QueueSave();
        return Task.CompletedTask;
    }

    private async Task EditSourceAsync(ChangeEventArgs args) => await EditSourceAsync(args.Value?.ToString() ?? "");

    private async Task EditSourceAsync(string source)
    {
        var language = _language;
        _drafts[language] = source;
        try { await (await ModuleAsync()).InvokeVoidAsync("writeDraft", Token, Subject, language, source); }
        catch (JSException) { }
        QueueSave(language);
    }

    private void QueueSave(string? draftLanguage = null)
    {
        var language = draftLanguage ?? _language;
        if (_autosaves.TryGetValue(language, out var previous))
            previous.Cancel();
        var delay = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _autosaves[language] = delay;
        _ = SaveAfterDelayAsync(delay, language);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource delay, string language)
    {
        try
        {
            await Task.Delay(900, delay.Token);
            while (_working) await Task.Delay(150, delay.Token);
            if (_conflict) return;
            await InvokeAsync(async () =>
            {
                await PerformAsync(() => SaveLanguageAsync(language));
                StateHasChanged();
            });
        }
        catch (OperationCanceledException) when (delay.IsCancellationRequested) { }
        finally
        {
            if (_autosaves.GetValueOrDefault(language) == delay) _autosaves.Remove(language);
            delay.Dispose();
        }
    }

    private Task SaveCurrentAsync() => PerformAsync(() => SaveLanguageAsync(_language));

    private async Task SaveLanguageAsync(string language, bool force = false)
    {
        var source = DraftSource(language);
        if (!force && source == SavedSource(language)) return;
        if (Encoding.UTF8.GetByteCount(source) > HomeGameVersion.MaxSourceBytes)
            throw new HomeGameRequestException("Programmet fylder mere end 128 KiB. Forkort koden, og gem igen.");
        _state = await Client.SaveAsync(Subject, new(_state!.Layout.Revision, language, source), Token);
        if (DraftSource(language) == source)
        {
            _drafts.Remove(language);
            await RemoveDraftAsync(language);
        }
        if (_run?.Run.Language == language || IsActive) _stale = _run is not null;
        _conflict = false;
    }

    private async Task RestoreDraftsAsync()
    {
        try
        {
            var module = await ModuleAsync();
            foreach (var language in Languages)
            {
                var draft = await module.InvokeAsync<string?>("readDraft", Token, Subject, language);
                if (draft is not null && draft != SavedSource(language)) _drafts[language] = draft;
            }
        }
        catch (JSException) { }
    }

    private async Task UseSavedCodeAsync()
    {
        var language = _language;
        await ReloadAsync();
        if (_conflict) return;
        _drafts.Remove(language);
        await RemoveDraftAsync(language);
    }

    private async Task ApplyExampleAsync(string source)
    {
        await ChangeModeAsync("code");
        if (Source == source) return;
        if ((_state!.Drafts.Any(d => d.Language == _language) || _drafts.ContainsKey(_language)) &&
            !await Js.InvokeAsync<bool>("confirm", Token, "Erstat koden i editoren med den valgte starterkode?"))
            return;
        await EditSourceAsync(source);
    }

    private async Task CopySourceAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", Token, Source);
            _notice = "Koden er kopieret.";
        }
        catch (JSException) { _error = "Markér og kopiér koden direkte fra editoren."; }
    }

    private async Task RemoveDraftAsync(string language)
    {
        try { await (await ModuleAsync()).InvokeVoidAsync("removeDraft", Token, Subject, language); }
        catch (JSException) { }
    }

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await Js.InvokeAsync<IJSObjectReference>("import", Token, "./js/home-game.js");

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusPlacement || _placement is null) return;
        _focusPlacement = false;
        try { await (await ModuleAsync()).InvokeVoidAsync("focusPlacement", Token, _roomView, _cursor.X, _cursor.Y); }
        catch (JSException) { }
    }

    private Task SaveReflectionAsync() => PerformAsync(async () =>
    {
        _state = await Client.ReflectAsync(Subject, new(_state!.Layout.Revision, _reflection), Token);
        _notice = "Dine noter er gemt.";
    });

    private Task SendHelpAsync() => PerformAsync(async () =>
    {
        _helpOperation ??= Guid.NewGuid();
        await Client.HelpAsync(Subject, new(_helpOperation.Value, _helpHold, 0, _question), Token);
        _helpOperation = null;
        _question = "";
        _notice = "Dit spørgsmål er sendt til holdets lærer.";
    });

    private Task RunCodeAsync() => StartAsync();
    private Task RestartAsync() => _run is { } run ? StartAsync(run) : Task.CompletedTask;

    private async Task GoToControllerAsync()
    {
        await ChangeModeAsync("build");
        _shelf = "stock";
        if (DisplayLayout.Items.FirstOrDefault(item => item.DefinitionId == HomeCatalog.ControllerId) is { } item)
            StartPlacement(item);
    }

    private Task StartAsync(HomeRunStatus? previous = null) => PerformAsync(async () =>
    {
        CancelPlacement();
        _replay = null;
        _overlay = "light";
        _roomCollapsed = false;
        if (!HasController)
            throw new HomeGameRequestException("Placér hjemmecontrolleren fra lageret for at køre kode.");
        if (!_state!.RunnerAvailable)
        {
            await ReloadAvailabilityAsync();
            if (!_state.RunnerAvailable)
                throw new HomeGameRequestException("Kodeafvikling er utilgængelig lige nu. Prøv igen senere.");
        }
        var language = previous?.Run.Language ?? _language;
        if (IsActive) await SendControlAsync(new("stop"));
        ResetAudition();
        _beforeLight = HomeReadingPoint.Measure(DisplayLayout, PreviewSnapshot());
        await SaveLanguageAsync(language, !_state.Drafts.Any(draft => draft.Language == language));
        var experimentIntent = previous is null ? _intent : _runIntent;
        _run = await Client.StartAsync(Subject, new(_state.Layout.Revision, language, Task: 0, Intent: experimentIntent,
            Assessment: false, ScenarioId: "free", Speed: _speed, RestartRunId: previous?.Run.Id), Token);
        _runIntent = experimentIntent;
        _language = language;
        _stale = false;
        Drive();
    });

    private HomeWorldSnapshot PreviewSnapshot()
    {
        var sim = new HomeSimulation(DisplayLayout, HomeCatalog.FreeScenario, "room-preview");
        sim.BeginTick();
        return sim.Snapshot(includeLightMap: true);
    }

    private void ResetAudition()
    {
        _manualSimulation = null;
        _manualSnapshot = null;
        _beforeLight = null;
    }

    private void TryDevice(HomeItem item, bool value)
    {
        if (IsActive || _replay is not null || !IsOutput(item) || item.Placement is null) return;
        const string id = "manual-preview";
        if (_manualSimulation is null || _manualSimulation.Finished)
        {
            _manualSimulation = new HomeSimulation(DisplayLayout, HomeCatalog.FreeScenario, id, manualControl: true);
            if (Snapshot is { } last)
            {
                _manualSimulation.SetDaylight(last.DaylightLux);
                // Start the audition with the visible outputs from the latest real run.
                var first = _manualSimulation.BeginTick();
                var commands = last.PoweredAliases.Where(alias => RoomItems.Any(i => i.Alias == alias && IsOutput(i)))
                    .Select(alias => new HomeCommand(alias, "setPower", true)).ToArray();
                var seedError = _manualSimulation.Commit(new(id, first.Tick, _manualSimulation.NextSequence, commands, 100));
                if (seedError is not null) { _error = seedError.Message; ResetAudition(); return; }
                _manualSimulation.AdvanceTick();
            }
        }
        var sim = _manualSimulation;
        var frame = sim.BeginTick();
        _beforeLight = HomeReadingPoint.Measure(DisplayLayout, sim.Snapshot(includeLightMap: true));
        var error = sim.Commit(new(id, frame.Tick, sim.NextSequence, [new(item.Alias, "setPower", value)], 1000));
        if (error is not null) { _error = error.Message; ResetAudition(); return; }
        for (int i = 0; i < 10 && !sim.Finished; i++) sim.AdvanceTick();
        sim.BeginTick();
        _manualSnapshot = sim.Snapshot(includeLightMap: true);
        _overlay = "light";
    }

    private async Task ReloadAvailabilityAsync()
    {
        var current = await Client.LoadAsync(Subject, Token);
        _state = _state! with { RunnerAvailable = current.RunnerAvailable };
    }

    private Task ControlAsync(HomeRunControlRequest request) => PerformAsync(() => SendControlAsync(request));

    private async Task SendControlAsync(HomeRunControlRequest request)
    {
        if (_run is not { } before) return;
        var run = await Client.ControlAsync(Subject, before.Run.Id, request, Token);
        if (_run is not { } current || current.Run.Id != run.Run.Id || run.Sequence < current.Sequence) return;
        _run = run;
        if (request.Action == "resume") Drive();
    }

    private async Task ChangeSpeedAsync(ChangeEventArgs args)
    {
        if (!int.TryParse(args.Value?.ToString(), out var speed) || speed is not (1 or 4 or 10)) return;
        _speed = speed;
        if (IsActive) await ControlAsync(new("speed", Speed: speed));
    }

    private Task ChangeLightAsync(ChangeEventArgs args) => int.TryParse(args.Value?.ToString(), out var lux)
        ? ControlAsync(new("input", DaylightLux: lux)) : Task.CompletedTask;

    private Task ChangeSwitchAsync(ChangeEventArgs args) => ControlAsync(new("input", AutoEnabled: args.Value is true));

    private Task OpenReplayAsync() => PerformAsync(async () =>
    {
        if (_run is not { } run || IsActive) return;
        ResetAudition();
        _replay = new(await Client.ReplayAsync(Subject, run.Run.Id, Token));
        _replay.Seek(0, 0);
    });

    private void ChangeReplayScenario(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var index)) _replay?.Seek(index, 0);
    }

    private void SeekReplay(ChangeEventArgs args)
    {
        if (_replay is { } replay && int.TryParse(args.Value?.ToString(), out var tick))
            replay.Seek(replay.ScenarioIndex, tick);
    }

    private void Drive()
    {
        if (!IsAdvancing || _run is null) return;
        _ = DriveAsync(_run.Run.Id, ++_driverVersion);
    }

    private async Task DriveAsync(Guid id, long generation)
    {
        try
        {
            while (IsAdvancing && _run?.Run.Id == id && generation == _driverVersion)
            {
                await Task.Delay(100, Token);
                if (!IsAdvancing || _run.Run.Id != id || generation != _driverVersion) return;
                if (_working) continue;
                var current = _run;
                var status = await Client.StepAsync(Subject, id, new(current.Sequence, current.Speed), Token);
                if (_run?.Run.Id != id || generation != _driverVersion) return;
                if (status.Sequence >= _run.Sequence) _run = status;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (HomeGameRequestException ex)
        {
            if (generation != _driverVersion) return;
            _error = ex.Message;
            _conflict = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static string ValueText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => "Sand",
        JsonValueKind.False => "Falsk",
        _ => value.ToString()
    };

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetime.Cancel();
        await CloseResidentRoomAsync();
        if (_module is not null)
            await _module.DisposeAsync();
        _lifetime.Dispose();
    }
}
