namespace Mercantec.Tech.Web.Pages.SocialGames;

internal static class SocialText
{
    public static bool IsRequestError(Exception ex) => ex is InvalidOperationException or HttpRequestException or OperationCanceledException;
    public static string Error(Exception ex) => ex is InvalidOperationException ? ex.Message : "Forbindelsen til spillet blev afbrudt. Prøv igen.";

    public static string RoleImage(string role) =>
        $"/images/social-games/roles/{(role == "wolf" ? "werewolf" : role)}.webp";
}
