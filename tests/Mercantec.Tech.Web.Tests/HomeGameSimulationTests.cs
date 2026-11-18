using System.Text.Json;
using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Web.Tests;

public sealed class HomeGameSimulationTests
{
    [Fact]
    public void Desk_rotation_transforms_the_footprint_and_external_interaction_cells()
    {
        var desk = HomeCatalog.CreateStarter().Items.Single(item => item.DefinitionId == HomeCatalog.DeskId);
        var rotated = desk with { Placement = desk.Placement! with { Rotation = 90 } };

        Assert.Equal(new HomeCell[] { new(6, 3), new(6, 4) }, HomeRules.Footprint(rotated));
        Assert.Equal(new HomeCell[] { new(5, 3), new(5, 4) }, HomeRules.Interactions(rotated));
    }

    [Fact]
    public void Invalid_purchase_keeps_the_entry_furniture_access_items_and_coins_unchanged()
    {
        var layout = HomeCatalog.CreateStarter();
        var outside = HomeRules.Apply(layout, Buy(HomeCatalog.SofaId, 11, 9));
        var atEntry = HomeRules.Apply(layout, Buy(HomeCatalog.LampId, 0, 4));
        var blocksDesk = HomeRules.Apply(layout, Buy(HomeCatalog.SofaId, 6, 4));
        var atAvatar = HomeRules.Apply(layout, Buy(HomeCatalog.LampId, 2, 2), new(2, 2));

        Assert.Equal("OUT_OF_BOUNDS", outside.Error?.Code);
        Assert.Equal("BLOCKED_ACCESS", atEntry.Error?.Code);
        Assert.Equal("BLOCKED_ACCESS", blocksDesk.Error?.Code);
        Assert.Equal("BLOCKED_ACCESS", atAvatar.Error?.Code);
        Assert.All(new[] { outside, atEntry, blocksDesk, atAvatar }, failed =>
        {
            Assert.Null(failed.Layout);
            Assert.Equal(0, failed.GoldDelta);
        });
        Assert.Equal(1, layout.Revision);
        Assert.Equal(8, layout.Items.Count);
    }

    [Fact]
    public void Rug_overlaps_a_sofa_on_its_own_layer_and_does_not_change_navigation()
    {
        var layout = HomeCatalog.CreateStarter();
        var sofa = HomeRules.Apply(layout, Buy(HomeCatalog.SofaId, 2, 1));
        var rug = HomeRules.Apply(Assert.IsType<HomeLayout>(sofa.Layout), Buy(HomeCatalog.RugId, 2, 1));
        var duplicateRug = HomeRules.Apply(Assert.IsType<HomeLayout>(rug.Layout), Buy(HomeCatalog.RugId, 3, 1));

        Assert.Equal(-90, sofa.GoldDelta);
        Assert.Equal(-30, rug.GoldDelta);
        Assert.Equal("OCCUPIED", duplicateRug.Error?.Code);
        Assert.Contains(new HomeCell(2, 1), HomeRules.WalkBlockers(rug.Layout!));
        Assert.DoesNotContain(new HomeCell(2, 2), HomeRules.WalkBlockers(rug.Layout!));
    }

    [Fact]
    public void Mounted_fixture_tracks_its_host_through_move_rotate_and_storage()
    {
        var layout = HomeCatalog.CreateStarter();
        var desk = layout.Items.Single(item => item.DefinitionId == HomeCatalog.DeskId);
        var sensor = new HomeItem("fixture_light", "fixture_indoor_sensor", "indoor_light", "Lysføler",
            HomeRules.PortPlacement(desk, "right"));
        layout = layout with { Items = [.. layout.Items, sensor] };
        Assert.Null(HomeRules.Validate(layout));

        var moving = HomeRules.Apply(layout, new("place", desk.Id, Placement: new(new(2, 6), 90)));
        var moved = Assert.IsType<HomeLayout>(moving.Layout);
        var movedSensor = moved.Items.Single(item => item.Id == sensor.Id);
        Assert.Equal("indoor_light", movedSensor.Alias);
        Assert.Equal(new HomeCell(2, 7), movedSensor.Placement?.Anchor);
        Assert.Equal(90, movedSensor.Placement?.Rotation);

        var storing = HomeRules.Apply(moved, new("store", desk.Id));
        Assert.Null(Assert.IsType<HomeLayout>(storing.Layout).Items.Single(item => item.Id == sensor.Id).Placement);
    }

    [Fact]
    public void Presence_includes_radius_boundary_and_wall_blocks_sight()
    {
        var layout = HomeCatalog.CreateStarter();
        var sensor = layout.Items.Single(item => item.DefinitionId == HomeCatalog.PresenceId)
            with { Placement = new(new(2, 2)) };
        Assert.Contains(new HomeCell(6, 2), HomeRules.Range(layout, sensor));
        Assert.DoesNotContain(new HomeCell(6, 3), HomeRules.Range(layout, sensor));

        var edge = new HomeEdge(4, 2, false);
        var withWall = layout with { Grid = layout.Grid with { Walls = [.. layout.Grid.Walls, new(edge)] } };
        var withDoor = layout with { Grid = layout.Grid with { Walls = [.. layout.Grid.Walls, new(edge, Open: true)] } };
        Assert.DoesNotContain(new HomeCell(6, 2), HomeRules.Range(withWall, sensor));
        Assert.Contains(new HomeCell(6, 2), HomeRules.Range(withDoor, sensor));
    }

    [Fact]
    public void Supercover_blocks_light_and_presence_through_a_corner_touch()
    {
        var layout = HomeCatalog.CreateStarter();
        var cabinet = new HomeItem("cabinet", "fixture_cabinet", "cabinet", "Skab", new(new(2, 3)));
        var blockedByCell = layout with { Items = [.. layout.Items, cabinet] };
        var blockedByEdge = layout with
        {
            Grid = layout.Grid with { Walls = [.. layout.Grid.Walls, new(new(2, 3, true))] }
        };

        Assert.True(HomeRules.HasSight(layout, new(2, 2), new(3, 3)));
        Assert.False(HomeRules.HasSight(blockedByCell, new(2, 2), new(3, 3)));
        Assert.False(HomeRules.HasSight(blockedByEdge, new(2, 2), new(3, 3), presence: true));
    }

    [Theory]
    [InlineData(0, 360)]
    [InlineData(1, 338)]
    [InlineData(4, 270)]
    [InlineData(5, 248)]
    [InlineData(16, 0)]
    public void Lamp_falloff_rounds_half_up_and_has_zero_light_at_radius(int distanceSquared, int expected)
    {
        var lamp = HomeCatalog.Definition(HomeCatalog.LampId)!;
        Assert.Equal(expected, HomeRules.LampLux(lamp, distanceSquared));
        Assert.Equal(99, HomeRules.Comfort(601));
    }

    [Fact]
    public void Combined_light_is_autonomous_and_daylight_sensor_does_not_see_lamps()
    {
        var layout = ReferenceLayout();
        var second = HomeRules.Apply(layout, Buy(HomeCatalog.LampId, 7, 4));
        var simulation = new HomeSimulation(Assert.IsType<HomeLayout>(second.Layout),
            HomeCatalog.Scenarios["dark_reading"], "two-lamps", new(6, 4));
        var frame = simulation.BeginTick();
        Assert.Null(simulation.Commit(new("two-lamps", frame.Tick, 1,
            [new("desk_lamp", "setPower", true), new(HomeCatalog.LampId, "setPower", true)], null, Completed: true)));
        simulation.AdvanceTick();
        Assert.True(simulation.ControllerFinished);

        var view = simulation.Snapshot(includeLightMap: true);
        Assert.Equal(716, view.LightMap.Single(cell => cell.Cell == new HomeCell(6, 4)).Lux);
        Assert.Equal(716, view.AvatarLux);
        Assert.Equal(1_200_000L, view.EnergyMicroJ);
        Assert.Equal(40, Sensor(simulation.BeginTick(), "daylight").GetInt32());
        Assert.Equal(71, view.Comfort);
    }

    [Fact]
    public void Shared_light_mask_respects_falloff_and_opaque_objects()
    {
        var layout = ReferenceLayout();
        var lamp = layout.Items.Single(item => item.DefinitionId == HomeCatalog.LampId);
        Assert.Contains(new HomeCell(7, 5), HomeRules.Range(layout, lamp));
        Assert.Equal(288, 40 + HomeRules.LightContribution(layout, lamp)[new(7, 5)]);
        Assert.DoesNotContain(new HomeCell(9, 4), HomeRules.Range(layout, lamp));

        var cabinet = new HomeItem("cabinet", "fixture_cabinet", "cabinet", "Skab", new(new(4, 4)));
        var blocked = layout with { Items = [.. layout.Items, cabinet] };
        Assert.DoesNotContain(new HomeCell(3, 4), HomeRules.Range(blocked, lamp));
        Assert.False(HomeRules.LightContribution(blocked, lamp).ContainsKey(new(3, 4)));
    }

    [Fact]
    public void Breadth_first_navigation_chooses_north_then_east_and_keeps_the_source_for_five_ticks()
    {
        var layout = HomeCatalog.CreateStarter();
        Assert.Equal(new HomeCell[] { new(0, 4), new(0, 3), new(1, 3) },
            HomeRules.FindPath(layout, new(0, 4), [new(1, 3)]));
        var simulation = new HomeSimulation(layout, HomeCatalog.FreeScenario, "walking");
        var first = simulation.BeginTick();
        Assert.Null(simulation.Commit(new("walking", first.Tick, 1, [], null, Completed: true)));
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(new HomeCell(0, 4), simulation.Snapshot().AvatarCell);
            simulation.AdvanceTick();
        }
        simulation.BeginTick();
        Assert.Equal(new HomeCell(1, 4), simulation.Snapshot().AvatarCell);
    }

    [Fact]
    public void Immutable_sensor_snapshot_and_rounded_virtual_wait_share_a_tick_boundary()
    {
        var layout = ReferenceLayout();
        var desk = layout.Items.Single(item => item.DefinitionId == HomeCatalog.DeskId);
        var indoor = new HomeItem("light", "fixture_indoor_sensor", "indoor_light", "Lysføler",
            HomeRules.PortPlacement(desk, "left"));
        layout = layout with { Items = [.. layout.Items, indoor] };
        var simulation = new HomeSimulation(layout, HomeCatalog.Scenarios["dark_reading"], "snapshot", new(6, 4));
        var start = simulation.BeginTick();
        Assert.True(Sensor(start, "presence").GetBoolean());
        Assert.Null(simulation.Commit(new("snapshot", 0, 1, [new("desk_lamp", "setPower", true)], 101)));
        Assert.Equal(40, Sensor(simulation.BeginTick(), "indoor_light").GetInt32());
        Assert.Equal(378, simulation.Snapshot().AvatarLux);
        var paused = simulation.Snapshot();
        Assert.Equal(paused.TimeMs, simulation.Snapshot().TimeMs);
        Assert.Equal(paused.EnergyMicroJ, simulation.Snapshot().EnergyMicroJ);
        simulation.AdvanceTick();
        var next = simulation.BeginTick();
        Assert.False(next.ControllerDue);
        Assert.Equal(355, Sensor(next, "indoor_light").GetInt32());
        Assert.True(Sensor(next, "presence").GetBoolean());
        simulation.AdvanceTick();
        Assert.True(simulation.BeginTick().ControllerDue);
    }

    [Fact]
    public void Batch_rejects_forged_future_and_duplicate_activations_and_discards_invalid_commands_atomically()
    {
        var simulation = new HomeSimulation(ReferenceLayout(), HomeCatalog.FreeScenario, "valid");
        simulation.BeginTick();
        var on = new HomeCommand("desk_lamp", "setPower", true);
        var good = new HomeCommandBatch("valid", 0, 1, [on], 0);
        Assert.Equal("INVALID_BATCH", simulation.Commit(good with { RunId = "forged" })?.Code);
        Assert.Equal("INVALID_BATCH", simulation.Commit(good with { Tick = 1 })?.Code);
        Assert.Equal("INVALID_BATCH", simulation.Commit(good with { Sequence = 2 })?.Code);
        Assert.Empty(simulation.Snapshot().PoweredAliases);
        Assert.Null(simulation.Commit(good));
        Assert.Equal("INVALID_BATCH", simulation.Commit(good)?.Code);
        simulation.AdvanceTick();
        simulation.BeginTick();
        var invalid = good with { Tick = 1, Sequence = 2, Commands = [on with { Value = false }, new("daylight", "setPower", false)] };
        Assert.Equal("INVALID_COMMAND", simulation.Commit(invalid)?.Code);
        Assert.Equal(new[] { "desk_lamp" }, simulation.Snapshot().PoweredAliases);
        Assert.True(simulation.Finished);
        Assert.False(simulation.Outcome().Passed);
    }

    [Fact]
    public void Aliases_are_unique_including_storage_and_survive_cosmetic_renaming()
    {
        var layout = HomeCatalog.CreateStarter();
        var result = HomeRules.Apply(layout, Buy(HomeCatalog.LampId, 4, 2));
        var next = Assert.IsType<HomeLayout>(result.Layout);
        var lamp = next.Items.Single(item => item.Alias == HomeCatalog.LampId);
        var duplicate = HomeRules.Apply(next, new("alias", lamp.Id, Value: "desk_lamp"));
        var invalid = HomeRules.Apply(next, new("alias", lamp.Id, Value: "lamp\n"));
        var rename = HomeRules.Apply(next, new("rename", lamp.Id, Value: "Min lampe"));
        Assert.Equal("ALIAS_IN_USE", duplicate.Error?.Code);
        Assert.Equal("INVALID_ALIAS", invalid.Error?.Code);
        Assert.Equal(HomeCatalog.LampId, rename.Layout?.Items.Single(item => item.Id == lamp.Id).Alias);
    }

    [Theory]
    [InlineData("P01")]
    [InlineData("P02")]
    [InlineData("P03")]
    [InlineData("P04")]
    [InlineData("P05")]
    [InlineData("P06")]
    public void Every_pilot_task_is_solvable_and_scores_actual_activity_and_lamp_state(string taskId)
    {
        var task = HomeCatalog.Tasks.Single(candidate => candidate.Id == taskId);
        var layout = taskId == "P02" ? MovedDeskLayout() : ReferenceLayout();
        if (taskId == "P02")
            Assert.Equal("GOAL_PLACEMENT", HomeRules.AssessmentPrerequisites(ReferenceLayout(), task)?.Code);
        Assert.Null(HomeRules.AssessmentPrerequisites(layout, task));
        foreach (var scenarioId in task.ScenarioIds)
        {
            var scenario = HomeCatalog.WithSeed(HomeCatalog.Scenarios[scenarioId], 20261116);
            var simulation = new HomeSimulation(layout, scenario, "assessment");
            Finish(simulation, taskId, "assessment");
            var outcome = simulation.Outcome();
            Assert.True(outcome.Passed, $"{taskId} / {scenarioId}: {JsonSerializer.Serialize(outcome)}");
            if (taskId == "P02") Assert.Equal(new HomeCell(2, 7), simulation.Snapshot().AvatarCell);
            if (scenarioId == "auto_switch")
            {
                var off = Assert.Single(outcome.Criteria, criterion => criterion.Id == "off_2");
                Assert.Equal(290, off.Samples);
                Assert.Equal(290, off.Matched);
                Assert.DoesNotContain(outcome.Criteria, criterion => criterion.Id == "comfort_2");
            }
        }
    }

    [Fact]
    public void Device_placement_stays_valid_for_existing_simulation_fixtures()
    {
        var home = HomeCatalog.CreateStarter();
        var deskId = home.Items.Single(item => item.Role == HomeCatalog.PrimaryDeskRole).Id;
        var lightId = home.Items.Single(item => item.DefinitionId == HomeCatalog.LampId).Id;
        var daylight = home.Items.Single(item => item.DefinitionId == HomeCatalog.DaylightId);
        var sensor = HomeRules.Apply(home, new("place", daylight.Id, Placement: new(new(7, 0), 180, new(7, 0, true))));
        Assert.Null(sensor.Error);

        foreach (var task in HomeCatalog.Tasks)
        {
            home = task.Id switch
            {
                "P01" => Place(home, HomeCatalog.LampId, new(new(5, 4))),
                "P02" => Place(Place(home, HomeCatalog.DeskId, new(new(2, 6))),
                    HomeCatalog.LampId, new(new(3, 7))),
                "P03" => Place(home, HomeCatalog.DaylightId, new(new(7, 0), 180, new(7, 0, true))),
                "P04" => Place(home, HomeCatalog.PresenceId, new(new(2, 9), 0, new(2, 10, true))),
                "P05" => Place(home, HomeCatalog.SwitchId, new(new(8, 0), 180, new(8, 0, true))),
                _ => home
            };
            Assert.Null(HomeRules.AssessmentPrerequisites(home, task));
            foreach (var scenarioId in task.ScenarioIds)
            {
                var run = new HomeSimulation(home, HomeCatalog.Scenarios[scenarioId], "progression");
                Finish(run, task.Id, "progression");
                Assert.True(run.Outcome().Passed, $"{task.Id} / {scenarioId}: {JsonSerializer.Serialize(run.Outcome())}");
            }
        }

        var reloaded = JsonSerializer.Deserialize<HomeLayout>(JsonSerializer.Serialize(home))!;
        Assert.Equal(new HomeCell(2, 6), reloaded.Items.Single(item => item.Id == deskId).Placement?.Anchor);
        Assert.Equal("desk_lamp", reloaded.Items.Single(item => item.Id == lightId).Alias);
        Assert.Equal(home.Revision, reloaded.Revision);
    }

    [Fact]
    public void Constant_lighting_fails_day_and_empty_home_and_recorded_commands_replay_tick_by_tick()
    {
        var layout = ReferenceLayout();
        var scenario = HomeCatalog.Scenarios["day_reading"];
        var run = new HomeSimulation(layout, scenario, "record");
        var replay = new HomeSimulation(layout, scenario, "record");
        var recorded = new List<HomeCommandBatch>();
        var snapshots = new List<string>();
        while (!run.Finished)
        {
            var frame = run.BeginTick();
            if (frame.ControllerDue)
            {
                var command = Solution("P01", frame, run.NextSequence, "record");
                recorded.Add(command);
                Assert.Null(run.Commit(command));
            }
            run.AdvanceTick();
            snapshots.Add(JsonSerializer.Serialize(run.Snapshot()));
        }
        Assert.False(run.Outcome().Passed);
        Assert.Contains(run.Outcome().Criteria, criterion => criterion.Id == "off_1" && !criterion.Passed);
        var away = new HomeSimulation(layout, HomeCatalog.Scenarios["dark_away"], "away");
        Finish(away, "P01", "away");
        Assert.False(away.Outcome().Passed);

        var batches = recorded.ToDictionary(batch => batch.Tick);
        for (var tick = 0; !replay.Finished; tick++)
        {
            var frame = replay.BeginTick();
            if (frame.ControllerDue) Assert.Null(replay.Commit(batches[tick]));
            replay.AdvanceTick();
            Assert.Equal(snapshots[tick], JsonSerializer.Serialize(replay.Snapshot()));
        }
        Assert.Equal(JsonSerializer.Serialize(run.Outcome()), JsonSerializer.Serialize(replay.Outcome()));
    }

    private static HomeLayout ReferenceLayout()
    {
        var home = HomeCatalog.CreateStarter();
        home = Place(home, HomeCatalog.LampId, new(new(5, 4)));
        home = Place(home, HomeCatalog.DaylightId, new(new(7, 0), 180, new(7, 0, true)));
        home = Place(home, HomeCatalog.PresenceId, new(new(6, 0), 180, new(6, 0, true)));
        return Place(home, HomeCatalog.SwitchId, new(new(8, 0), 180, new(8, 0, true)))
            with { LayoutVersion = "reference_home_v1" };
    }

    private static HomeLayout MovedDeskLayout()
    {
        var home = Place(HomeCatalog.CreateStarter(), HomeCatalog.DeskId, new(new(2, 6)));
        return Place(home, HomeCatalog.LampId, new(new(3, 7))) with { LayoutVersion = "p02_reference_home_v1" };
    }

    private static HomeLayout Place(HomeLayout layout, string definition, HomePlacement placement)
    {
        var item = layout.Items.Single(candidate => candidate.DefinitionId == definition);
        return Assert.IsType<HomeLayout>(HomeRules.Apply(layout, new("place", item.Id, Placement: placement)).Layout);
    }

    private static HomeBuildAction Buy(string definition, int x, int y) =>
        new("buy", DefinitionId: definition, Placement: new(new(x, y)));

    private static JsonElement Sensor(HomeFrame frame, string alias) =>
        frame.Sensors.Single(sensor => sensor.Alias == alias).Value;

    private static void Finish(HomeSimulation simulation, string task, string runId)
    {
        while (!simulation.Finished)
        {
            var frame = simulation.BeginTick();
            if (frame.ControllerDue) Assert.Null(simulation.Commit(Solution(task, frame, simulation.NextSequence, runId)));
            simulation.AdvanceTick();
        }
    }

    private static HomeCommandBatch Solution(string task, HomeFrame frame, long sequence, string runId)
    {
        var power = task switch
        {
            "P01" or "P02" => true,
            "P03" => Sensor(frame, "daylight").GetInt32() < 300,
            "P04" => Sensor(frame, "daylight").GetInt32() < 300 && Sensor(frame, "presence").GetBoolean(),
            _ => Sensor(frame, "auto_enabled").GetBoolean() && Sensor(frame, "daylight").GetInt32() < 300
                && Sensor(frame, "presence").GetBoolean()
        };
        return new(runId, frame.Tick, sequence, [new("desk_lamp", "setPower", power)], 200, task is "P01" or "P02");
    }
}
