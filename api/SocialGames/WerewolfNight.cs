using System.Security.Cryptography;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.SocialGames;

public sealed partial class WerewolfGame
{
    private static readonly string[] NightSteps = ["thief", "cupid", "guard", "seer", "wolves", "heal", "poison"];

    private void EnterNightStep(string next, DateTimeOffset now)
    {
        for (var i = Array.IndexOf(NightSteps, next); i < NightSteps.Length; i++)
        {
            var step = NightSteps[i];
            bool needed = step switch
            {
                "thief" or "cupid" => Round == 1 && ActiveRole(step) is not null,
                "guard" or "seer" => ActiveRole(step) is not null,
                "wolves" => ActiveRole("wolf") is not null && Seats.Any(p => p.Alive && p.Role != "wolf"),
                "heal" => HasHealing && WolfTarget.HasValue && ActiveRole("witch") is not null,
                "poison" => HasPoison && ActiveRole("witch") is not null,
                _ => false
            };
            if (!needed) continue;
            Transition(step, now);
            return;
        }
        ResolveNight(now);
    }

    private void NextNightStep(DateTimeOffset now)
    {
        var next = Array.IndexOf(NightSteps, Step) + 1;
        if (next == NightSteps.Length) ResolveNight(now);
        else EnterNightStep(NightSteps[next], now);
    }

    private void ApplyNight(WerewolfSeat player, string action, string[] choices, DateTimeOffset now)
    {
        if (action != "skip")
        {
            switch (Step)
            {
                case "thief":
                    var card = int.Parse(choices[0], System.Globalization.CultureInfo.InvariantCulture);
                    var index = Array.FindIndex(Seats, p => p.Id == player.Id);
                    Seats[index] = player with { Role = Spares[card] };
                    Spares[card] = "thief";
                    Tell(player.Id, $"Du overtog rollen {SocialRoles.Names[Seats[index].Role]}.");
                    break;
                case "cupid":
                    Lovers = choices.Select(Guid.Parse).ToArray();
                    foreach (var id in Lovers)
                    {
                        var partner = Seats.Single(p => Lovers.Contains(p.Id) && p.Id != id);
                        Tell(id, $"Du er forelsket i {partner.Name}.");
                    }
                    break;
                case "guard": ProtectedTonight = Guid.Parse(choices[0]); break;
                case "seer":
                    var seen = Seats.Single(p => p.Id == Guid.Parse(choices[0]));
                    Tell(player.Id, $"Nat {Round}: {seen.Name} er {SocialRoles.Names[seen.Role]}.");
                    break;
                case "heal":
                    HasHealing = false;
                    WolfTarget = null;
                    break;
                case "poison":
                    HasPoison = false;
                    PoisonTarget = Guid.Parse(choices[0]);
                    break;
            }
        }
        NextNightStep(now);
    }

    private void ResolveWolves(DateTimeOffset now)
    {
        var choices = Votes.Values.Distinct().ToArray();
        WolfTarget = choices.Length == 1 && choices[0] != ProtectedTonight ? choices[0] : null;
        EnterNightStep("heal", now);
    }

    private void ResolveNight(DateTimeOffset now)
    {
        ReturnToNight = false;
        PublicMessage = WolfTarget.HasValue || PoisonTarget.HasValue
            ? $"Det er morgen efter nat {Round}."
            : $"Det er morgen efter nat {Round}. Alle overlevede natten.";
        if (WolfTarget.HasValue) Eliminate(WolfTarget.Value, "wolf");
        if (PoisonTarget.HasValue) Eliminate(PoisonTarget.Value, "poison");
        FinishEliminations(now);
    }

    // Use each expired deadline as the start of the following step, including after a service restart.
    public bool AdvanceExpired(DateTimeOffset now)
    {
        bool changed = false;
        while (DeadlineUtc is { } expired && expired <= now)
        {
            changed = true;
            if (Step == "wolves")
            {
                var targets = Seats.Where(s => s.Alive && s.Role != "wolf").ToArray();
                foreach (var wolf in Seats.Where(s => s.Alive && s.Role == "wolf" && !Votes.ContainsKey(s.Id)))
                    Votes[wolf.Id] = targets[RandomNumberGenerator.GetInt32(targets.Length)].Id;
                ResolveWolves(expired);
                continue;
            }

            var actor = Seats.Single(p => Actions(p, false).Length > 0);
            var options = Actions(actor, false);
            var optional = options.FirstOrDefault(a => a.Key == "skip");
            if (optional is not null) { Apply(actor, "skip", [], expired); continue; }
            var mandatory = options[0];
            var pool = mandatory.Options.ToArray();
            RandomNumberGenerator.Shuffle(pool.AsSpan());
            var values = pool.Take(mandatory.MinChoices).Select(o => o.Value).ToArray();
            Apply(actor, mandatory.Key, values, expired);
        }
        return changed;
    }
}
