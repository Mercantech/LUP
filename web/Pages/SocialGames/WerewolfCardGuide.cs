namespace Mercantec.Tech.Web.Pages.SocialGames;

internal sealed record GuideText(string Danish, string English)
{
    public string In(SocialGameLanguage language) => language.T(Danish, English);
}

internal sealed record RoleGuide(GuideText Goal, GuideText Timing, GuideText Rules, GuideText Example, int? RulebookPage = null);

internal static partial class WerewolfCardGuide
{
    public const string RulebookUrl = "https://image.intertoys.nl/wcsstore/IntertoysCAS/images/catalog/manuals/1995320-0a030ce7.pdf";

    private static readonly Dictionary<string, RoleGuide> Cards = Create();

    public static RoleGuide For(string key) => Cards[key];

    private static Dictionary<string, RoleGuide> Create()
    {
        var cards = new Dictionary<string, RoleGuide>(StringComparer.Ordinal);
        AddPlayable(cards);
        AddVillage(cards);
        AddExpansion(cards);
        return cards;
    }
}
