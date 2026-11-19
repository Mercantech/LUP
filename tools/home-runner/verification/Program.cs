using System.Text;
using System.Text.Json;
using Mercantec.Tech.Contracts.HomeGame;

Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var home = HomeCatalog.CreateStarter();
home = Place(home, HomeCatalog.DeskId, new(new(6, 3)));
home = Place(home, HomeCatalog.ControllerId, new(new(9, 0), 180, new(9, 0, true)));
var passed = true;

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
    var prerequisite = HomeRules.AssessmentPrerequisites(home, task);
    if (prerequisite is not null)
        throw new InvalidOperationException(prerequisite.Message);

    var aliases = home.Items.ToDictionary(item => item.Alias, item =>
        item.Placement is null ? "uninstalled" : HomeCatalog.Definition(item.DefinitionId)!.Kind);
    foreach (var id in task.ScenarioIds)
    {
        const string run = "pilot-fixture";
        var scenario = HomeCatalog.WithSeed(HomeCatalog.Scenarios[id], 20261116);
        var simulation = new HomeSimulation(home, scenario, run);
        Write(new { type = "scenario", task = task.Id, scenario = id, aliases });
        while (!simulation.Finished)
        {
            var frame = simulation.BeginTick();
            if (frame.ControllerDue)
            {
                var sequence = simulation.NextSequence;
                var activation = sequence.ToString();
                Write(new
                {
                    type = "snapshot", frame.Tick, frame.TimeMs, activation,
                    values = frame.Sensors.ToDictionary(sensor => sensor.Alias, sensor => sensor.Value)
                });
                var response = Console.ReadLine() ?? throw new EndOfStreamException("The runtime did not return a batch.");
                var batch = JsonSerializer.Deserialize<FixtureBatch>(response, json);
                if (batch is not { Type: "batch" } || batch.Activation != activation)
                    throw new InvalidOperationException(response);
                var error = simulation.Commit(new(run, frame.Tick, sequence, batch.Commands, batch.NextDelayMs, batch.Done));
                if (error is not null)
                    throw new InvalidOperationException(error.Message);
            }
            simulation.AdvanceTick();
        }
        var outcome = simulation.Outcome();
        passed &= outcome.Passed;
        Write(new { type = "outcome", task = task.Id, outcome });
    }
}

return passed ? 0 : 1;

void Write<T>(T value) => Console.WriteLine(JsonSerializer.Serialize(value, json));

static HomeLayout Place(HomeLayout layout, string definition, HomePlacement placement)
{
    var item = layout.Items.Single(candidate => candidate.DefinitionId == definition);
    var result = HomeRules.Apply(layout, new("place", item.Id, Placement: placement));
    return result.Layout ?? throw new InvalidOperationException(result.Error?.Message);
}

sealed record FixtureBatch(string Type, string Activation, HomeCommand[] Commands, double NextDelayMs, bool Done);
