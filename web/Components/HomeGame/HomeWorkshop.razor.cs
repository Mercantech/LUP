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
    private HomeBuildAction? _placement;
    private HomeBuildRequest? _retryBuild;
    private HomeItem? _ghost;
    private HomeCell _cursor = new(0, 4);
    private string _mode = "test";
    private string _mobilePane = "plan";
    private string _shelf = "stock";
    private string _overlay = "light";
    private string _language = "python";
    private string _intent = "read";
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

    private HomeCell? AvatarCell => Snapshot is { } snapshot ? snapshot.AvatarCell : DisplayLayout.Grid.Entry;
    private HomeItem? Selected => _state?.Layout.Items.FirstOrDefault(item => item.Id == _selectedId);
    private HomeItem? BoardSelection => _replay is null ? Selected : DisplayLayout.Items.FirstOrDefault(item => item.Id == _selectedId);
    private bool HasDesk => DisplayLayout.Items.Any(item => item.Role == HomeCatalog.PrimaryDeskRole && item.Placement is not null);
    private string Alias(string definition, string fallback) => DisplayLayout.Items
        .Where(item => item.DefinitionId == definition).OrderByDescending(item => item.Placement is not null)
        .ThenByDescending(item => item.Starter).Select(item => item.Alias).FirstOrDefault() ?? fallback;
    private string LampAlias => Alias(HomeCatalog.LampId, "desk_lamp");
    private string DaylightAlias => Alias(HomeCatalog.DaylightId, "daylight");
    private string LampExample => HomeCodeGuide.Lamp(_language, LampAlias);
    private string DaylightExample => HomeCodeGuide.Daylight(_language, LampAlias, DaylightAlias);

    private IEnumerable<HomeItemDefinition> ShopItems => HomeCatalog.Items.Where(item => item.Purchasable);
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

    private static string ActivityLabel(string activity) => activity switch
    {
        "reading" => "Din avatar læser",
        "walking" => "Din avatar er på vej",
        "relaxing" => "Din avatar slapper af",
        "away" => "Din avatar er ikke hjemme",
        _ => "Din avatar er hjemme"
    };

    private static bool IsUnfinished(string? status) => status is "queued" or "compiling" or "running" or "paused";

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => PerformAsync(async () =>
    {
        var first = _state is null;
        _state = await Client.LoadAsync(Subject, Token);
        _conflict = false;
        _reflection = _state.Reflection;
        _helpHold = _state.Classrooms.FirstOrDefault()?.HoldId ?? Guid.Empty;
        if (first)
        {
            if (InitialView == "butik")
            {
                _mode = "build";
                _shelf = "shop";
                _mobilePane = "work";
            }
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
                _mode = "test";
            }
        }
        if (IsAdvancing)
        {
            _mode = "test";
            Drive();
        }
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
        _shelf = shelf;
        if (shelf != "shop" || _state?.IsTeacherSandbox != false) return Task.CompletedTask;
        return PerformAsync(async () =>
        {
            var current = await Client.LoadAsync(Subject, Token);
            _state = _state! with { Gold = current.Gold };
        });
    }

    private async Task ChangeModeAsync(string mode)
    {
        if (mode == "build" && IsAdvancing)
        {
            await ControlAsync(new("pause"));
            if (IsAdvancing) return;
        }
        if (mode != "build") CancelPlacement();
        if (mode != "test") _replay = null;
        _mode = mode;
        _overlay = mode == "build" ? "normal" : "light";
        _mobilePane = "work";
    }

    private bool CanBuy(HomeItemDefinition definition) => _state!.IsTeacherSandbox || _state.Gold >= definition.Price;

    private string PriceLabel(HomeItemDefinition definition)
    {
        if (_state!.IsTeacherSandbox) return "Tilføj gratis";
        return CanBuy(definition) ? $"{definition.Price} guld" : $"Mangler {definition.Price - _state.Gold} guld";
    }

    private static string SizeLabel(HomeItemDefinition definition) => definition.Layer == "wall"
        ? "Monteres på væg" : $"{definition.Width} × {definition.Height} felter";

    private void Select(HomeItem item)
    {
        _selectedId = item.Id;
        _itemName = item.Name;
        _itemAlias = item.Alias;
        _sellId = null;
    }

    private async Task ActivateCellAsync(HomeCell cell)
    {
        VisitCell(cell);
        if (_replay is not null) return;
        if (_placement is not null)
        {
            await ConfirmPlacementAsync();
            return;
        }
        var item = DisplayLayout.Items.LastOrDefault(item => item.Placement is { } placed &&
            (placed.Anchor == cell || HomeRules.Footprint(item).Contains(cell)));
        if (item is null) return;
        await ChangeModeAsync("build");
        if (_mode != "build") return;
        Select(item);
        _shelf = "room";
    }

    private void VisitCell(HomeCell cell)
    {
        if (_cursor == cell) return;
        _cursor = cell;
        UpdatePreview();
    }

    private void StartPurchase(HomeItemDefinition definition)
    {
        _selectedId = null;
        _rotation = 0;
        _placement = new("buy", DefinitionId: definition.Id);
        _mobilePane = "plan";
        UpdatePreview();
    }

    private void StartPlacement(HomeItem item)
    {
        Select(item);
        _rotation = item.Placement?.Rotation ?? 0;
        _cursor = item.Placement?.Anchor ?? _cursor;
        _placement = new("place", item.Id, item.DefinitionId);
        _mobilePane = "plan";
        UpdatePreview();
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
        _rotation = (_rotation + 90) % 360;
        UpdatePreview();
    }

    private void CancelPlacement()
    {
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
        _retryBuild = null;
        _stale = _run is not null;
        _sellId = null;
        CancelPlacement();
        if (action.Kind == "buy" && _state.Layout.Items.FirstOrDefault(item => !oldItems.Contains(item.Id)) is { } bought)
            Select(bought);
        if (action.Kind is "place" or "buy")
        {
            _mobilePane = "work";
            _shelf = "room";
            if (Selected?.DefinitionId is HomeCatalog.LampId or HomeCatalog.PresenceId)
                _overlay = "range";
        }
        if (action.Kind == "store") _shelf = "stock";
        _notice = action.Kind switch
        {
            "buy" when _state.IsTeacherSandbox => "Prøveudstyret er placeret.",
            "buy" => "Køb og placering gemt.",
            "undo" => "Seneste byggehandling fortrudt.",
            "sell" when _state.Gold > previousGold => "Genstanden er solgt. Guldet er tilbage på din LUP-saldo.",
            "sell" => "Genstanden er fjernet fra dit hjem.",
            _ => "Indretningen er gemt."
        };
        if (_state.Gold != previousGold)
            await World.ReloadAvatarProfileAsync();
    });

    private string SavedSource(string language) => _state?.Drafts.FirstOrDefault(draft => draft.Language == language)?.Source
        ?? HomeCodeGuide.Lamp(language, LampAlias);
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
        if (Source != LampExample &&
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
            Select(item);
    }

    private Task StartAsync(HomeRunStatus? previous = null) => PerformAsync(async () =>
    {
        CancelPlacement();
        _replay = null;
        _overlay = "light";
        _mobilePane = "work";
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
        await SaveLanguageAsync(language, !_state.Drafts.Any(draft => draft.Language == language));
        _run = await Client.StartAsync(Subject, new(_state.Layout.Revision, language, Task: 0, Intent: _intent,
            Assessment: false, ScenarioId: "free", Speed: _speed, RestartRunId: previous?.Run.Id), Token);
        _language = language;
        _stale = false;
        Drive();
    });

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

    private async Task ChangeIntentAsync(ChangeEventArgs args)
    {
        _intent = args.Value?.ToString() ?? "read";
        if (IsActive) await ControlAsync(new("input", Intent: _intent));
    }

    private Task ChangeLightAsync(ChangeEventArgs args) => int.TryParse(args.Value?.ToString(), out var lux)
        ? ControlAsync(new("input", DaylightLux: lux)) : Task.CompletedTask;

    private Task ChangeSwitchAsync(ChangeEventArgs args) => ControlAsync(new("input", AutoEnabled: args.Value is true));

    private Task OpenReplayAsync() => PerformAsync(async () =>
    {
        if (_run is not { } run || IsActive) return;
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
        if (_module is not null)
            await _module.DisposeAsync();
        _lifetime.Dispose();
    }
}
