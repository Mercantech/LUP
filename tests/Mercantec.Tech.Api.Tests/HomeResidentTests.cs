using System.Text.Json;
using System.Text.Json.Nodes;
using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Api.Tests;

public sealed class HomeResidentTests
{
    [Fact]
    public void Empty_home_has_a_repeatable_life_cycle_with_changes_in_both_needs()
    {
        var home = HomeCatalog.CreateStarter();
        var resident = HomeSimulation.ForResident(home);
        var replay = HomeSimulation.ForResident(home);
        var previous = home.Grid.Entry;
        var visited = new HashSet<HomeCell>();
        var activities = new HashSet<string>();
        var energy = new HashSet<int>();
        var inspiration = new HashSet<int>();
        for (var i = 0; i < 1800; i++)
        {
            resident.AdvanceTick();
            replay.AdvanceTick();
            var world = resident.Snapshot();
            var copy = replay.Snapshot();
            Assert.False(resident.Finished);
            Assert.Null(world.Error);
            Assert.Equal(world.Resident, copy.Resident);
            Assert.Equal(world.AvatarCell, copy.AvatarCell);
            Assert.Equal(world.Activity, copy.Activity);
            Assert.True(world.Resident!.IsAutonomous);
            Assert.Null(world.Resident.TargetAlias);
            Assert.InRange(world.Resident.Energy, 0, 100);
            Assert.InRange(world.Resident.Inspiration, 0, 100);
            var cell = world.AvatarCell!.Value;
            Assert.True(HomeRules.OnFloor(home.Grid, cell));
            Assert.InRange(HomeRules.DistanceSquared(previous, cell), 0, 1);
            previous = cell;
            visited.Add(cell);
            activities.Add(world.Activity);
            energy.Add(world.Resident.Energy);
            inspiration.Add(world.Resident.Inspiration);
        }
        Assert.True(visited.Count > 5);
        Assert.Contains("exploring", activities);
        Assert.Contains("resting", activities);
        Assert.Contains("lookingOutside", activities);
        Assert.True(energy.Count > 10);
        Assert.True(inspiration.Count > 10);
    }

    [Theory]
    [InlineData(HomeCatalog.ArmchairId, 90)]
    [InlineData(HomeCatalog.SofaId, 270)]
    public void Low_energy_takes_priority_and_recovers_at_the_rotated_seat(string definition, int rotation)
    {
        var home = HomeCatalog.CreateStarter();
        var seat = new HomeItem("seat", definition, "seat", "Hvilested", new(new(5, 3), rotation));
        var lamp = new HomeItem("lamp", HomeCatalog.LampId, "lamp", "Lampe", new(new(1, 4)));
        home = home with { Items = [.. home.Items, seat, lamp] };
        var resident = HomeSimulation.ForResident(home, new(18, 5, "idle", null, null, false));
        var world = resident.Snapshot();
        var goal = world.Resident!.Destination!.Value;
        Assert.Contains(goal, HomeRules.AccessibleInteractions(home, seat));
        Assert.Equal("seat", world.Resident.TargetAlias);
        Assert.Equal("rest", world.Resident.Want);
        var blockers = HomeRules.WalkBlockers(home);
        while (world.Activity == "walking" && resident.Tick < 110)
        {
            resident.AdvanceTick();
            world = resident.Snapshot();
            Assert.DoesNotContain(world.AvatarCell!.Value, blockers);
        }
        Assert.Equal("relaxing", world.Activity);
        Assert.Equal(goal, world.AvatarCell);
        var arrivedWith = world.Resident!.Energy;
        for (var i = 0; i < 30; i++) resident.AdvanceTick();
        world = resident.Snapshot();
        Assert.Equal("relaxing", world.Activity);
        Assert.Equal(arrivedWith + 21, world.Resident!.Energy);
        for (var i = 0; i < 180 && resident.Snapshot().Resident!.Want == "rest"; i++) resident.AdvanceTick();
        Assert.NotEqual("rest", resident.Snapshot().Resident!.Want);
        Assert.InRange(resident.Snapshot().Resident!.Energy, 76, 100);
    }

    [Fact]
    public void Low_inspiration_leads_to_reading_then_the_plant_is_visited_without_operating_the_pump()
    {
        var home = HomeCatalog.CreateStarter();
        var desk = home.Items.Single(item => item.DefinitionId == HomeCatalog.DeskId) with { Placement = new(new(4, 3), 180) };
        var plant = new HomeItem("pot", HomeCatalog.PlanterId, "pump", "Plante", new(new(6, 3), 90));
        home = home with { Items = [.. home.Items.Where(item => item.Id != desk.Id), desk, plant] };
        var resident = HomeSimulation.ForResident(home, new(95, 28, "idle", null, null, false));
        resident.SetDaylight(450);
        var arriving = Until(resident, "reading");
        Assert.Equal("read", arriving.Resident!.Want);
        Assert.Contains(arriving.AvatarCell!.Value, HomeRules.AccessibleInteractions(home, desk));
        for (var i = 0; i < 30; i++) resident.AdvanceTick();
        Assert.Equal(arriving.Resident.Inspiration + 18, resident.Snapshot().Resident!.Inspiration);
        var looking = Until(resident, "visitingPlant");
        Assert.Equal("pump", looking.Resident!.TargetAlias);
        Assert.Equal(looking.AvatarCell, looking.Resident.Destination);
        Assert.Contains(HomeRules.Footprint(plant), cell => HomeRules.DistanceSquared(cell, looking.AvatarCell!.Value) == 1);
        Assert.Empty(looking.PoweredAliases);
    }

    [Fact]
    public void Lighting_the_desk_with_code_increases_the_inspiration_gained_from_reading()
    {
        var starter = HomeCatalog.CreateStarter();
        var desk = starter.Items.Single(item => item.Role == HomeCatalog.PrimaryDeskRole) with { Placement = new(new(4, 3)) };
        var lamp = starter.Items.Single(item => item.DefinitionId == HomeCatalog.LampId) with { Placement = new(new(3, 4)) };
        var home = starter with { Items = [desk, lamp] };
        HomeWorldSnapshot ReadWithLamp(bool on)
        {
            var sim = new HomeSimulation(home, HomeCatalog.FreeScenario, "lamp", new(4, 4), manualControl: true);
            sim.BeginTick();
            Assert.Null(sim.Commit(new("lamp", 0, 1, [new(lamp.Alias, "setPower", on)], null, Completed: true)));
            for (var i = 0; i < 30; i++) sim.AdvanceTick();
            return sim.Snapshot();
        }
        var dark = ReadWithLamp(false);
        var lit = ReadWithLamp(true);
        Assert.Equal("reading", dark.Activity);
        Assert.InRange(dark.AvatarLux!.Value, 0, 299);
        Assert.InRange(lit.AvatarLux!.Value, 300, 600);
        Assert.True(dark.Resident!.Inspiration > 58);
        Assert.True(lit.Resident!.Inspiration > dark.Resident.Inspiration);
    }

    [Fact]
    public void Closing_the_blind_with_code_redirects_the_resident_to_an_available_activity()
    {
        var home = HomeCatalog.CreateStarter();
        var blind = home.Items.Single(item => item.DefinitionId == HomeCatalog.BlindId)
            with { Placement = new(new(2, 0), 180, HomeCatalog.WindowEdge) };
        home = home with { Items = [blind] };
        var sim = new HomeSimulation(home, HomeCatalog.FreeScenario, "window", manualControl: true);
        sim.SetIntention("auto");
        var lookedOutside = false;
        for (var i = 0; i < 180; i++)
        {
            sim.BeginTick();
            var world = sim.Snapshot();
            var atWindow = world.Activity == "lookingOutside";
            if (atWindow) Assert.Equal(new HomeCell(2, 0), world.AvatarCell);
            var commands = i == 0 || atWindow ? new HomeCommand[] { new(blind.Alias, "setPower", !atWindow) } : [];
            Assert.Null(sim.Commit(new("window", sim.Tick, sim.NextSequence, commands, 100)));
            sim.AdvanceTick();
            if (atWindow) { lookedOutside = true; break; }
        }
        Assert.True(lookedOutside);
        sim.BeginTick();
        Assert.DoesNotContain(blind.Alias, sim.Snapshot().PoweredAliases);
        Assert.NotEqual("lookOutside", sim.Snapshot().Resident!.Want);
    }

    [Fact]
    public void The_room_resident_follows_the_visible_light_and_blind_without_sending_any_commands()
    {
        var home = HomeCatalog.CreateStarter();
        var blind = home.Items.Single(item => item.DefinitionId == HomeCatalog.BlindId)
            with { Placement = new(new(2, 0), 180, HomeCatalog.WindowEdge) };
        var lamp = home.Items.Single(item => item.DefinitionId == HomeCatalog.LampId)
            with { Placement = new(new(3, 1)) };
        home = home with { Items = [blind, lamp] };
        var visible = new HomeSimulation(home, HomeCatalog.FreeScenario, "preview", manualControl: true);
        visible.SetDaylight(100);
        visible.BeginTick();
        Assert.Null(visible.Commit(new("preview", 0, 1, [new(lamp.Alias, "setPower", true),
            new(blind.Alias, "setPower", true)], null, Completed: true)));
        visible.AdvanceTick();
        var room = HomeSimulation.ForResident(home);
        room.SetResidentEnvironment(visible.Snapshot(true));
        var window = Until(room, "lookingOutside");
        Assert.Empty(window.LastCommands);
        Assert.Empty(window.DeviceCommits!);
        Assert.True(window.AvatarLux > window.DaylightLux);
        Assert.Equal(100, window.DaylightLux);
        Assert.Equal(visible.Snapshot(true).LightMap.Single(cell => cell.Cell == window.AvatarCell).Lux, window.AvatarLux);
        room.SetResidentEnvironment(null);
        room.AdvanceTick();
        Assert.Empty(room.Snapshot().PoweredAliases);
        Assert.NotEqual("lookOutside", room.Snapshot().Resident!.Want);
        visible.SetResidentEnvironment(null);
        Assert.Contains(lamp.Alias, visible.Snapshot().PoweredAliases);
    }

    [Fact]
    public void An_edited_room_revalidates_the_saved_position_and_wish_and_can_recover_on_a_single_tile()
    {
        var home = HomeCatalog.CreateStarter();
        var desk = home.Items.Single(item => item.DefinitionId == HomeCatalog.DeskId) with { Placement = new(new(5, 3)) };
        var entry = home.Grid.Entry;
        var sealedRoom = home with
        {
            Items = [desk],
            Grid = home.Grid with { Walls = [.. home.Grid.Walls, new(new(0, 4, true)), new(new(1, 4, false)), new(new(0, 5, true))] }
        };
        var stale = new HomeResidentState(-90, 200, "read", desk.Alias, new(5, 4), true);
        var resident = HomeSimulation.ForResident(sealedRoom, stale, new(8, 4));
        Assert.Equal(entry, resident.Snapshot().AvatarCell);
        Assert.Equal("resting", resident.Snapshot().Activity);
        Assert.Equal(0, resident.Snapshot().Resident!.Energy);
        Assert.Equal(100, resident.Snapshot().Resident!.Inspiration);
        var active = Until(resident, "exploring");
        Assert.Equal(entry, active.AvatarCell);
        Assert.Equal(entry, active.Resident!.Destination);
        Assert.True(active.Resident.Energy >= 76);
    }

    [Fact]
    public void Restored_manual_intents_take_effect_before_the_first_step()
    {
        var home = HomeCatalog.CreateStarter();
        var savedCell = new HomeCell(3, 4);
        var idle = HomeSimulation.ForResident(home, previousCell: savedCell, intent: "idle");
        for (var i = 0; i < 30; i++) idle.AdvanceTick();
        Assert.Equal(savedCell, idle.Snapshot().AvatarCell);
        Assert.Equal("idle", idle.Snapshot().Activity);
        var leaving = HomeSimulation.ForResident(home, previousCell: savedCell, intent: "away");
        Assert.Equal("walking", leaving.Snapshot().Activity);
        Assert.Null(Until(leaving, "away").AvatarCell);
        var absent = HomeSimulation.ForResident(home, intent: "away");
        Assert.Null(absent.Snapshot().AvatarCell);
        absent.SetIntention("auto");
        absent.AdvanceTick();
        absent.AdvanceTick();
        Assert.True(absent.Snapshot().Resident!.IsAutonomous);
        Assert.Equal(home.Grid.Entry, absent.Snapshot().AvatarCell);
    }

    [Fact]
    public void A_checkpoint_restores_a_reachable_wish_and_old_json_still_deserializes()
    {
        var home = HomeCatalog.CreateStarter();
        var first = HomeSimulation.ForResident(home);
        for (var i = 0; i < 11; i++) first.AdvanceTick();
        var checkpoint = JsonSerializer.Deserialize<HomeWorldSnapshot>(JsonSerializer.Serialize(first.Snapshot()))!;
        var resumed = HomeSimulation.ForResident(home, checkpoint.Resident, checkpoint.AvatarCell).Snapshot();
        Assert.Equal(checkpoint.AvatarCell, resumed.AvatarCell);
        Assert.Equal(checkpoint.Resident, resumed.Resident);
        var old = JsonSerializer.SerializeToNode(checkpoint)!.AsObject();
        old.Remove("Resident");
        Assert.Null(old.Deserialize<HomeWorldSnapshot>()!.Resident);
    }

    private static HomeWorldSnapshot Until(HomeSimulation sim, string activity)
    {
        for (var i = 0; i < 600 && sim.Snapshot().Activity != activity; i++) sim.AdvanceTick();
        var world = sim.Snapshot();
        Assert.Null(world.Error);
        Assert.Equal(activity, world.Activity);
        return world;
    }
}
