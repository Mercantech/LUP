using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Pages.SocialGames;

public sealed record SocialGameLanguage(bool IsEnglish = false)
{
    public string T(string da, string en) => IsEnglish ? en : da;

    public string Role(string key) => IsEnglish
        ? WerewolfMessages.RoleEnglish(key)
        : SocialRoles.Names.GetValueOrDefault(key, key);

    public string Rule(string key) => key switch
    {
        "wolf" => T("Vælg et offer sammen med de andre ulve om natten.", "Choose a victim with the other werewolves each night."),
        "villager" => T("Brug dagens diskussion og din stemme til at finde ulvene.", "Use the discussion and your vote to find the werewolves."),
        "thief" => T("Vælg mellem to overskydende kort den første nat.", "Choose between two spare cards on the first night."),
        "seer" => T("Se en anden spillers rolle hver nat.", "Learn another player’s role each night."),
        "cupid" => T("Vælg to forelskede, der deler skæbne.", "Choose two lovers who will share the same fate."),
        "hunter" => T("Tag en anden spiller med dig med dit sidste skud.", "Use your final shot to take another player with you."),
        "witch" => T("Du har én helbredelsesdrik og én gift til hele spillet.", "You have one healing potion and one poison potion for the whole game."),
        "guard" => T("Beskyt en spiller mod ulvene. Skift mål hver nat.", "Protect one player from the werewolves. Choose a different player each night."),
        _ => ""
    };

    public string Phase(string key) => key switch
    {
        "night" => T("Nat", "Night"),
        "day" => T("Dag", "Day"),
        "vote" => T("Afstemning", "Voting"),
        "hunter" => T("Jægerens sidste skud", "The Hunter’s final shot"),
        "finished" => T("Spillet er slut", "Game over"),
        _ => T("Spillet", "Game")
    };

    public string Server(string text) => IsEnglish ? SocialGameMessages.English(text) : text;
    public string Error(Exception ex) => Server(SocialText.Error(ex));
}
