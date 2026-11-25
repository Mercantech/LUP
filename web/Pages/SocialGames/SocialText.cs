namespace Mercantec.Tech.Web.Pages.SocialGames;

internal static class SocialText
{
    public static bool IsRequestError(Exception ex) => ex is InvalidOperationException or HttpRequestException or OperationCanceledException;
    public static string Error(Exception ex) => ex is InvalidOperationException ? ex.Message : "Forbindelsen til spillet blev afbrudt. Prøv igen.";

    public static string RoleImage(string role) =>
        $"/images/social-games/roles/{(role == "wolf" ? "werewolf" : role)}.webp";

    public static string RoleRule(string key) => key switch
    {
        "wolf" => "Vælg et offer sammen med de andre ulve om natten.",
        "villager" => "Brug dagens diskussion og din stemme til at finde ulvene.",
        "thief" => "Vælg mellem to overskydende kort den første nat.",
        "seer" => "Se en anden spillers rolle hver nat.",
        "cupid" => "Vælg to forelskede, der deler skæbne.",
        "hunter" => "Tag en anden spiller med dig med dit sidste skud.",
        "witch" => "Du har én helbredelsesdrik og én gift til hele spillet.",
        "guard" => "Beskyt en spiller mod ulvene. Skift mål hver nat.",
        _ => ""
    };

    public static string Phase(string key) => key switch
    {
        "night" => "Nat", "day" => "Dag", "vote" => "Afstemning",
        "hunter" => "Jægerens sidste skud", "finished" => "Spillet er slut", _ => "Spillet"
    };
}
