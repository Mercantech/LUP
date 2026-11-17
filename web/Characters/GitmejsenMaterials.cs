namespace Mercantec.Tech.Web.Characters;

internal static class GitmejsenMaterials
{
    private const string ImageRoot = "/images/characters/gitmejsen";

    public static IReadOnlyList<CharacterAppearance> Appearances { get; } =
    [
        new("Gitmejsen", $"{ImageRoot}/portrait.webp", "Gitmejsen med sort-hvid fjertop og koboltblåt tørklæde"),
        new("Vinter", $"{ImageRoot}/vinter.webp", "Gitmejsen har pustet fjerene op og sidder med sit blå tørklæde på en snepudret grankvist"),
        new("Arkivaren", $"{ImageRoot}/arkivaren.webp", "Gitmejsen med blåt tørklæde og runde messingbriller undersøger en ring på sin siddegren")
    ];

    public static IReadOnlyList<CharacterIllustration> Scenes { get; } =
    [
        Scene("branches-vaerksted", "Værkstedet i trækronen", "Gitmejsen sidder ved en Y-formet gren med træperler og to messingklemmer i sit værksted i trækronen."),
        Scene("kladden-under-barken", "Kladden under barken", "Gitmejsen skubber en sammenrullet kladde ind under løs fyrrebark i aftensolen."),
        Scene("soeen-ved-daggry", "Søen ved daggry", "Gitmejsen flyver over en stille sø med morgensol, tåge og nåletræer langs bredden."),
        Scene("udgivelseshylden", "Udgivelseshylden", "Gitmejsen holder et messingmærke over en pakke papirer i et loftsarkiv med mærkede papirpakker."),
        Scene("broen-efter-regnen", "Broen efter regnen", "Gitmejsen sidder på en regnvåd træbro over et brusende vandløb mellem birk og gran."),
        Scene("badehuset-ved-skumring", "Badehuset ved skumring", "Gitmejsen sidder på en brostolpe foran et træbadehus, mens lampelyset spejler sig i søen."),
        Scene("vinterhaven", "Vinterhaven", "Gitmejsen undersøger små grønne spirer på kanten af en lerpotte i et varmt drivhus med sne uden for ruderne."),
        Scene("kometaften-paa-klippen", "Kometaften på klippen", "Gitmejsen sidder ved en kikkert på en klippe, mens en komet lyser over søens øer i skumringen.")
    ];

    public static IReadOnlyList<CharacterAnimation> Animations { get; } =
    [
        Animation("foerste-commit", "Første commit", "Arbejdsmappe, staging og historik",
            "Vælg en ændring med git add, og gem det valgte indhold med git commit.",
            "README.md føjes til staging med git add. Git commit gemmer det valgte indhold som commit A."),
        Animation("ny-branch", "Opret en branch", "Samme udgangspunkt, eget spor",
            "Opret en branch fra det samme commit. Når du laver et nyt commit på den, flytter dens peger sig frem.",
            "main og forsog peger først på B. Et nyt commit C flytter kun forsog videre."),
        Animation("flet-branches", "Flet branches", "Et merge med to forældre",
            "Når begge branches er gået videre, kan en merge samle dem i et commit med to forældre.",
            "Commit B og C har fælles forælder A. Merge-commit M får B og C som forældre, og main flytter til M.")
    ];

    public static IReadOnlyList<CharacterIllustration> Illustrations { get; } =
    [
        Lesson("foerste-commit", "Dit første commit",
            "Gitmejsen sammenligner en kladde og en færdig fuglehusskitse med et gyldent frø som segl.",
            "Opret et projekt til et fuglehus, og giv dit første commit en besked, der fortæller, hvad du har tilføjet.",
            [
                "Opret en tom mappe, og kør git init -b main i mappen. Opret README.md i din editor. Skriv »# Fuglehus« på første linje og »Åbningen udskæres efter mål.« på næste linje. Gem filen.",
                "Git add vælger den version af filen, der kommer med i næste commit. Se det valgte indhold med git diff --cached. Beskeden »Beskriv fuglehusets åbning« fortæller en kollega, hvad commit’et gør.",
                "Læs bagefter det nye commit med git log, og hent filens indhold fra commit’et med git show."
            ],
            """
            git init -b main
            # Opret og gem README.md som beskrevet ovenfor.
            git add README.md
            git diff --cached
            git commit -m "Beskriv fuglehusets åbning"
            git log --oneline
            git show HEAD:README.md
            """,
            new("Pro Git om at registrere ændringer", "https://git-scm.com/book/en/v2/Git-Basics-Recording-Changes-to-the-Repository", true, false)),
        Lesson("branches-og-merge", "Branches og merge",
            "Den ene skitse viser et blåt tag, den anden en ravfarvet siddepind. Det samlede fuglehus har begge dele.",
            "Arbejd videre på to branches og se deres commits mødes i en merge.",
            [
                "Fortsæt i fuglehusprojektet fra første øvelse. Opret branchen blaat-tag, og skriv en kort beskrivelse af taget i tag.md. Skift tilbage til main, og beskriv siddepinden i siddepind.md.",
                "Når du opretter blaat-tag, peger begge branchnavne først på det samme commit. Hvert nyt commit flytter navnet for den branch, du står på.",
                "Flet blaat-tag ind fra main. Fordi begge branches har fået et nyt commit siden A, får merge-commit’et M to forældre, B og C. Brug loggen til at se grafen, og åbn begge filer for at tjekke resultatet."
            ],
            """
            git switch -c blaat-tag
            # Opret tag.md, og beskriv det blå tag.
            git add tag.md
            git commit -m "Beskriv det blå tag"

            git switch main
            # Opret siddepind.md, og beskriv den ravfarvede siddepind.
            git add siddepind.md
            git commit -m "Beskriv siddepinden"

            git merge blaat-tag -m "Saml tag og siddepind"
            git log --graph --oneline --all

            # Graf læst fra venstre mod højre.
            # A---C---M  main
            #  \     /
            #   B---'    blaat-tag
            """,
            new("Pro Git om branching og merge", "https://git-scm.com/book/en/v2/Git-Branching-Basic-Branching-and-Merging", true, false)),
        Lesson("mergekonflikt", "Løs en mergekonflikt",
            "Gitmejsen sammenligner en rund og en firkantet åbning på hver sin skitse af samme fuglehus.",
            "Læs begge forslag, når to branches ændrer den samme linje i README.md.",
            [
                "Stå på main efter fletningen. Lav branchen rund-aabning, og ændr den anden linje i README.md som vist nedenfor. Gem og commit, og skift tilbage til main. Ret den samme linje til beskrivelsen af den udskiftelige plade, og gem et nyt commit.",
                "Når du fletter, stopper Git og markerer de to versioner af linjen. Læs med git diff og find markørerne <<<<<<<, ======= og >>>>>>>. Den ene kollega vil have en rund åbning uden skarpe hjørner. Den anden vil kunne udskifte fronten.",
                "Erstat hele den markerede blok med sætningen om en rund åbning i en udskiftelig, firkantet plade. Behold resten af filen. Brug git add, kontrollér den valgte løsning med git diff --cached, og afslut fletningen med et commit."
            ],
            """
            git switch -c rund-aabning
            # Skriv 'Åbningen er rund uden skarpe hjørner.' på anden linje.
            git add README.md
            git commit -m "Gør åbningen rund"

            git switch main
            # Skriv 'Åbningen er firkantet i en udskiftelig frontplade.' på anden linje.
            git add README.md
            git commit -m "Gør frontpladen udskiftelig"

            git merge rund-aabning
            git status
            git diff
            # Skriv 'Åbningen er rund og sidder i en udskiftelig, firkantet frontplade.'
            git add README.md
            git diff --cached
            git commit -m "Foren rund åbning og udskiftelig front"
            git status
            """,
            new("Git-manualen om konfliktløsning", "https://git-scm.com/docs/git-merge#_how_to_resolve_conflicts", true, false))
    ];

    private static CharacterIllustration Scene(string file, string title, string alt) =>
        new(title, $"{ImageRoot}/scenes/{file}.png", alt, 1536, 1024)
        {
            Preview = $"{ImageRoot}/scenes/{file}.webp"
        };

    private static CharacterAnimation Animation(string id, string title, string topic, string description, string alt) =>
        new(id, title, topic, description, $"{ImageRoot}/animations/{id}.gif", $"{ImageRoot}/animations/{id}.webp", alt, 640, 480);

    private static CharacterIllustration Lesson(string file, string title, string alt, string summary,
        IReadOnlyList<string> explanation, string code, CharacterLink source) =>
        new(title, $"{ImageRoot}/teaching/{file}.png", alt, 1536, 1024)
        {
            Preview = $"{ImageRoot}/teaching/{file}.webp",
            Summary = summary,
            Explanation = explanation,
            Code = code,
            Sources = [source]
        };
}
