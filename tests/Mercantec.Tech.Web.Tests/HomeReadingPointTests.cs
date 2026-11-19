using Mercantec.Tech.Contracts.HomeGame;
using Mercantec.Tech.Web.HomeGame;

namespace Mercantec.Tech.Web.Tests;

public sealed class HomeReadingPointTests
{
    [Fact]
    public void Moved_desk_uses_the_accessible_reading_tile_and_its_measured_lux()
    {
        var layout = HomeCatalog.CreateStarter();
        var desk = layout.Items.Single(item => item.Role == HomeCatalog.PrimaryDeskRole);
        var moved = HomeRules.Apply(layout, new("place", desk.Id, Placement: new(new(2, 6), 90))).Layout!;
        var sim = new HomeSimulation(moved, HomeCatalog.FreeScenario, "reading-probe");
        var sample = sim.Snapshot(includeLightMap: true);
        var target = new HomeCell(1, 6);
        sample = sample with { LightMap = [new(new(6, 4), 420), new(target, 185), new(new(1, 7), 100)] };

        Assert.Equal((target, 185), HomeReadingPoint.Measure(moved, sample));
        Assert.Null(HomeReadingPoint.Measure(moved, sample with { LightMap = [] }));
    }

    [Fact]
    public void A_seated_reader_uses_the_measured_avatar_position_even_if_another_desk_cell_is_closer_to_the_entry()
    {
        var layout = HomeCatalog.CreateStarter();
        var desk = layout.Items.Single(item => item.Role == HomeCatalog.PrimaryDeskRole);
        layout = HomeRules.Apply(layout, new("place", desk.Id, Placement: new(new(6, 3)))).Layout!;
        var atDesk = new HomeCell(7, 4);
        var sim = new HomeSimulation(layout, HomeCatalog.FreeScenario, "seated-reader");
        var reading = sim.Snapshot() with { Activity = "reading", AvatarCell = atDesk, AvatarLux = 368 };

        Assert.Equal((atDesk, 368), HomeReadingPoint.Measure(layout, reading));
        Assert.Null(HomeReadingPoint.Measure(layout, null));
    }
}
