using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Electives;

// The template IDs are the original Notion page IDs used by the catalogue import.
public sealed record ElectivePackage(Guid Id, string Title, string ImageUrl, string Summary)
{
    public static IReadOnlyList<ElectivePackage> Catalogue { get; } =
    [
        new(new("28bdab5c-a237-80a6-a76f-e538cef2e5d3"), "Deploy or Die",
            "/images/valgfag/deploy-or-die.webp",
            "Få din kode ud på nettet. Arbejd med DevNet, automatisering og sikkerhed, når en applikation skal i drift."),
        new(new("b219ccf5-5b8a-48a3-8052-7c06216eadc4"), "Game Design",
            "/images/valgfag/game-design.webp",
            "Byg spilbare prototyper i en game engine. Giv spilleren noget at udforske med baner, figurer og dine egne spilleregler."),
        new(new("9a0e18a7-983b-499d-8e17-3a797ce0624c"), "Machine Learning",
            "/images/valgfag/machine-learning.webp",
            "Træn en model i Python. Gå fra rå data til forudsigelser, og test, hvor godt modellen rammer på nye eksempler.")
    ];

    public static ElectivePackage? ForElective(ElectiveView elective)
    {
        var packageId = elective.TemplateId ?? elective.Id;
        return Catalogue.FirstOrDefault(package => package.Id == packageId);
    }

    public string Href => $"/valgfag?vis=katalog&id={Id:D}";
}
