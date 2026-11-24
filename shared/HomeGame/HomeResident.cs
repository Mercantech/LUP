namespace Mercantec.Tech.Contracts.HomeGame;

internal sealed class HomeResident(HomeLayout layout)
{
    private sealed record Place(string Want, string? Alias, HomeCell Cell);

    private static readonly string[] Wishes = ["wander", "lookOutside", "read", "visitPlant", "rest"];
    private IReadOnlyList<Place>? _places;
    private IReadOnlyList<HomeCell>? _floor;
    private bool _resume;
    private int? _nextChoice;

    public HomeResidentState State { get; private set; } = new(72, 58, "idle", null, null, false);

    public void Restore(HomeResidentState previous)
    {
        State = previous with { Energy = Math.Clamp(previous.Energy, 0, 100), Inspiration = Math.Clamp(previous.Inspiration, 0, 100) };
        _resume = previous.IsAutonomous;
    }

    public void Manual(string intent, string? alias = null, HomeCell? destination = null)
    {
        _resume = false;
        _nextChoice = null;
        State = State with { Want = intent, TargetAlias = alias, Destination = destination, IsAutonomous = false };
    }

    public bool Ready(int tick) => _nextChoice is { } next && tick >= next &&
        (State.Want != "rest" || State.Energy >= 76);

    public string Arrive(int tick)
    {
        _nextChoice = tick + (State.Want is "wander" ? 20 : 45);
        return State.Want switch
        {
            "read" => "reading",
            "rest" => State.TargetAlias is null ? "resting" : "relaxing",
            "lookOutside" => "lookingOutside",
            "visitPlant" => "visitingPlant",
            _ => "exploring"
        };
    }

    public void AdvanceNeeds(string activity, int readingComfort)
    {
        if (activity == "away") return;
        var (energy, inspiration) = activity switch
        {
            "walking" => (-2, -1),
            "relaxing" => (7, -1),
            "resting" => (4, -1),
            "reading" => (-1, 3 + 3 * readingComfort / 100),
            "lookingOutside" or "visitingPlant" => (-1, 4),
            "exploring" => (-1, 2),
            _ => (1, -1)
        };
        State = State with
        {
            Energy = Math.Clamp(State.Energy + energy, 0, 100),
            Inspiration = Math.Clamp(State.Inspiration + inspiration, 0, 100)
        };
    }

    public IReadOnlyList<HomeCell> Plan(HomeCell avatar, int tick, int seed, bool windowVisible)
    {
        _nextChoice = null;
        _floor ??= HomeRules.Reachable(layout, avatar).OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        _places ??= PlacesToVisit(_floor);
        var available = windowVisible ? _places : _places.Where(place => place.Want != "lookOutside").ToArray();
        if (_resume)
        {
            _resume = false;
            if (State.Destination is { } saved && _floor.Contains(saved))
            {
                var floorRest = State.Want == "rest" && State.TargetAlias is null && !available.Any(place => place.Want == "rest");
                if (State.Want == "wander" || floorRest || available.Contains(new(State.Want, State.TargetAlias, saved)))
                    return HomeRules.FindPath(layout, avatar, [saved])!;
            }
        }

        var want = ChooseWant(available);
        var places = available.Where(place => place.Want == want).ToArray();
        IReadOnlyList<HomeCell> path;
        string? alias = null;
        if (places.Length != 0)
        {
            path = HomeRules.FindPath(layout, avatar, places.Select(place => place.Cell))!;
            alias = places.First(place => place.Cell == path[^1]).Alias;
        }
        else if (want == "wander")
        {
            var nearby = _floor.Where(cell => HomeRules.DistanceSquared(avatar, cell) is >= 4 and <= 25).ToArray();
            if (nearby.Length == 0) nearby = _floor.Where(cell => cell != avatar).ToArray();
            var index = (uint)unchecked(seed + 37 * tick + 11 * avatar.X + 17 * avatar.Y);
            var destination = nearby.Length == 0 ? avatar : nearby[index % nearby.Length];
            path = HomeRules.FindPath(layout, avatar, [destination]) ?? [avatar];
        }
        else
        {
            // The resident can always take a breather, even before the first piece of furniture is placed.
            path = [avatar];
        }
        State = State with { Want = want, TargetAlias = alias, Destination = path[^1], IsAutonomous = true };
        return path;
    }

    private string ChooseWant(IReadOnlyList<Place> places)
    {
        if (State.Energy <= 32) return "rest";
        if (State.Inspiration <= 35)
        {
            foreach (var want in new[] { "read", "visitPlant", "lookOutside" })
                if (places.Any(place => place.Want == want)) return want;
            return "wander";
        }
        var previous = Array.IndexOf(Wishes, State.Want);
        for (var offset = 1; offset <= Wishes.Length; offset++)
        {
            var want = Wishes[(previous + offset) % Wishes.Length];
            if (want is "wander" or "rest" || places.Any(place => place.Want == want)) return want;
        }
        return "wander";
    }

    private List<Place> PlacesToVisit(IReadOnlyList<HomeCell> floor)
    {
        var result = new List<Place>();
        var walls = HomeRules.ClosedEdges(layout.Grid);
        var window = HomeCatalog.WindowEdge;
        var windowCell = new HomeCell(window.X, window.Y);
        if (walls.Contains(window) && floor.Contains(windowCell))
            result.Add(new("lookOutside", null, windowCell));

        foreach (var item in layout.Items.Where(item => item.Placement is not null).OrderBy(item => item.Alias, StringComparer.Ordinal))
        {
            var kind = HomeCatalog.Definition(item.DefinitionId)?.Kind;
            if (kind is "reading" or "relaxing")
                result.AddRange(HomeRules.AccessibleInteractions(layout, item).Where(floor.Contains)
                    .Select(cell => new Place(kind == "reading" ? "read" : "rest", item.Alias, cell)));
            if (item.DefinitionId == HomeCatalog.PlanterId)
            {
                var footprint = HomeRules.Footprint(item);
                result.AddRange(floor.Where(cell => footprint.Any(pot => HomeRules.DistanceSquared(cell, pot) == 1 &&
                    !walls.Contains(HomeRules.EdgeBetween(cell, pot)))).Select(cell => new Place("visitPlant", item.Alias, cell)));
            }
        }
        return result;
    }
}
