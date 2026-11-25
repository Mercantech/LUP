using System.Security.Cryptography;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.SocialGames;

public sealed class WerewolfException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed record WerewolfSeat(Guid Id, string Name, string Role, bool Alive = true);

// This is the private, persisted state. Only the player's projection leaves the API.
public sealed partial class WerewolfGame
{
    public const int MinPlayers = 5;
    public const int MaxPlayers = 40;

    public WerewolfSeat[] Seats { get; set; } = [];
    public string[] Spares { get; set; } = [];
    public Guid[] Lovers { get; set; } = [];
    public Dictionary<Guid, string[]> Secrets { get; set; } = [];
    public Dictionary<Guid, Guid> Votes { get; set; } = [];
    public bool Timed { get; set; }
    public int Round { get; set; } = 1;
    public int PhaseId { get; set; }
    public string Step { get; set; } = "thief";
    public DateTimeOffset? DeadlineUtc { get; set; }
    public string PublicMessage { get; set; } = "Den første nat er begyndt.";
    public string? Result { get; set; }
    public Guid? ProtectedLastNight { get; set; }
    public Guid? ProtectedTonight { get; set; }
    public Guid? WolfTarget { get; set; }
    public Guid? PoisonTarget { get; set; }
    public bool HasHealing { get; set; } = true;
    public bool HasPoison { get; set; } = true;
    public bool WolvesHaveMet { get; set; }
    public Guid? PendingHunter { get; set; }
    public bool ReturnToNight { get; set; }

    public static string? StartProblem(int playerCount, SocialGameSettings settings)
    {
        if (playerCount < MinPlayers || playerCount > MaxPlayers) return $"Werewolf kræver {MinPlayers}–{MaxPlayers} spillere.";
        if (settings.SpecialRoles is null || settings.SpecialRoles.Any(role => !SocialRoles.Specials.Contains(role))
            || settings.SpecialRoles.Distinct().Count() != settings.SpecialRoles.Length)
            return "Vælg hvert specialkort højst én gang.";
        var wolves = settings.Wolves ?? SocialRoles.SuggestedWolves(playerCount);
        if (wolves < 1 || wolves >= playerCount) return "Der skal være mindst én varulv og én spiller på landsbyens side.";
        if (wolves + settings.SpecialRoles.Length > playerCount) return "Der er flere valgte kort end spillere.";
        return null;
    }

    public static WerewolfGame Start(IReadOnlyList<SocialPlayer> players, SocialGameSettings settings, DateTimeOffset now)
    {
        var error = StartProblem(players.Count, settings);
        if (error is not null) throw new WerewolfException(400, error);
        if (players.Select(p => p.Id).Distinct().Count() != players.Count || players.Any(p => p.Id == Guid.Empty))
            throw new WerewolfException(400, "Hver spiller skal have sin egen plads.");

        var wolves = settings.Wolves ?? SocialRoles.SuggestedWolves(players.Count);
        var hasThief = settings.SpecialRoles.Contains("thief");
        string[] deck = [..Enumerable.Repeat("wolf", wolves), ..settings.SpecialRoles,
            ..Enumerable.Repeat("villager", players.Count - wolves - settings.SpecialRoles.Length + (hasThief ? 2 : 0))];
        // Even small custom lobbies must start with someone who can play for each side.
        // Keep the original spare-card rule, including a thief forced to take one of two wolves.
        do { RandomNumberGenerator.Shuffle(deck.AsSpan()); }
        while (hasThief && !deck.Take(players.Count).Contains("wolf")
            && !(deck.Take(players.Count).Contains("thief") && deck.Skip(players.Count).All(r => r == "wolf")));

        var game = new WerewolfGame
        {
            Timed = settings.Timed,
            Seats = players.Select((p, i) => new WerewolfSeat(p.Id, p.Name, deck[i])).ToArray(),
            Spares = deck.Skip(players.Count).ToArray()
        };
        game.EnterNightStep("thief", now);
        return game;
    }

    public bool CanChat(Guid playerId) => Step == "day" && Seats.Any(p => p.Id == playerId && p.Alive);

    public void Act(Guid playerId, bool isHost, SocialGameActionRequest action, DateTimeOffset now)
    {
        if (action is null || action.Choices is null || action.Action is null)
            throw new WerewolfException(400, "Vælg en handling.");
        if (action.PhaseId != PhaseId || DeadlineUtc <= now)
            throw new WerewolfException(409, "Spillet er gået videre. Hent den aktuelle fase.");
        var player = Seats.FirstOrDefault(p => p.Id == playerId)
            ?? throw new WerewolfException(403, "Du har ingen plads i spillet.");
        var available = Actions(player, isHost).FirstOrDefault(a => a.Key == action.Action)
            ?? throw new WerewolfException(409, "Denne handling er ikke tilgængelig nu.");
        if (action.Choices.Length < available.MinChoices || action.Choices.Length > available.MaxChoices
            || action.Choices.Distinct(StringComparer.Ordinal).Count() != action.Choices.Length
            || action.Choices.Any(v => !available.Options.Any(o => o.Value == v)))
            throw new WerewolfException(400, "Vælg blandt de viste muligheder.");
        Apply(player, action.Action, action.Choices, now);
    }

    private void Apply(WerewolfSeat player, string action, string[] choices, DateTimeOffset now)
    {
        if (Step == "day") { Transition("vote", now); return; }
        if (Step == "vote" || Step == "wolves")
        {
            Votes.Add(player.Id, Guid.Parse(choices[0]));
            var voters = Step == "vote" ? Seats.Count(s => s.Alive) : Seats.Count(s => s.Alive && s.Role == "wolf");
            if (Votes.Count == voters)
            {
                if (Step == "vote") ResolveVote(now);
                else ResolveWolves(now);
            }
            return;
        }
        if (Step == "hunter")
        {
            PendingHunter = null;
            Eliminate(Guid.Parse(choices[0]), "hunter");
            FinishEliminations(now);
            return;
        }
        ApplyNight(player, action, choices, now);
    }

    private void ResolveVote(DateTimeOffset now)
    {
        var ranks = Votes.Values.GroupBy(id => id).OrderByDescending(g => g.Count()).ToArray();
        ReturnToNight = true;
        if (ranks.Length > 1 && ranks[0].Count() == ranks[1].Count())
            PublicMessage = "Stemmerne stod lige. Ingen blev stemt ud.";
        else
        {
            PublicMessage = "Landsbyen har stemt.";
            Eliminate(ranks[0].Key, "vote");
        }
        FinishEliminations(now);
    }

    private void Transition(string next, DateTimeOffset now)
    {
        Step = next;
        if (next == "wolves") WolvesHaveMet = true;
        // Opaque to clients. A sequential phase number would reveal skipped secret roles.
        int previous = PhaseId;
        do { PhaseId = RandomNumberGenerator.GetInt32(1, int.MaxValue); } while (previous == PhaseId);
        DeadlineUtc = Timed && next is not ("day" or "vote" or "hunter" or "finished") ? now.AddSeconds(60) : null;
        Votes.Clear();
    }

    private WerewolfSeat? ActiveRole(string role) => Seats.FirstOrDefault(p => p.Alive && p.Role == role);

    private void Tell(Guid id, string message)
    {
        Secrets.TryGetValue(id, out var previous);
        Secrets[id] = [..previous ?? [], message];
    }

    private void Eliminate(Guid id, string cause)
    {
        var index = Array.FindIndex(Seats, p => p.Id == id && p.Alive);
        if (index < 0) return;
        var player = Seats[index];
        Seats[index] = player with { Alive = false };
        PublicMessage += $" {player.Name} ({SocialRoles.Names[player.Role]}) er ude.";
        if (player.Role == "hunter" && cause is "wolf" or "vote" or "love") PendingHunter = id;
        if (Lovers.Contains(id)) Eliminate(Lovers.Single(partner => partner != id), "love");
    }

    private void FinishEliminations(DateTimeOffset now)
    {
        var alive = Seats.Where(p => p.Alive).ToArray();
        if (PendingHunter.HasValue && alive.Length > 0) { Transition("hunter", now); return; }
        PendingHunter = null;
        var wolves = alive.Count(p => p.Role == "wolf");
        if (alive.Length == 0) Result = "Ingen overlevede. Partiet har ingen vinder.";
        else if (alive.Length == 2 && wolves == 1 && alive.All(p => Lovers.Contains(p.Id)))
            Result = $"De forelskede, {alive[0].Name} og {alive[1].Name}, vinder sammen!";
        else if (wolves == 0) Result = "Landsbyen vinder!";
        else if (wolves == alive.Length) Result = "Varulvene vinder!";
        if (Result is not null) { Transition("finished", now); return; }
        if (!ReturnToNight) { Transition("day", now); return; }
        Round++;
        ProtectedLastNight = ProtectedTonight;
        ProtectedTonight = WolfTarget = PoisonTarget = null;
        EnterNightStep("guard", now);
    }
}
