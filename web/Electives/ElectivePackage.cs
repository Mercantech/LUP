using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Electives;

// The template IDs are the original Notion page IDs used by the catalogue import.
public sealed record ElectivePackage(Guid Id, string Title, string ImageUrl, string Summary,
    string Subjects, IReadOnlyList<string> Outcomes)
{
    public static IReadOnlyList<ElectivePackage> Catalogue { get; } =
    [
        new(new("28bdab5c-a237-80a6-a76f-e538cef2e5d3"), "Deploy or Die",
            "/images/valgfag/deploy-or-die.webp",
            "Få din app fra din egen maskine og ud på nettet. Lær at automatisere arbejdet og tænke sikkerhed ind fra start.",
            "DevNet og Informationssikkerhed og awareness",
            [
                "Automatisér opgaver med Python, Linux og API-kald.",
                "Deploy en løsning, og beskyt data i et cloudmiljø.",
                "Vurdér risikoen i konkrete situationer som phishing og usikre logins."
            ]),
        new(new("b219ccf5-5b8a-48a3-8052-7c06216eadc4"), "Game Design",
            "/images/valgfag/game-design.webp",
            "Gør din spilidé spilbar. Byg baner, få styr på spillerens bevægelser, og find ud af, hvad der virker, når andre prøver spillet.",
            "Game-design I og II",
            [
                "Byg en 2D-prototype, og brug spiltests til at forbedre den.",
                "Skab flere baner, skift mellem scener, og gem en highscore.",
                "Planlæg et simpelt 3D-multiplayerspil, og programmér figurer med simpel AI."
            ]),
        new(new("9a0e18a7-983b-499d-8e17-3a797ce0624c"), "Machine Learning",
            "/images/valgfag/machine-learning.webp",
            "Kan en model genkende en kategori eller forudsige et tal? Byg og test din egen i Python på et konkret datasæt.",
            "Machine Learning",
            [
                "Gør rå data klar til modellens træning.",
                "Brug klassifikation til at forudsige kategorier og regression til at estimere tal.",
                "Test modellen på nye data, og vurder, hvor godt den virker."
            ])
    ];

    public static ElectivePackage? ForElective(ElectiveView elective)
    {
        var packageId = elective.TemplateId ?? elective.Id;
        return Catalogue.FirstOrDefault(package => package.Id == packageId);
    }

    public string Href => $"/valgfag?vis=katalog&id={Id:D}";
}
