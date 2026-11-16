using Mercantec.Tech.Api.Arena;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Tests;

public sealed class BilboArenaTests
{
    private static ArenaSimulation Match(bool training = false)
    {
        var simulation = new ArenaSimulation(training: training);
        simulation.AddFighter("one", "Bilbo Backend", AvatarEconomy.BilboId);
        simulation.AddFighter("two", "Modspiller", AvatarEconomy.StarterId);
        simulation.Start();
        for (var tick = 0; tick < 211; tick++) simulation.Tick();
        return simulation;
    }

    private static ArenaFighterSnapshot Bilbo(ArenaSimulation simulation) =>
        simulation.Snapshot().Fighters.Single(fighter => fighter.Id == "one");

    private static void Step(ArenaSimulation simulation, ArenaInput? input = null, ArenaInput? rival = null)
    {
        var sequence = simulation.Snapshot().Tick + 1L;
        simulation.SetInput("one", (input ?? new()) with { Sequence = sequence });
        simulation.SetInput("two", (rival ?? new()) with { Sequence = sequence });
        simulation.Tick();
    }

    private static void StepMany(ArenaSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) Step(simulation);
    }

    [Fact]
    public void JarLeavesAtFrameFourAndBecomesATemporaryFloorPatch()
    {
        var simulation = Match();
        Step(simulation, new(Special: true));
        Assert.Equal(86, Bilbo(simulation).Combat!.Energy);
        for (var tick = 0; tick < 15; tick++)
        {
            Step(simulation);
            Assert.Empty(simulation.Snapshot().Projectiles);
        }
        Step(simulation);
        Assert.Equal(4, Bilbo(simulation).ActionFrame);
        var jar = Assert.Single(simulation.Snapshot().Projectiles);
        Assert.Equal("jam-jar", jar.Kind);
        Assert.Equal(AvatarEconomy.BilboId, jar.SourceAvatarId);

        // Keep the target from intercepting the jar before its second landing.
        for (var tick = 0; tick < 98 && simulation.Snapshot().Projectiles.All(p => p.Kind != "jam-patch"); tick++)
            Step(simulation, rival: new(Horizontal: 1));

        var patch = Assert.Single(simulation.Snapshot().Projectiles);
        Assert.Equal(jar.Id, patch.Id);
        Assert.Equal("jam-patch", patch.Kind);
        Assert.Equal(0, patch.Vx);
        Assert.Equal(0, patch.Vy);
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "jar-splat");
        StepMany(simulation, 110);
        Assert.Empty(simulation.Snapshot().Projectiles);
    }

    [Fact]
    public void OrdrekoeenUsesItsMeterAndReleasesThreeJarsOnSeparateFrames()
    {
        var standard = Match();
        Step(standard, new(Smash: true));
        Assert.Equal(0, Bilbo(standard).Combat!.ChannelRemaining);
        Assert.Empty(standard.Snapshot().Projectiles);

        var simulation = Match(training: true);
        Step(simulation, new(Smash: true));
        Assert.Equal(0, Bilbo(simulation).Combat!.Ultimate);
        Assert.Equal("order-queue-charge", Bilbo(simulation).Combat!.ActiveAbility);
        StepMany(simulation, 48);
        Assert.Equal("ultimate_release", Bilbo(simulation).Action);
        Assert.Empty(simulation.Snapshot().Projectiles);

        for (var index = 0; index < 3; index++)
        {
            StepMany(simulation, 11);
            Assert.Equal(index, simulation.Snapshot().Projectiles.Count);
            Step(simulation);
            Assert.Equal((index + 1) * 2, Bilbo(simulation).ActionFrame);
            Assert.Equal(index + 1, simulation.Snapshot().Projectiles.Count);
        }
        Assert.All(simulation.Snapshot().Projectiles, jar =>
        {
            Assert.Equal("order-jar", jar.Kind);
            Assert.Equal(AvatarEconomy.BilboId, jar.SourceAvatarId);
        });
        Assert.Single(simulation.Snapshot().Events, item => item.Type == "order-queue");
    }

    [Fact]
    public void ReconnectionDiscardsTheRemainingOrders()
    {
        var simulation = Match(training: true);
        Step(simulation, new(Smash: true));
        StepMany(simulation, 60);
        Assert.Single(simulation.Snapshot().Projectiles);
        simulation.SetConnected("one", false);
        simulation.SetConnected("one", true);
        StepMany(simulation, 34);
        Assert.DoesNotContain(simulation.Snapshot().Events, item => item.Type == "projectile" && item.ActorId == "one");
    }

    [Fact]
    public void GlasvaernAbsorbsWithoutIncreasingLagerOrUltimate()
    {
        var simulation = Match();
        Step(simulation, new(Shield: true), new(Special: true));
        for (var tick = 0; tick < 60 && simulation.Snapshot().Events.All(item => item.Type != "glass-block"); tick++)
            Step(simulation, new(Shield: true));
        Assert.Contains(simulation.Snapshot().Events, item => item.Type == "glass-block" && item.ActorId == "one");
        Assert.Empty(simulation.Snapshot().Projectiles);
        Assert.Equal(0, Bilbo(simulation).Damage);
        Assert.Equal(100, Bilbo(simulation).Combat!.Energy);
        Assert.Equal(0, Bilbo(simulation).Combat!.Ultimate);
    }
}
