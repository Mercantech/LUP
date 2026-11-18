using System.Text.Json;

namespace Mercantec.Tech.Contracts.HomeGame;

public sealed class HomeSimulation
{
    private const int WalkTicks = 5;
    private const int MaxCommands = 256;
    private const int MinAssessmentSamples = 200;
    private readonly HomeLayout _layout;
    private readonly HomeScenario _scenario;
    private readonly string _runId;
    private readonly IReadOnlyDictionary<string, HomeItem> _items;
    private readonly IReadOnlyDictionary<HomeCell, string> _rooms;
    private readonly Dictionary<string, int> _ambientByRoom = new(StringComparer.Ordinal);
    private readonly HomeItem[] _sensors;
    private readonly Dictionary<string, IReadOnlyDictionary<HomeCell, int>> _lamps;
    private readonly Dictionary<string, IReadOnlySet<HomeCell>> _presence;
    private readonly HashSet<string> _powered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _enabledSwitches = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HomeCommitObservation> _deviceCommits = new(StringComparer.Ordinal);
    private readonly Queue<HomeScenarioEvent> _events;
    private readonly List<AssessmentCounter> _criteria = [];
    private HomeFrame? _openFrame;
    private IReadOnlyList<HomeSensor> _lastSensors = [];
    private IReadOnlyList<HomeCommand> _lastCommands = [];
    private bool _committed;
    private bool _controllerDone;
    private int _wakeTick;
    private long _energyMicroJ;
    private int _daylight = 40;
    private int _ambient = 40;
    private HomeCell? _avatar;
    private string _activity = "idle";
    private string _intention = "idle";
    private string? _pendingIntention;
    private int? _pendingDaylight;
    private readonly Dictionary<string, bool> _pendingSwitches = new(StringComparer.Ordinal);
    private string? _navigationMessage;
    private bool _refreshPath;
    private Queue<HomeCell> _route = new();
    private HomeCell? _step;
    private int _stepArrival;
    private HomeRuleError? _fault;

    public int Tick { get; private set; }
    public long NextSequence { get; private set; } = 1;
    public bool Finished => _fault is not null || Tick >= _scenario.Ticks;
    public bool ControllerFinished => _controllerDone;

    public HomeSimulation(HomeLayout layout, HomeScenario scenario, string runId, HomeCell? avatarStart = null)
    {
        _layout = layout;
        _scenario = scenario;
        _runId = runId;
        _avatar = avatarStart ?? layout.Grid.Entry;
        SetSwitchState(PrimarySwitchAlias(), true);
        _items = layout.Items.ToDictionary(item => item.Alias, StringComparer.Ordinal);
        _rooms = layout.Grid.Rooms.SelectMany(room => room.Value.Select(cell => (Cell: cell, Room: room.Key)))
            .ToDictionary(pair => pair.Cell, pair => pair.Room);
        var installed = layout.Items.Where(item => item.Placement is not null).ToArray();
        _controllerDone = !installed.Any(item => item.DefinitionId == HomeCatalog.ControllerId);
        _sensors = installed.Where(item => HomeCatalog.Definition(item.DefinitionId)?.Kind is
            "daylight" or "presence" or "switch" or "indoorLight").ToArray();
        _lamps = installed.Where(item => HomeCatalog.Definition(item.DefinitionId)?.Kind == "lamp")
            .ToDictionary(item => item.Alias, item => HomeRules.LightContribution(layout, item), StringComparer.Ordinal);
        _presence = _sensors.Where(item => HomeCatalog.Definition(item.DefinitionId)?.Kind == "presence")
            .ToDictionary(item => item.Alias, item => (IReadOnlySet<HomeCell>)HomeRules.Range(layout, item).ToHashSet(),
                StringComparer.Ordinal);
        _events = new(scenario.Events.OrderBy(e => e.Tick));
        for (var i = 0; i < scenario.Windows.Count; i++)
        {
            var window = scenario.Windows[i];
            if (window.Requirement is "comfort" or "day")
                _criteria.Add(new(window, "comfort_" + (i + 1), "300–600 lux under læsning", "comfort", 95));
            if (window.Requirement is "off" or "day")
                _criteria.Add(new(window, "off_" + (i + 1), OffTitle(scenario, window), "off", 100));
        }
    }

    private static string OffTitle(HomeScenario scenario, HomeAssessmentWindow window) => window.Requirement == "day"
        ? "Slukket i dagslys"
        : scenario.Id == "auto_switch" ? "Slukket med autolys deaktiveret" : "Slukket i tom bolig";

    public HomeFrame BeginTick()
    {
        if (_openFrame is { } frame) return frame;
        if (Finished) return new(Tick, Tick * HomeCatalog.TickMs, false, _lastSensors);
        ApplyEvents();
        CompleteMovement();
        if (_refreshPath) PlanPath();
        _lastSensors = ReadSensors();
        _committed = false;
        _openFrame = new(Tick, Tick * HomeCatalog.TickMs, !_controllerDone && Tick >= _wakeTick, _lastSensors);
        return _openFrame;
    }

    public HomeRuleError? Commit(HomeCommandBatch batch)
    {
        if (Finished || _openFrame is not { ControllerDue: true } || _committed ||
            batch.RunId != _runId || batch.Tick != Tick || batch.Sequence != NextSequence)
            return new("INVALID_BATCH", "Kommandobatchen matcher ikke controllerens aktive tick eller sekvens.", []);
        if (batch.Commands is null || batch.Commands.Count > MaxCommands)
            return Fault(new("BATCH_LIMIT", "Controlleren kan aflevere højst 256 kommandoer pr. aktivering.", []));
        if (!batch.Completed && (batch.WaitMs is not { } wait || !double.IsFinite(wait) || wait < 0))
            return Fault(new("INVALID_WAIT", "Ventetiden skal være et endeligt tal på mindst nul.", []));
        var final = new Dictionary<string, HomeCommand>(StringComparer.Ordinal);
        foreach (var command in batch.Commands)
        {
            if (command is null || !HomeRules.ValidAlias(command.Alias) ||
                !_items.TryGetValue(command.Alias, out var target))
                return Fault(new("UNKNOWN_ALIAS", $"Kodealiasset {command?.Alias} findes ikke i boligen.", []));
            if (target.Placement is null)
                return Fault(new("DEVICE_NOT_INSTALLED", $"Placér {target.Name} fra lageret.", []));
            if (!_lamps.ContainsKey(command.Alias) || command.Action != "setPower")
                return Fault(new("INVALID_COMMAND", $"{target.Name} understøtter ikke denne kommando.", []));
            final[command.Alias] = command;
        }

        foreach (var command in final.Values)
        {
            var changed = command.Value ? _powered.Add(command.Alias) : _powered.Remove(command.Alias);
            _deviceCommits[command.Alias] = new(command.Alias, command.Action, command.Value, Tick, changed);
        }
        if (final.Count > 0) _lastCommands = final.Values.ToArray();
        _controllerDone = batch.Completed;
        if (!batch.Completed)
        {
            var delay = Math.Max(1, Math.Ceiling(batch.WaitMs!.Value / HomeCatalog.TickMs));
            _wakeTick = Tick + (int)Math.Min(_scenario.Ticks + 1d, delay);
        }
        _committed = true;
        NextSequence++;
        return null;
    }

    public HomeRuleError Fault(HomeRuleError error)
    {
        _fault ??= error;
        return _fault;
    }

    public void AdvanceTick()
    {
        if (Finished) return;
        var frame = BeginTick();
        if (frame.ControllerDue && !_committed)
        {
            Fault(new("CONTROLLER_BATCH_MISSING", "Controlleren afleverede ikke et svar for det aktive tick.", []));
            return;
        }
        foreach (var alias in _powered)
        {
            var definition = HomeCatalog.Definition(_items[alias].DefinitionId)!;
            var milliWatts = 8000 * definition.BrightnessPercent / 100;
            _energyMicroJ += milliWatts * HomeCatalog.TickMs;
        }
        Observe();
        if (_step is null && _route.TryDequeue(out var next))
        {
            _step = next;
            _stepArrival = Tick + WalkTicks;
            _activity = "walking";
        }
        Tick++;
        _openFrame = null;
    }

    public void SetIntention(string intention)
    {
        if (_scenario.Free && !Finished && intention is "read" or "relax" or "idle" or "leave" or "away")
            _pendingIntention = intention == "away" ? "leave" : intention;
    }

    public void SetDaylight(int lux)
    {
        if (_scenario.Free && !Finished) _pendingDaylight = Math.Clamp(lux, 0, 2000);
    }

    public void SetAutoEnabled(bool enabled, string? alias = null)
    {
        alias ??= PrimarySwitchAlias();
        if (_scenario.Free && !Finished && _items.TryGetValue(alias, out var item) &&
            HomeCatalog.Definition(item.DefinitionId)?.Kind == "switch" && item.Placement is not null)
            _pendingSwitches[alias] = enabled;
    }

    private void ApplyEvents()
    {
        while (_events.TryPeek(out var next) && next.Tick == Tick)
        {
            _events.Dequeue();
            _daylight = next.DaylightLux ?? _daylight;
            _ambient = next.AmbientLux ?? _ambient;
            if (next.AmbientLuxByRoom is { } ambient)
            {
                foreach (var room in ambient) _ambientByRoom[room.Key] = room.Value;
            }
            if (next.Intention is { } intention) ApplyIntention(intention);
            if (next.AutoEnabled is { } enabled) SetSwitchState(PrimarySwitchAlias(), enabled);
        }
        if (_pendingIntention is { } manualIntention)
        {
            ApplyIntention(manualIntention);
            _pendingIntention = null;
        }
        if (_pendingDaylight is { } lux)
        {
            _daylight = _ambient = lux;
            _ambientByRoom.Clear();
            _pendingDaylight = null;
        }
        foreach (var (alias, enabled) in _pendingSwitches) SetSwitchState(alias, enabled);
        _pendingSwitches.Clear();
    }

    private string PrimarySwitchAlias() =>
        _layout.Items.FirstOrDefault(item => item.DefinitionId == HomeCatalog.SwitchId && item.Starter)?.Alias
        ?? "auto_enabled";

    private void SetSwitchState(string alias, bool enabled)
    {
        if (enabled) _enabledSwitches.Add(alias);
        else _enabledSwitches.Remove(alias);
    }

    private void ApplyIntention(string intention)
    {
        _intention = intention;
        _refreshPath = true;
        _navigationMessage = null;
        if (intention == "away")
        {
            _avatar = null;
            _step = null;
        }
    }

    private void CompleteMovement()
    {
        if (_step is not { } step || Tick < _stepArrival) return;
        _avatar = step;
        _step = null;
        if (_route.Count == 0 && !_refreshPath) Arrive();
    }

    private void PlanPath()
    {
        if (_step is not null) return;
        _refreshPath = false;
        _route.Clear();
        if (_intention is "read" or "relax") _avatar ??= _layout.Grid.Entry;
        if (_avatar is not { } avatar)
        {
            _activity = "away";
            return;
        }
        if (_intention == "idle")
        {
            _activity = "idle";
            return;
        }
        var item = _intention == "read"
            ? _layout.Items.FirstOrDefault(furniture => furniture.Role == HomeCatalog.PrimaryDeskRole)
            : _layout.Items.FirstOrDefault(furniture =>
                furniture.DefinitionId == HomeCatalog.SofaId && furniture.Placement is not null);
        var goals = _intention == "leave"
            ? new[] { _layout.Grid.Entry }
            : item is null ? [] : HomeRules.AccessibleInteractions(_layout, item);
        var path = HomeRules.FindPath(_layout, avatar, goals);
        if (path is null)
        {
            _activity = "idle";
            _navigationMessage = _intention switch
            {
                "read" => "Avataren kan ikke komme frem til skrivebordet.",
                "relax" => "Avataren kan ikke komme frem til en sofa.",
                _ => "Avataren kan ikke komme frem til indgangen."
            };
            return;
        }
        if (path.Count == 1)
        {
            Arrive();
            return;
        }
        _route = new(path.Skip(1));
        _activity = "walking";
    }

    private void Arrive()
    {
        _activity = _intention switch
        {
            "read" => "reading",
            "relax" => "relaxing",
            "leave" => "away",
            _ => "idle"
        };
        if (_activity == "away") _avatar = null;
    }

    private IReadOnlyList<HomeSensor> ReadSensors()
    {
        return _sensors.Select(item => HomeCatalog.Definition(item.DefinitionId)!.Kind switch
        {
            "presence" => new HomeSensor(item.Id, item.Alias,
                JsonSerializer.SerializeToElement(_avatar is { } avatar && _presence[item.Alias].Contains(avatar)),
                "boolean", null, Tick * HomeCatalog.TickMs),
            "switch" => new(item.Id, item.Alias, JsonSerializer.SerializeToElement(_enabledSwitches.Contains(item.Alias)),
                "boolean", null, Tick * HomeCatalog.TickMs),
            "indoorLight" => new(item.Id, item.Alias, JsonSerializer.SerializeToElement(LuxAt(item.Placement!.Anchor)),
                "number", "lux", Tick * HomeCatalog.TickMs),
            _ => new(item.Id, item.Alias, JsonSerializer.SerializeToElement(_daylight),
                "number", "lux", Tick * HomeCatalog.TickMs)
        }).ToArray();
    }

    private int LuxAt(HomeCell cell)
    {
        var lux = AmbientAt(cell);
        foreach (var alias in _powered) lux += _lamps[alias].GetValueOrDefault(cell);
        return Math.Clamp(lux, 0, 2000);
    }

    private int AmbientAt(HomeCell cell) => _rooms.TryGetValue(cell, out var room)
        ? _ambientByRoom.GetValueOrDefault(room, _ambient)
        : _ambient;

    private void Observe()
    {
        foreach (var criterion in _criteria)
        {
            if (Tick < criterion.Window.Start || Tick >= criterion.Window.End) continue;
            if (criterion.Kind == "comfort" && (_activity != "reading" || _avatar is null)) continue;
            criterion.Samples++;
            if (criterion.Kind == "off" ? _powered.Count == 0 : LuxAt(_avatar!.Value) is >= 300 and <= 600)
                criterion.Matched++;
        }
    }

    public HomeWorldSnapshot Snapshot(bool includeLightMap = false)
    {
        int? lux = _avatar is { } avatar ? LuxAt(avatar) : null;
        int? comfort = _activity == "reading" && lux is { } readingLux ? HomeRules.Comfort(readingLux) : null;
        var message = _navigationMessage ?? (_activity == "reading" ? lux switch
        {
            < 300 => "Der er for mørkt til at læse behageligt.",
            > 600 => "Lyset er for kraftigt til at læse behageligt.",
            _ => "Avataren læser med behageligt lys."
        } : null);
        return new(Tick, Tick * HomeCatalog.TickMs, _avatar, _activity, _daylight, AmbientAt(_avatar ?? _layout.Grid.Entry),
            lux, comfort,
            _energyMicroJ, _enabledSwitches.Contains(PrimarySwitchAlias()), _powered.Order(StringComparer.Ordinal).ToArray(),
            _lastSensors, _lastCommands,
            includeLightMap ? HomeRules.FloorCells(_layout.Grid).Select(cell => new HomeLightCell(cell, LuxAt(cell))).ToArray() : [],
            message, _fault, _deviceCommits.Values.ToArray());
    }

    public HomeScenarioOutcome Outcome()
    {
        var criteria = _criteria.Select(counter =>
        {
            var passed = counter.Samples >= MinAssessmentSamples &&
                counter.Matched * 100 >= counter.Samples * counter.RequiredPercent;
            return new HomeCriterion(counter.Id, counter.Title, counter.Samples, counter.Matched, MinAssessmentSamples,
                counter.RequiredPercent, passed);
        }).ToArray();
        return new(_scenario.Id, _scenario.Seed,
            Finished && _fault is null && criteria.All(criterion => criterion.Passed), criteria, _energyMicroJ, _fault);
    }

    private sealed class AssessmentCounter(
        HomeAssessmentWindow window, string id, string title, string kind, int requiredPercent)
    {
        public HomeAssessmentWindow Window { get; } = window;
        public string Id { get; } = id;
        public string Title { get; } = title;
        public string Kind { get; } = kind;
        public int RequiredPercent { get; } = requiredPercent;
        public int Samples { get; set; }
        public int Matched { get; set; }
    }
}
