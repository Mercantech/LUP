using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Games;
using Category = Mercantec.Tech.Contracts.GlobalSearchCategories;

namespace Mercantec.Tech.Web.Public;

public static class PublicGlobalSearch
{
    public static IEnumerable<GlobalSearchHit> Search(string? query, string category = Category.All)
    {
        var term = string.Join(' ', SearchWords(query));
        if (term.Length == 0) return [];

        return PublicCatalog.Search(term).Select(hit => new GlobalSearchHit(CategoryFor(hit.Kind), hit.Kind, hit.Title,
                hit.Href, "Oversigt", hit.Description))
            .Concat(GameHits().Concat(PublicPages()).Where(hit => Matches(hit, term)))
            .Where(hit => category == Category.All || hit.Category == category);
    }

    public static IEnumerable<GlobalSearchHit> MemberPages()
    {
        yield return new(Category.Pages, "Side", "Min profil", "/profile", "Konto og indstillinger", "Skift profilnavn og tema.");
        yield return new(Category.Pages, "Side", "Mit univers", "/profile#mit-univers", "Min profil");
        yield return new(Category.Pages, "Side", "Mine hold", "/courses", "Undervisning");
        yield return new(Category.Pages, "Side", "Mine projekter", "/projects/mine", "Mit univers");
        yield return new(Category.Pages, "Side", "Min avatarsamling", "/profile/character", "Mit univers");
        yield return new(Category.Pages, "Side", "Min lejlighed", "/apartment", "Mit univers", "Indret din lejlighed med møbler.");
        yield return new(Category.Pages, "Side", "Butik", "/universe/shop", "Mit univers", "Find møbler og avatartøj.");
    }

    private static IEnumerable<GlobalSearchHit> PublicPages()
    {
        yield return new(Category.Pages, "Side", "Oversigt", "/library", "LUP");
        yield return new(Category.Pages, "Side", "Hjælp", "/help", "LUP", "Sådan hænger LUP sammen.");
        yield return new(Category.Pages, "Side", "Om LUP", "/sources", "LUP", "Lokal UndervisningsPlatform.");
        yield return new(Category.Pages, "Side", "Privatliv", "/privacy", "LUP");
    }

    public static string[] SearchWords(string? query) => (query ?? "").Split((char[]?)null,
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool Matches(GlobalSearchHit hit, string query) => SearchWords(query).All(word =>
        (hit.Title + " " + hit.Kind + " " + hit.Context + " " + hit.Description).Contains(word, StringComparison.CurrentCultureIgnoreCase));

    public static IOrderedEnumerable<GlobalSearchHit> Rank(IEnumerable<GlobalSearchHit> hits, string query)
    {
        var term = string.Join(' ', SearchWords(query));
        return hits.OrderBy(hit => hit.Title.Equals(term, StringComparison.CurrentCultureIgnoreCase) ? 0
                : hit.Title.StartsWith(term, StringComparison.CurrentCultureIgnoreCase) ? 1
                : hit.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase) ? 2 : 3)
            .ThenBy(hit => hit.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(hit => hit.Href, StringComparer.Ordinal);
    }

    private static string CategoryFor(string kind) => kind switch
    {
        "Forløb" or "Fag" or "Emne" => Category.Courses,
        "Badgekrav" => Category.Badges,
        "Karakter" => Category.Characters,
        _ => Category.Projects
    };

    private static IEnumerable<GlobalSearchHit> GameHits()
    {
        yield return new(Category.Games, "Spiloversigt", "Spil", GamesCatalog.Path, "LUP", "Arena, daglige spil og klassiske spil.");
        yield return new(Category.Games, "Spil", "LUP Arena", "/games/arena", "LUP", "Træn mod computeren eller spil online med venner.");
        yield return new(Category.Games, "Vejledning", "Arduino Oplà-guide", GamesCatalog.GuidePath, "Spil");
        foreach (var game in DailyGameProgressCatalog.Games)
            yield return new(Category.Games, "Dagligt spil", game.Title, $"{GamesCatalog.Path}/{game.Id}", "LUP", game.ScoreDescription);
        foreach (var game in GamesCatalog.Classics)
            yield return new(Category.Games, "Klassisk spil", game.Name, game.Path, "Spil", game.Description);
    }
}
