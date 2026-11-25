using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.SocialGames;

public sealed partial class WerewolfGame
{
    public WerewolfView Project(Guid playerId, bool isHost)
    {
        var player = Seats.FirstOrDefault(s => s.Id == playerId)
            ?? throw new WerewolfException(403, "Du har ingen plads i spillet.");
        var actions = Actions(player, isHost);
        var publicPhase = Step is "day" or "vote" or "hunter" or "finished" ? Step : "night";
        var personal = new List<string>(Secrets.GetValueOrDefault(playerId) ?? []);
        if (player.Role == "wolf" && WolvesHaveMet)
        {
            var otherWolves = Seats.Where(s => s.Role == "wolf" && s.Id != player.Id).Select(s => s.Name).ToArray();
            personal.Add(otherWolves.Length == 0 ? "Du er den eneste varulv." : $"Dine medulve: {string.Join(", ", otherWolves)}.");
            if (Step == "wolves" && player.Alive)
            {
                foreach (var (voter, target) in Votes.Where(v => v.Key != playerId))
                    personal.Add($"{Seats.Single(s => s.Id == voter).Name} foreslår {Seats.Single(s => s.Id == target).Name}.");
            }
        }
        if (player.Role == "witch" && player.Alive)
        {
            personal.Add($"Helbredelsesdrik: {(HasHealing ? "tilbage" : "brugt")}. Gift: {(HasPoison ? "tilbage" : "brugt")}.");
            if (Step == "heal" && WolfTarget.HasValue)
                personal.Add($"Ulvene har valgt {Seats.Single(s => s.Id == WolfTarget).Name}.");
        }
        if (Votes.TryGetValue(playerId, out var choice))
            personal.Add($"Dit valg er registreret: {Seats.Single(s => s.Id == choice).Name}.");

        var message = PublicMessage;
        if (Step == "hunter" && PendingHunter.HasValue) message += " Jægeren vælger sit sidste mål.";
        if (Step == "finished")
            message += " Rollekort: " + string.Join(", ", Seats.Select(s => $"{s.Name} ({SocialRoles.Names[s.Role]})")) + ".";
        // Observers receive the same night and no action clock throughout the secret sequence.
        return new(actions.Length > 0 ? PhaseId : 0, publicPhase, Round, player.Role,
            Seats.Where(s => s.Alive).Select(s => s.Id).ToArray(), message, personal.ToArray(),
            actions.Length > 0 ? DeadlineUtc : null, actions, Step == "finished", Result);
    }

    private WerewolfAction[] Actions(WerewolfSeat player, bool isHost)
    {
        if (Step == "day")
            return isHost ? [new("open-vote", "Åbn afstemningen", 0, 0, [])] : [];
        if (Step == "hunter")
            return PendingHunter == player.Id ? [Select("shoot", "Vælg jægerens sidste mål", Seats.Where(p => p.Alive))] : [];
        if (!player.Alive) return [];

        var alive = Seats.Where(p => p.Alive);
        switch (Step)
        {
            case "thief" when player.Role == "thief":
                var cards = Spares.Select((r, i) => new WerewolfOption(i.ToString(System.Globalization.CultureInfo.InvariantCulture), SocialRoles.Names[r])).ToArray();
                var steal = new WerewolfAction("steal", "Overtag et af de to kort", 1, 1, cards);
                return Spares.All(r => r == "wolf") ? [steal] : [steal, Skip("Behold tyvens kort")];
            case "cupid" when player.Role == "cupid":
                return [Select("love", "Vælg de to forelskede", alive, 2)];
            case "guard" when player.Role == "guard":
                return [Select("protect", "Beskyt en spiller i nat", alive.Where(p => p.Id != ProtectedLastNight))];
            case "seer" when player.Role == "seer":
                return [Select("inspect", "Se en anden spillers rolle", alive.Where(p => p.Id != player.Id))];
            case "wolves" when player.Role == "wolf" && !Votes.ContainsKey(player.Id):
                return [Select("hunt", "Vælg ulvenes offer", alive.Where(p => p.Role != "wolf"))];
            case "heal" when player.Role == "witch":
                return [new("heal", "Brug helbredelsesdrikken", 0, 0, []), Skip("Gem helbredelsesdrikken")];
            case "poison" when player.Role == "witch":
                return [Select("poison", "Giv gift til en spiller", alive), Skip("Gem giften")];
            case "vote" when !Votes.ContainsKey(player.Id):
                return [Select("vote", "Stem på en spiller", alive.Where(p => p.Id != player.Id
                    && !(Lovers.Contains(player.Id) && Lovers.Contains(p.Id))))];
            default:
                return [];
        }
    }

    private static WerewolfAction Select(string action, string label, IEnumerable<WerewolfSeat> players, int count = 1)
        => new(action, label, count, count, players.Select(p => new WerewolfOption(p.Id.ToString(), p.Name)).ToArray());

    private static WerewolfAction Skip(string label) => new("skip", label, 0, 0, []);
}
