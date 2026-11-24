namespace Mercantec.Tech.Contracts.HomeGame;

public static class HomeCatalog
{
    public const string StarterLayoutId = "starter_home_v1";
    public const int TickMs = 100;
    public const double CellMeters = 0.5;
    public const string RoomId = "living_room";
    public const string PrimaryDeskRole = "primary_reading_desk";
    public const string DeskId = "desk_basic";
    public const string SofaId = "sofa_basic";
    public const string RugId = "rug_basic";
    public const string LampId = "lamp_basic";
    public const string DaylightId = "daylight_sensor";
    public const string PresenceId = "presence_sensor";
    public const string SwitchId = "switch_basic";
    public const string ControllerId = "home_controller";
    public const string BlindId = "roller_blind";
    public const string ArmchairId = "armchair";
    public const string BookcaseId = "bookcase_low";
    public const string CoffeeTableId = "coffee_table";
    public const string CorkboardId = "corkboard";
    public const string SignId = "flip_sign";
    public const string PlanterId = "planter_pump";
    public const string MoistureId = "soil_probe";
    public static HomeEdge WindowEdge { get; } = new(2, 0, true);

    public static IReadOnlyDictionary<string, string> FloorStyles { get; } = new Dictionary<string, string>
    {
        ["oak"] = "Egeplanker", ["parquet"] = "Sildebensparket", ["slate"] = "Skifer"
    };
    public static IReadOnlyDictionary<string, string> WallpaperStyles { get; } = new Dictionary<string, string>
    {
        ["linen"] = "Lyst hør", ["sage"] = "Salviegrøn", ["stripe"] = "Stribet tapet"
    };

    public static bool IsOutput(string? kind) => kind is "lamp" or "blind" or "sign" or "pump";

    public static IReadOnlyList<HomeItemDefinition> Items { get; } = Array.AsReadOnly<HomeItemDefinition>(
    [
        new(DeskId, "Skrivebord", "reading", "floor", 2, 1, 80, BlocksWalk: true,
            InteractionCells: [new(0, 1), new(1, 1)],
            Ports: new Dictionary<string, HomeCell> { ["left"] = new(0, 0), ["right"] = new(1, 0) }),
        new(SofaId, "Sofa", "relaxing", "floor", 3, 1, 90, BlocksWalk: true,
            InteractionCells: [new(0, 1), new(1, 1), new(2, 1)]),
        new(RugId, "Tæppe", "rug", "floorDecoration", 2, 2, 30),
        new(LampId, "Gulvlampe", "lamp", "floor", 1, 1, 30, BlocksWalk: true,
            Radius: 4, NominalLux: 480, BrightnessPercent: 75),
        new(DaylightId, "Dagslyssensor", "daylight", "wall", 1, 1, 20),
        new(PresenceId, "Tilstedeværelsessensor", "presence", "wall", 1, 1, 20, Radius: 4),
        new(SwitchId, "Kontakt", "switch", "wall", 1, 1, 10),
        new(ControllerId, "Hjemmecontroller", "controller", "wall", 1, 1, 0, Purchasable: false),
        new(BlindId, "Rullegardin", "blind", "wall", 1, 1, 35),
        new(ArmchairId, "Lænestol", "relaxing", "floor", 1, 1, 45, BlocksWalk: true,
            InteractionCells: [new(0, 1)]),
        new(BookcaseId, "Lav bogreol", "bookcase", "floor", 2, 1, 50, BlocksWalk: true),
        new(CoffeeTableId, "Rundt sofabord", "table", "floor", 1, 1, 30, BlocksWalk: true),
        new(CorkboardId, "Opslagstavle", "corkboard", "wall", 1, 1, 20),
        new(SignId, "Vendeskilt", "sign", "wall", 1, 1, 25),
        new(PlanterId, "Plantekasse med pumpe", "pump", "floor", 2, 1, 45, BlocksWalk: true,
            Ports: new Dictionary<string, HomeCell> { ["soil"] = new(0, 0) }),
        new(MoistureId, "Jordfugtføler", "moisture", "surface", 1, 1, 15)
    ]);

    // Geometry and SDK fixtures. Kept out of the pilot store and inventory.
    private static readonly HomeItemDefinition[] FixtureItems =
    [
        new("fixture_cabinet", "Højt skab", "cabinet", "floor", 1, 1, 0, BlocksWalk: true,
            BlocksLight: true, BlocksPresence: true, Purchasable: false),
        new("fixture_indoor_sensor", "Indendørs lyssensor", "indoorLight", "surface", 1, 1, 0,
            Purchasable: false),
        new("fixture_surface_lamp", "Bordlampe", "lamp", "surface", 1, 1, 0, Radius: 4,
            NominalLux: 480, BrightnessPercent: 75, Purchasable: false)
    ];

    private static readonly IReadOnlyDictionary<string, HomeItemDefinition> Definitions =
        Items.Concat(FixtureItems).ToDictionary(d => d.Id, StringComparer.Ordinal);

    public static HomeItemDefinition? Definition(string? id) =>
        id is null ? null : Definitions.GetValueOrDefault(id);

    public static HomeGrid StarterGrid { get; } = CreateStarterGrid();

    public static HomeLayout CreateStarter()
    {
        return new(1,
        [
            Starter(DeskId, "reading_desk", PrimaryDeskRole),
            Starter(ControllerId, "home_controller"),
            Starter(LampId, "desk_lamp"),
            Starter(SofaId, "sofa"),
            Starter(RugId, "rug"),
            Starter(BlindId, "roller_blind"),
            Starter(DaylightId, "daylight"),
            Starter(PresenceId, "presence"),
            Starter(SwitchId, "auto_enabled")
        ], StarterGrid);
    }

    private static HomeItem Starter(
        string definition, string alias, string? role = null) =>
        new(Guid.NewGuid().ToString("N"), definition, alias, Definitions[definition].Name, null,
            Starter: true, Role: role);

    private static HomeGrid CreateStarterGrid()
    {
        const int columns = 12;
        const int rows = 10;
        var cells = Enumerable.Range(0, rows)
            .SelectMany(y => Enumerable.Range(0, columns).Select(x => new HomeCell(x, y))).ToArray();
        var walls = new List<HomeBoundary>();
        for (var x = 0; x < columns; x++)
        {
            walls.Add(new(new(x, 0, true)));
            walls.Add(new(new(x, rows, true)));
        }
        for (var y = 0; y < rows; y++)
        {
            walls.Add(new(new(0, y, false), Open: y == 4));
            walls.Add(new(new(columns, y, false)));
        }
        return new(columns, rows, new(0, 4), walls.ToArray(),
            new Dictionary<string, IReadOnlyList<HomeCell>>(StringComparer.Ordinal) { [RoomId] = cells });
    }

    private static readonly string[] LightingScenarios = ["dark_reading", "day_reading"];
    private static readonly string[] PresenceScenarios = [.. LightingScenarios, "dark_away", "dark_return"];
    private static readonly string[] AllScenarios = [.. PresenceScenarios, "auto_switch"];
    private static readonly string[] LightingDefinitions = [LampId, DaylightId];
    private static readonly string[] PresenceDefinitions = [.. LightingDefinitions, PresenceId];
    private static readonly string[] AllDefinitions = [.. PresenceDefinitions, SwitchId];

    public static IReadOnlyList<HomeTask> Tasks { get; } = Array.AsReadOnly<HomeTask>(
    [
        new("P01", "Lys ved bordet", "Placér en lampe, og brug kode til at give avataren 300–600 lux under læsning.",
            "Prøv lampen ét felt fra læsefeltet. Skriv en kommando, der tænder desk_lamp.",
            [LampId], ["dark_reading"]),
        new("P02", "Placering betyder noget", "Flyt skrivebordet til (2, 6) uden rotation, og skab læselys på den nye plads.",
            "Genbrug koden fra første opgave. Se i Gang og adgang, hvor avataren ender med at læse.",
            [LampId], ["dark_reading"]),
        new("P03", "Dagslysstyring", "Giv læselys i mørke, og sluk lampen i dagslys.",
            "Montér dagslyssensoren. Læs daylight, og sammenlign målingen med 300 lux i en betingelse.",
            LightingDefinitions, LightingScenarios),
        new("P04", "Tilstedeværelse", "Tænd i mørke, når avataren er hjemme. Hold boligen slukket, når den er tom.",
            "Monter sensoren inden for fire felter af læsefeltet. Kombinér presence med dagslysmålingen.",
            PresenceDefinitions, PresenceScenarios),
        new("P05", "Manuel betjening", "Lad kontakten give lov til autolys. Lampen skal være slukket, når autolys deaktiveres.",
            "Læs auto_enabled som en tredje værdi. Prøv at slå kontakten fra og til under læsning.",
            AllDefinitions, AllScenarios),
        new("P06", "Et sammenhængende hjem", "Afprøv dine samlede lysregler i fem situationer. Beskriv en mulig forbedring.",
            "Se på målinger og log i hvert scenarie. Forklar, hvad der kunne gøre løsningen mere robust.",
            AllDefinitions, AllScenarios)
    ]);

    public static IReadOnlyDictionary<string, HomeScenario> Scenarios { get; } =
        new Dictionary<string, HomeScenario>(StringComparer.Ordinal)
        {
            ["dark_reading"] = new("dark_reading", "Læsning i mørke", 300,
                [Weather(40, "read")], [new(10, 300, "comfort")]),
            ["day_reading"] = new("day_reading", "Læsning i dagslys", 300,
                [Weather(450, "read")], [new(10, 300, "day")]),
            ["dark_away"] = new("dark_away", "Tom bolig i mørke", 300,
                [Weather(40, "away")], [new(10, 300, "off")]),
            ["dark_return"] = new("dark_return", "Hjemkomst i mørke", 600,
                [Weather(40, "away"), new(300, Intention: "read")],
                [new(10, 300, "off"), new(310, 600, "comfort")]),
            ["auto_switch"] = new("auto_switch", "Kontakt under læsning", 900,
                [Weather(40, "read"), new(300, AutoEnabled: false), new(600, AutoEnabled: true)],
                [new(10, 300, "comfort"), new(310, 600, "off"), new(610, 900, "comfort")])
        };

    public static HomeScenario FreeScenario { get; } =
        new("free", "Fri afprøvning", 1200, [Weather(40, "read")], [], Free: true);

    private static HomeScenarioEvent Weather(int lux, string intention) =>
        new(0, lux, Intention: intention, AutoEnabled: true,
            AmbientLuxByRoom: new Dictionary<string, int> { [RoomId] = lux });

    // Assessment variation is bounded independently of viewport and installed furniture.
    public static HomeScenario WithSeed(HomeScenario scenario, int seed)
    {
        if (seed == 0 || scenario.Free) return scenario with { Seed = seed };
        var variation = (int)((uint)seed % 9) - 4;
        var events = scenario.Events.Select(e => e with
        {
            DaylightLux = e.DaylightLux + variation,
            AmbientLux = e.AmbientLux + variation,
            AmbientLuxByRoom = e.AmbientLuxByRoom?.ToDictionary(room => room.Key, room => room.Value + variation)
        }).ToArray();
        return scenario with { Seed = seed, Events = events };
    }
}
