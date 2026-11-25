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
        var privateEnglish = personal.Select(WerewolfMessages.PrivateEnglish).ToList();
        void AddPrivate(string da, string? en = null)
        {
            personal.Add(da);
            privateEnglish.Add(en ?? WerewolfMessages.PrivateEnglish(da));
        }
        if (player.Role == "wolf" && WolvesHaveMet)
        {
            var otherWolves = Seats.Where(s => s.Role == "wolf" && s.Id != player.Id).Select(s => s.Name).ToArray();
            AddPrivate(otherWolves.Length == 0 ? "Du er den eneste varulv." : $"Dine medulve: {string.Join(", ", otherWolves)}.");
            if (Step == "wolves" && player.Alive)
            {
                foreach (var (voter, target) in Votes.Where(v => v.Key != playerId))
                {
                    var from = Seats.Single(s => s.Id == voter).Name;
                    var to = Seats.Single(s => s.Id == target).Name;
                    AddPrivate($"{from} foreslår {to}.", $"{from} suggests {to}.");
                }
            }
        }
        if (player.Role == "witch" && player.Alive)
        {
            AddPrivate($"Helbredelsesdrik: {(HasHealing ? "tilbage" : "brugt")}. Gift: {(HasPoison ? "tilbage" : "brugt")}.");
            if (Step == "heal" && WolfTarget.HasValue)
                AddPrivate($"Ulvene har valgt {Seats.Single(s => s.Id == WolfTarget).Name}.");
        }
        if (Votes.TryGetValue(playerId, out var choice))
            AddPrivate($"Dit valg er registreret: {Seats.Single(s => s.Id == choice).Name}.");

        var message = PublicMessage;
        var english = EnglishAnnouncement();
        if (Step == "hunter" && PendingHunter.HasValue)
        {
            message += " Jægeren vælger sit sidste mål.";
            english += " The hunter is choosing a final target.";
        }
        if (Step == "finished")
        {
            message += " Rollekort: " + string.Join(", ", Seats.Select(s => $"{s.Name} ({SocialRoles.Names[s.Role]})")) + ".";
            english += " Role cards: " + string.Join(", ", Seats.Select(s => $"{s.Name} ({WerewolfMessages.RoleEnglish(s.Role)})")) + ".";
        }
        var resultEnglish = WerewolfMessages.ResultEnglish(Result);
        if (Result?.StartsWith("De forelskede, ", StringComparison.Ordinal) == true)
        {
            var pair = Seats.Where(s => s.Alive && Lovers.Contains(s.Id)).ToArray();
            if (pair.Length == 2) resultEnglish = $"The lovers, {pair[0].Name} and {pair[1].Name}, win together!";
        }
        // Observers receive the same night and no action clock throughout the secret sequence.
        return new(actions.Length > 0 ? PhaseId : 0, publicPhase, Round, player.Role,
            Seats.Where(s => s.Alive).Select(s => s.Id).ToArray(), message, personal.ToArray(),
            actions.Length > 0 ? DeadlineUtc : null, actions, Step == "finished", Result,
            new(english, privateEnglish.ToArray(), resultEnglish));
    }

    private string EnglishAnnouncement()
    {
        var (opening, rest) = WerewolfMessages.PublicOpening(PublicMessage);
        if (rest.Length == 0) return opening;
        var deaths = AnnouncedDeaths?.Select(id => Seats.Single(s => s.Id == id)).ToArray()
            ?? ReadLegacyDeaths(rest);
        if (deaths is null)
            return opening + " Players out so far: " + string.Join(", ", Seats.Where(s => !s.Alive).Select(s => s.Name)) + ".";
        return opening + string.Concat(deaths.Select(s => $" {s.Name} ({WerewolfMessages.RoleEnglish(s.Role)}) is out."));
    }

    private WerewolfSeat[]? ReadLegacyDeaths(string rest)
    {
        // A nickname can contain punctuation or a role name. Match complete, real player/role pairs,
        // never replace words inside arbitrary text. An ambiguous old report can still show who is out.
        var candidates = Seats.Where(s => !s.Alive)
            .Select(s => (Seat: s, Sentence: $" {s.Name} ({SocialRoles.Names[s.Role]}) er ude.")).ToArray();
        var deaths = new List<WerewolfSeat>();
        var index = 0;
        while (index < rest.Length)
        {
            WerewolfSeat? match = null;
            var length = 0;
            foreach (var (seat, sentence) in candidates)
            {
                if (!rest.AsSpan(index).StartsWith(sentence, StringComparison.Ordinal)) continue;
                if (match is not null) return null;
                match = seat;
                length = sentence.Length;
            }
            if (match is null || deaths.Contains(match)) return null;
            deaths.Add(match);
            index += length;
        }
        return deaths.ToArray();
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
