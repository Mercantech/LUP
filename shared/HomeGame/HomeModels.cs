using System.Text.Json;

namespace Mercantec.Tech.Contracts.HomeGame;

public readonly record struct HomeCell(int X, int Y)
{
    public static HomeCell operator +(HomeCell cell, HomeCell offset) => new(cell.X + offset.X, cell.Y + offset.Y);
}

public readonly record struct HomeEdge(int X, int Y, bool Horizontal);
public sealed record HomeBoundary(HomeEdge Edge, bool Open = false);

public sealed record HomeGrid(
    int Columns,
    int Rows,
    HomeCell Entry,
    IReadOnlyList<HomeBoundary> Walls,
    IReadOnlyDictionary<string, IReadOnlyList<HomeCell>> Rooms);

public sealed record HomePlacement(
    HomeCell Anchor,
    int Rotation = 0,
    HomeEdge? Wall = null,
    string? HostId = null,
    string? PortId = null);

public sealed record HomeItem(
    string Id,
    string DefinitionId,
    string Alias,
    string Name,
    HomePlacement? Placement,
    bool Starter = false,
    int GoldPaid = 0,
    string? Role = null);

public sealed record HomeLayout(
    int Revision, IReadOnlyList<HomeItem> Items, HomeGrid Grid, string LayoutVersion = HomeCatalog.StarterLayoutId);

public sealed record HomeItemDefinition(
    string Id,
    string Name,
    string Kind,
    string Layer,
    int Width,
    int Height,
    int Price,
    int UnlockAfter = 0,
    bool BlocksWalk = false,
    bool BlocksLight = false,
    bool BlocksPresence = false,
    IReadOnlyList<HomeCell>? InteractionCells = null,
    IReadOnlyDictionary<string, HomeCell>? Ports = null,
    int Radius = 0,
    int NominalLux = 0,
    int BrightnessPercent = 0,
    bool Purchasable = true);

public sealed record HomeBuildAction(
    string Kind,
    string? ItemId = null,
    string? DefinitionId = null,
    HomePlacement? Placement = null,
    string? Value = null);

public sealed record HomeRuleError(string Code, string Message, IReadOnlyList<HomeCell> Cells);
public sealed record HomeBuildResult(HomeLayout? Layout, int GoldDelta, HomeRuleError? Error);

public sealed record HomeTask(
    string Id,
    string Title,
    string Goal,
    string Hint,
    IReadOnlyList<string> RequiredDefinitions,
    IReadOnlyList<string> ScenarioIds);

public sealed record HomeScenarioEvent(
    int Tick,
    int? DaylightLux = null,
    int? AmbientLux = null,
    string? Intention = null,
    bool? AutoEnabled = null,
    IReadOnlyDictionary<string, int>? AmbientLuxByRoom = null);

public sealed record HomeAssessmentWindow(int Start, int End, string Requirement);

public sealed record HomeScenario(
    string Id,
    string Title,
    int Ticks,
    IReadOnlyList<HomeScenarioEvent> Events,
    IReadOnlyList<HomeAssessmentWindow> Windows,
    bool Free = false,
    int Seed = 0,
    int Version = 1);

public sealed record HomeSensor(
    string SensorId,
    string Alias,
    JsonElement Value,
    string ValueType,
    string? Unit,
    int SampledAtMs,
    string Quality = "valid");

public sealed record HomeFrame(int Tick, int TimeMs, bool ControllerDue, IReadOnlyList<HomeSensor> Sensors);
public sealed record HomeCommand(string Alias, string Action, bool Value);
public sealed record HomeCommitObservation(string Alias, string Action, bool Value, int Tick, bool StateChanged);

public sealed record HomeCommandBatch(
    string RunId,
    int Tick,
    long Sequence,
    IReadOnlyList<HomeCommand> Commands,
    double? WaitMs,
    bool Completed = false);

public sealed record HomeLightCell(HomeCell Cell, int Lux);

public sealed record HomeWorldSnapshot(
    int Tick,
    int TimeMs,
    HomeCell? AvatarCell,
    string Activity,
    int DaylightLux,
    int AmbientLux,
    int? AvatarLux,
    int? Comfort,
    long EnergyMicroJ,
    bool AutoEnabled,
    IReadOnlyList<string> PoweredAliases,
    IReadOnlyList<HomeSensor> Sensors,
    IReadOnlyList<HomeCommand> LastCommands,
    IReadOnlyList<HomeLightCell> LightMap,
    string? Message,
    HomeRuleError? Error,
    IReadOnlyList<HomeCommitObservation>? DeviceCommits = null);

public sealed record HomeCriterion(
    string Id, string Title, int Samples, int Matched, int MinSamples, int RequiredPercent, bool Passed);

public sealed record HomeScenarioOutcome(
    string ScenarioId,
    int Seed,
    bool Passed,
    IReadOnlyList<HomeCriterion> Criteria,
    long EnergyMicroJ,
    HomeRuleError? Error);
