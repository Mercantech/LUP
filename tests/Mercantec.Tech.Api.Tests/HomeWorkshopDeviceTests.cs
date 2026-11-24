using System.Text.Json;
using System.Text.Json.Nodes;
using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Api.Tests;

public sealed class HomeWorkshopDeviceTests
{
    [Fact]
    public void Fresh_room_is_empty_and_surfaces_round_trip_without_altering_inventory_or_gold()
    {
        var layout = HomeCatalog.CreateStarter();
        Assert.All(layout.Items, item => { Assert.True(item.Starter); Assert.Null(item.Placement); });
        Assert.Contains(layout.Items, item => item.DefinitionId == HomeCatalog.BlindId);
        var floor = HomeRules.Apply(layout, new("floorStyle", Value: "parquet"));
        var wall = HomeRules.Apply(floor.Layout!, new("wallpaperStyle", Value: "stripe"));
        Assert.Equal(0, floor.GoldDelta);
        Assert.Equal(0, wall.GoldDelta);
        Assert.Equal(layout.Revision + 2, wall.Layout!.Revision);
        Assert.Equal(layout.Items, wall.Layout.Items);
        var loaded = JsonSerializer.Deserialize<HomeLayout>(JsonSerializer.Serialize(wall.Layout))!;
        Assert.Equal("parquet", loaded.FloorStyle);
        Assert.Equal("stripe", loaded.WallpaperStyle);
        Assert.Equal("INVALID_STYLE", HomeRules.Apply(loaded, new("wallpaperStyle", Value: "bad")).Error?.Code);

        var furnished = Place(layout, HomeCatalog.DeskId, new(new(4, 5), 90));
        var oldJson = JsonSerializer.SerializeToNode(furnished)!.AsObject();
        oldJson.Remove("WallpaperStyle");
        oldJson.Remove("FloorStyle");
        var original = oldJson.Deserialize<HomeLayout>()!;
        Assert.Equal("oak", original.FloorStyle);
        Assert.Equal("linen", original.WallpaperStyle);
        Assert.Equal(furnished.Items, original.Items);
    }

    [Fact]
    public void Wall_window_floor_and_soil_mounts_enforce_their_locations_through_rotation_and_storage()
    {
        var home = HomeCatalog.CreateStarter();
        var blind = home.Items.Single(item => item.DefinitionId == HomeCatalog.BlindId);
        Assert.Equal("INVALID_MOUNT", HomeRules.Apply(home, new("place", blind.Id, Placement: new(new(2, 0)))).Error?.Code);
        Assert.Equal("INVALID_MOUNT", HomeRules.Apply(home, new("place", blind.Id, Placement: North(3))).Error?.Code);
        home = Place(home, HomeCatalog.BlindId, North(2));
        Assert.Equal("OCCUPIED", HomeRules.Apply(home, Buy(HomeCatalog.CorkboardId, North(2))).Error?.Code);
        Assert.Equal("INVALID_MOUNT", HomeRules.Apply(home, Buy(HomeCatalog.SignId, new(new(1, 3)))).Error?.Code);
        Assert.Equal("INVALID_MOUNT", HomeRules.Apply(home, Buy(HomeCatalog.BookcaseId, North(1))).Error?.Code);
        home = HomeRules.Apply(home, Buy(HomeCatalog.BookcaseId, new(new(5, 2), 90))).Layout!;
        Assert.Equal(new HomeCell[] { new(5, 2), new(5, 3) },
            HomeRules.Footprint(home.Items.Single(item => item.DefinitionId == HomeCatalog.BookcaseId)));
        home = Place(home, HomeCatalog.DeskId, new(new(7, 5)));
        var desk = home.Items.Single(item => item.DefinitionId == HomeCatalog.DeskId);
        Assert.Equal("INVALID_MOUNT", HomeRules.Apply(home, Buy(HomeCatalog.MoistureId, HomeRules.PortPlacement(desk, "left")!)).Error?.Code);
        home = HomeRules.Apply(home, Buy(HomeCatalog.PlanterId, new(new(3, 3)))).Layout!;
        var plant = home.Items.Single(item => item.DefinitionId == HomeCatalog.PlanterId);
        var soil = HomeRules.PortPlacement(plant, "soil")!;
        home = HomeRules.Apply(home, Buy(HomeCatalog.MoistureId, soil)).Layout!;
        Assert.Equal("OCCUPIED", HomeRules.Apply(home, Buy(HomeCatalog.MoistureId, soil)).Error?.Code);
        home = HomeRules.Apply(home, new("rotate", plant.Id, Placement: new(new(3, 3), 180))).Layout!;
        var probe = home.Items.Single(item => item.DefinitionId == HomeCatalog.MoistureId);
        Assert.Equal(new HomeCell(4, 3), probe.Placement?.Anchor);
        Assert.Equal(180, probe.Placement?.Rotation);
        home = HomeRules.Apply(home, new("store", plant.Id)).Layout!;
        Assert.Null(home.Items.Single(item => item.Id == probe.Id).Placement);
    }

    [Fact]
    public void Code_opens_window_and_changes_indoor_light_without_changing_the_outdoor_sensor()
    {
        var home = Place(HomeCatalog.CreateStarter(), HomeCatalog.ControllerId, North(9));
        home = Place(home, HomeCatalog.BlindId, North(2));
        home = Place(home, HomeCatalog.DaylightId, North(7));
        home = Place(home, HomeCatalog.LampId, new(new(5, 4)));
        home = HomeRules.Apply(home, Buy(HomeCatalog.SignId, North(5))).Layout!;
        var sim = new HomeSimulation(home, HomeCatalog.FreeScenario, "window");
        sim.SetDaylight(420);
        var start = sim.BeginTick();
        var atDesk = new HomeCell(6, 4);
        Assert.Equal(420, start.Sensors.Single(sensor => sensor.Alias == "daylight").Value.GetInt32());
        var closed = sim.Snapshot(true).LightMap.Single(point => point.Cell == atDesk).Lux;
        Assert.InRange(closed, 1, 419);
        Assert.Null(sim.Commit(new("window", start.Tick, 1,
            [new("roller_blind", "setPower", true), new("flip_sign", "setPower", true)], 100)));
        sim.AdvanceTick();
        Assert.Contains("flip_sign", sim.Snapshot().PoweredAliases);
        Assert.Equal(420, sim.Snapshot(true).LightMap.Single(point => point.Cell == atDesk).Lux);
        Assert.Equal(420, sim.BeginTick().Sensors.Single(sensor => sensor.Alias == "daylight").Value.GetInt32());
        Assert.Null(sim.Commit(new("window", sim.Tick, 2,
            [new("roller_blind", "setPower", false), new("desk_lamp", "setPower", true)], null, Completed: true)));
        sim.AdvanceTick();
        Assert.Equal(closed + 338, sim.Snapshot(true).LightMap.Single(point => point.Cell == atDesk).Lux);
    }

    [Fact]
    public void Each_probe_reads_its_own_soil_and_only_the_watered_pot_gets_wetter()
    {
        var home = Place(HomeCatalog.CreateStarter(), HomeCatalog.ControllerId, North(9));
        foreach (var x in new[] { 3, 7 })
        {
            home = HomeRules.Apply(home, Buy(HomeCatalog.PlanterId, new(new(x, 3)))).Layout!;
            var pot = home.Items.Single(item => item.DefinitionId == HomeCatalog.PlanterId && item.Placement!.Anchor.X == x);
            home = HomeRules.Apply(home, Buy(HomeCatalog.MoistureId, HomeRules.PortPlacement(pot, "soil")!)).Layout!;
        }
        var sim = new HomeSimulation(home, HomeCatalog.FreeScenario, "plants");
        var frame = sim.BeginTick();
        Assert.Equal(2, frame.Sensors.Count(sensor => sensor.Unit == "%" && sensor.Value.GetInt32() == 25));
        Assert.Null(sim.Commit(new("plants", frame.Tick, 1, [new("planter_pump", "setPower", true)], 20_000)));
        for (var i = 0; i < 200; i++) sim.AdvanceTick();
        var sensors = sim.BeginTick().Sensors.ToDictionary(sensor => sensor.Alias);
        Assert.Equal(100, sensors["soil_probe"].Value.GetInt32());
        Assert.Equal(21, sensors["soil_probe_2"].Value.GetInt32());
        Assert.Equal(100, sim.Snapshot().SoilMoisture!["planter_pump"]);
        Assert.All(sim.Snapshot(true).LightMap, cell => Assert.Equal(40, cell.Lux));
        Assert.Null(sim.Commit(new("plants", sim.Tick, 2, [new("planter_pump", "setPower", false)], null, Completed: true)));
        for (var i = 0; i < 100; i++) sim.AdvanceTick();
        Assert.Equal(98, sim.Snapshot().SoilMoisture!["planter_pump"]);
        Assert.Equal(98, sim.Snapshot().Sensors.Single(sensor => sensor.Alias == "soil_probe").Value.GetInt32());
    }

    private static HomeBuildAction Buy(string definitionId, HomePlacement placement) =>
        new("buy", DefinitionId: definitionId, Placement: placement);

    private static HomePlacement North(int x) => new(new(x, 0), 180, new(x, 0, true));

    private static HomeLayout Place(HomeLayout home, string definitionId, HomePlacement placement)
    {
        var item = home.Items.Single(item => item.DefinitionId == definitionId);
        return Assert.IsType<HomeLayout>(HomeRules.Apply(home, new("place", item.Id, Placement: placement)).Layout);
    }
}
