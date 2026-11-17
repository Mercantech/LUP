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
        Scene("kometaften-paa-klippen", "Kometaften på klippen", "Gitmejsen sidder ved en kikkert på en klippe, mens en komet lyser over søens øer i skumringen."),
        Scene("mumihuset-og-grenene", "Mumihuset og grenene", "Gitmejsen ved en Y-formet gren med to messingknapper foran det blå, runde Mumihus med rødt tag."),
        Scene("syltetoejsarkivet", "Syltetøjsarkivet", "Gitmejsen ved et mærket syltetøjsglas på en hylde, hvor Mumimors stribede forklæde og sorte taske hænger ved siden af."),
        Scene("snusmumrikkens-lejr", "Snusmumrikkens lejr", "Gitmejsen gemmer en rullet kladde i en barkrevne ved et blåt telt med Snusmumrikkens grønne hat og mundharmonika."),
        Scene("fyret-paa-oen", "Fyret på øen", "Gitmejsen ser på fyrtegninger med messingmærker ved siden af Mumifars sorte høje hat på en vindblæst klippeø.")
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
            new("Git-manualen om konfliktløsning", "https://git-scm.com/docs/git-merge#_how_to_resolve_conflicts", true, false)),
        Lesson("vaelg-naeste-commit", "Vælg indholdet til næste commit",
            "Gitmejsen har lagt en kopi af den valgte tagtegning i en kurv. To tegninger ligger på bordet, og Mumimors stribede forklæde hænger i baggrunden.",
            "Vælg den ene af to ændringer i samme fil, og kontrollér præcis, hvad næste commit kommer til at indeholde.",
            [
                "Start i en tom øvemappe. Skriv »Taget er rødt.« på første linje i materialer.md, mindst otte linjer med materialer derunder og »Døren er brun.« på sidste linje. Lav et commit med den fil som udgangspunkt.",
                "Ret første linje til »Taget er blåt.« og sidste linje til »Døren er grøn.«. Kør git add -p, og svar y til stykket om taget og n til stykket om døren. Du kan bruge s til at dele et stykke, hvis Git viser begge ændringer sammen.",
                "Se det staged indhold med git diff --cached. Lav dit commit, og kør git diff for at finde dørændringen i arbejdsmappen."
            ],
            """
            git init -b main
            # Opret materialer.md som beskrevet ovenfor.
            git add materialer.md
            git commit -m "Beskriv husets materialer"

            # Redigér første og sidste linje i materialer.md.
            git status --short
            git add -p -- materialer.md
            git diff --cached -- materialer.md
            git commit -m "Skift tagets farve til blå"
            git diff -- materialer.md
            """,
            new("Git-manualen om git add", "https://git-scm.com/docs/git-add", true, false)),
        Lesson("laes-en-diff", "Læs en diff",
            "To tegninger af Mumihuset viser en smal rød trappe og en bredere grøn trappe, markeret med minus og plus.",
            "Læs forskellen mellem det, du har valgt til næste commit, og det, du har redigeret siden.",
            [
                "Start i en tom øvemappe. Commit trappe.md med linjen »Trappen er smal og rød.«. Ret linjen til »Trappen er bred og grøn.«, gem filen, og kør git add. Tilføj derefter linjen »Gelænderet er hvidt.«, og gem igen.",
                "Git diff sammenligner arbejdsmappen med staging og viser den nye linje om gelænderet. Git diff --cached sammenligner staging med sidste commit og viser ændringen fra den røde trappe til den grønne.",
                "Læs linjer med minus som indhold, der fjernes, og linjer med plus som indhold, der tilføjes. Øvelsen efterlader én staged og én ustaged ændring i øvemappen."
            ],
            """
            git init -b main
            # Opret trappe.md med 'Trappen er smal og rød.'.
            git add trappe.md
            git commit -m "Beskriv den røde trappe"
            # Erstat linjen med 'Trappen er bred og grøn.'.
            git add trappe.md
            # Tilføj 'Gelænderet er hvidt.' på en ny linje.
            git diff -- trappe.md
            git diff --cached -- trappe.md
            git status --short
            """,
            new("Git-manualen om git diff", "https://git-scm.com/docs/git-diff", true, false)),
        Lesson("find-en-aendring", "Find en ændring i historikken",
            "Gitmejsen ser på den første tegning med en redningskrans blandt fire tegninger af samme bro. Mumifars høje hat står ved siden af en bog.",
            "Brug filens historik til at finde, hvornår broen fik en redningskrans.",
            [
                "Start i en tom øvemappe. Opret bro.md, og gem fire udgaver af broens journal med kommandoerne nedenfor. Tilføj én linje pr. commit.",
                "Find commitbeskeden »Tilføj en redningskrans« med git log --oneline -- bro.md. HEAD~2 går to commits tilbage fra den nyeste tegning. Brug det som genvej i git show, eller indsæt commit-id’et fra loggen.",
                "Læs, hvad der blev tilføjet i det valgte commit, og sammenlign med de andre udgaver af broen."
            ],
            """
            git init -b main
            # Opret bro.md med 'Broen er bygget af træ.'.
            git add bro.md
            git commit -m "Beskriv træbroen"
            # Tilføj 'Redningskransen hænger på broen.'.
            git add bro.md
            git commit -m "Tilføj en redningskrans"
            # Tilføj 'Der ligger et reb ved redningskransen.'.
            git add bro.md
            git commit -m "Beskriv redningsrebet"
            # Tilføj 'En lampe lyser ved broen.'.
            git add bro.md
            git commit -m "Beskriv lampen"

            git log --oneline -- bro.md
            git show HEAD~2 -- bro.md
            """,
            new("Git-manualen om git log", "https://git-scm.com/docs/git-log", true, false)),
        Lesson("stash-en-kladde", "Læg en kladde til side med stash",
            "Gitmejsen lægger en rullet tegning i en barklomme ved vandet. Snusmumrikkens grønne hat og mundharmonika ligger på bordet.",
            "Pak en ufærdig ændring væk, få en ren arbejdsmappe, og hent kladden frem igen.",
            [
                "Brug en ren øvemappe, hvor README.md allerede er med i et commit. Tilføj linjen »Mål taget før fredag.«, og gem filen uden at stage den.",
                "Gem den lokale ændring i en navngivet stash med git stash push. Brug git status til at se den rene arbejdsmappe, og find kladden med git stash list.",
                "Brug git stash apply til at hente din seneste kladde ind i arbejdsmappen igen. Øvelsen slutter med den ufærdige ændring i filen og kladden i stash-listen."
            ],
            """
            # Tilføj 'Mål taget før fredag.' til README.md, og gem.
            git status --short
            git stash push -m "Kladde til målene" -- README.md
            git status --short
            git stash list
            git stash apply 'stash@{0}'
            git diff -- README.md
            """,
            new("Git-manualen om git stash", "https://git-scm.com/docs/git-stash", true, false)),
        Lesson("hent-fra-remote", "Hent ændringer med fetch",
            "En færge sejler ekstra tagtegninger fra det blå Mumihus til Gitmejsens arbejdsbord. Hendes egen tegning sidder fortsat fast på tavlen ved siden af hende.",
            "Hent ændringer fra et fælles repository, og undersøg dem før en fletning.",
            [
                "Opret en tom øvemappe. Mappen central.git er fælleslageret, og værkstederne A og B er lokale kopier. Opret tag.md i A med linjen »Taget følger tegning A.«. Efter første push kloner du B.",
                "Tilføj linjen »En ekstra tagtegning er klar.« i A, og send det nye commit til central.git. Skift til B, og hent med git fetch. Øvelsens kommunikation foregår mellem mapper på din computer.",
                "Fetch modtager de nye Git-objekter og opdaterer fjernsporingsreferencen origin/main. Sammenlign det hentede indhold med B’s egen main. Loggen viser det commit, du kan vælge at flette bagefter."
            ],
            """
            git init --bare -b main central.git
            git clone ./central.git vaerksted-a
            cd vaerksted-a
            # Opret tag.md med 'Taget følger tegning A.'.
            git add tag.md
            git commit -m "Beskriv den første tagtegning"
            git push -u origin main
            cd ..
            git clone ./central.git vaerksted-b

            cd vaerksted-a
            # Tilføj 'En ekstra tagtegning er klar.' til tag.md.
            git add tag.md
            git commit -m "Tilføj den ekstra tagtegning"
            git push
            cd ../vaerksted-b

            git fetch origin
            git log --oneline main..origin/main
            git diff main origin/main -- tag.md
            git status --short --branch
            """,
            new("Git-manualen om git fetch", "https://git-scm.com/docs/git-fetch", true, false)),
        Lesson("fortryd-med-revert", "Fortryd med revert",
            "Tre modeller af Mumihuset står på række. Den første og sidste har hvidt gelænder på verandaen, og den midterste har en åben veranda. Gitmejsen og Mumifars hat er ved siden af.",
            "Tilføj et nyt commit, der ophæver en tidligere ændring til Mumihusets veranda.",
            [
                "Start i en tom øvemappe. Commit veranda.md med teksten »Verandaen har hvidt gelænder.«. Erstat linjen med »Verandaen er åben.«, og gem ændringen i et nyt commit.",
                "Kør git revert --no-edit HEAD for at lave et nyt commit med den modsatte ændring. Her peger HEAD på dit nyeste commit med den åbne veranda.",
                "Læs de tre commits i loggen. Brug git show til at se, hvordan det sidste commit sætter det hvide gelænder tilbage."
            ],
            """
            git init -b main
            # Opret veranda.md med 'Verandaen har hvidt gelænder.'.
            git add veranda.md
            git commit -m "Beskriv verandaens gelænder"
            # Erstat linjen med 'Verandaen er åben.'.
            git add veranda.md
            git commit -m "Åbn verandaen"
            git show HEAD -- veranda.md

            git revert --no-edit HEAD
            git log -3 --oneline
            git show HEAD -- veranda.md
            """,
            new("Git-manualen om git revert", "https://git-scm.com/docs/git-revert", true, false)),
        Lesson("tag-en-udgivelse", "Mærk en udgivelse med et tag",
            "Ét messingmærke hænger fast på det midterste af tre syltetøjsglas. Gitmejsen står ved mærket, og Mumimors stribede forklæde og sorte taske er til venstre.",
            "Sæt et versionsnavn med en besked på et bestemt commit, og sammenlign det med den næste version.",
            [
                "Brug en ren øvemappe med README.md i historikken, og vælg et ledigt versionsnavn som v0.1.0. Markér den aktuelle version med git tag -a. Det annoterede tag gemmer din besked.",
                "Tilføj linjen »Næste prøve er på tirsdag.« til README.md, og lav et nyt commit. Kør git rev-parse for at læse commit-id’et bag v0.1.0 og det id, HEAD peger på nu.",
                "Ligesom messingmærket på glasset knytter Git-tagget et fast navn til den valgte version. Øvelsen opretter et lokalt tag og et lokalt commit."
            ],
            """
            git tag -a v0.1.0 -m "Første version af fuglehuset"
            git show --no-patch v0.1.0
            # Tilføj 'Næste prøve er på tirsdag.' i README.md.
            git add README.md
            git commit -m "Planlæg næste prøve"

            git rev-parse 'v0.1.0^{commit}'
            git rev-parse HEAD
            """,
            new("Git-manualen om git tag", "https://git-scm.com/docs/git-tag", true, false)),
        Lesson("find-fejlen-med-bisect", "Find første fejl med bisect",
            "Gitmejsen peger på det fjerde af seks billeder af det samme fyr. Lyset er tændt på de tre første og slukket på de tre sidste. Mumifars hat ligger ved søjournalen.",
            "Find det første commit, hvor fyrets lys gik ud, ved at gentage den samme test.",
            [
                "Start i en tom øvemappe. Lav seks commits med kommandoerne nedenfor. Fyret er tændt i de første tre og slukket i de sidste tre.",
                "Start bisect med det seneste commit som kendt dårligt og det første som kendt godt. Git afprøver omtrent midten af det resterende interval. Den samme git grep-test returnerer 0, når signal.txt indeholder en linje med taendt, og ellers 1.",
                "Git bisect run gentager testen, til Git har fundet det første dårlige commit. Sammenlign den udpegede besked med »Sluk fyrets lys«. Afslut med git bisect reset for at komme tilbage til den branch, du startede på."
            ],
            """
            git init -b main
            # Opret signal.txt med linjen 'taendt'.
            git add signal.txt
            git commit -m "Tænd fyrets lys"
            git tag god

            # Opret README.md med 'Fyret står på øen.'.
            git add README.md
            git commit -m "Beskriv fyrets placering"
            # Tilføj 'Passagen er smal.' i README.md.
            git add README.md
            git commit -m "Beskriv passagen"
            # Skriv 'slukket' i signal.txt, og gem.
            git add signal.txt
            git commit -m "Sluk fyrets lys"
            # Tilføj 'Adgang via søen.' i README.md.
            git add README.md
            git commit -m "Beskriv adgangen til øen"
            # Tilføj 'Lygtehuset er rundt.' i README.md.
            git add README.md
            git commit -m "Beskriv lygtehuset"

            git bisect start HEAD god
            git bisect run git grep -q '^taendt[[:space:]]*$' -- signal.txt
            git bisect reset
            """,
            new("Git-manualen om git bisect", "https://git-scm.com/docs/git-bisect", true, false))
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
