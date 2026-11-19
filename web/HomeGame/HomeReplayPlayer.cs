using Mercantec.Tech.Contracts.HomeGame;

namespace Mercantec.Tech.Web.HomeGame;

internal sealed class HomeReplayPlayer(HomeRunReplay replay)
{
    private HomeSimulation? _simulation;
    private ILookup<int, HomeReplayEvent> _events = Array.Empty<HomeReplayEvent>().ToLookup(entry => entry.Tick);

    public HomeLayout Layout => replay.Layout;
    public IReadOnlyList<HomeScenario> Scenarios => replay.Scenarios;
    public int ScenarioIndex { get; private set; } = -1;
    public int Tick => _simulation?.Tick ?? 0;
    public int LastTick => ScenarioIndex < 0 ? 0 : Math.Min(Scenarios[ScenarioIndex].Ticks,
        ScenarioIndex == replay.Run.ScenarioIndex ? replay.Run.Snapshot?.Tick ?? 0 : Scenarios[ScenarioIndex].Ticks);
    public HomeWorldSnapshot? Snapshot { get; private set; }

    public void Seek(int scenarioIndex, int target)
    {
        if (scenarioIndex < 0 || scenarioIndex >= Scenarios.Count) return;
        const string binding = "playback";
        if (_simulation is null || scenarioIndex != ScenarioIndex || target <= 0 || target < Tick)
        {
            ScenarioIndex = scenarioIndex;
            var scenario = Scenarios[scenarioIndex];
            _simulation = new(Layout, scenario, binding);
            if (scenario.Free) _simulation.SetIntention(replay.InitialIntent);
            _events = replay.Events.Where(entry => entry.ScenarioIndex == scenarioIndex).ToLookup(entry => entry.Tick);
        }

        var stopAt = Math.Min(target, LastTick);
        while (_simulation.Tick < stopAt && !_simulation.Finished)
        {
            var events = _events[_simulation.Tick];
            foreach (var input in events)
            {
                if (input.Intent is { } intent) _simulation.SetIntention(intent);
                if (input.DaylightLux is { } lux) _simulation.SetDaylight(lux);
                if (input.AutoEnabled is { } enabled) _simulation.SetAutoEnabled(enabled);
            }

            var frame = _simulation.BeginTick();
            foreach (var entry in events)
            {
                if (entry.Error is { } error)
                {
                    _simulation.Fault(error);
                    break;
                }
                if (frame.ControllerDue && entry is { Intent: null, DaylightLux: null, AutoEnabled: null })
                    _simulation.Commit(new(binding, frame.Tick, _simulation.NextSequence, entry.Commands,
                        entry.WaitMs, entry.ControllerCompleted));
            }
            _simulation.AdvanceTick();
        }
        if (_simulation.Tick == LastTick && _events[LastTick].FirstOrDefault(entry => entry.Error is not null)?.Error is { } fault)
        {
            _simulation.BeginTick();
            _simulation.Fault(fault);
        }
        Snapshot = _simulation.Snapshot(includeLightMap: true);
        if (replay.Run.Snapshot?.Resident is null) Snapshot = Snapshot with { Resident = null };
    }
}
