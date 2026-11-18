namespace Mercantec.Tech.Web.Characters;

internal static class CharacterCollaborations
{
    public const string Path = "/characters/collaborations";

    public static IReadOnlyList<CharacterCollaboration> All { get; } =
    [
        new("bilbo-raymond", "bilbo", "raymond", "Bilbo og Raymond · Fra C# til SQL",
            "Bilbo vil finde alle bestillinger på brombærsyltetøj. Han skriver forespørgslen i LINQ, og Raymond kigger på den SQL, EF Core og databaseprovideren oversætter den til. Databasen udfører forespørgslen, og EF Core omsætter resultatet til de typer, Bilbo bruger i C#.",
            "Bilbo holder en bestillingsseddel, mens Raymond peger på en tabel. En tavle viser vejen fra C# gennem EF Core til SQL."),
        new("docker-bilbo", "whale", "bilbo", "Docker og Bilbo · Tjek den nye version",
            "Docker ruller en ny version af syltetøjsboden ud. Bilbo prøver at åbne en bestilling, mens Docker tjekker, om tjenesten er klar til trafik. De aftaler på forhånd, hvornår de skal rulle tilbage.",
            "Havnekaptajnen Docker og Bilbo Backend ser på et advarselsikon og den tidligere grønne version på en bærbar computer ved kajens syltetøjsbod."),
        new("bilbo-gummianden", "bilbo", "gummianden", "Bilbo og Gummianden · Hvor kom den anden ordre fra?",
            "Bilbo forklarer Gummianden, hvad der sker fra det første klik til kvitteringen. De følger de to kald i loggen og undersøger, om de kom fra et dobbeltklik eller et genforsøg efter et mistet svar.",
            "Bilbo Backend viser Gummianden to ens bestillingssedler ved siden af ét glas brombærsyltetøj i sin kælder."),
        new("naesebladet-vatson", "bat", "vatson", "Næsebladet og Vatson · Fra afstand til handling",
            "Næsebladet måler afstanden til en klods. Vatson får lampen til at tænde, når klodsen kommer inden for den aftalte grænse. Flyt klodsen frem og tilbage, og undersøg, hvad styringen skal gøre, hvis der slet ikke kommer en måling.",
            "Næsebladet holder en træklods foran afstandssensoren, og Vatson viser den tændte lampe ved siden af styringen."),
        new("naesebladet-lupp", "bat", "lupp", "Næsebladet og LUPP · Afstand som spilinput",
            "Næsebladet bruger en afstandssensor som controller. LUPP ser, om spilleren kan forstå sammenhængen mellem bevægelsen foran sensoren og det, der sker på skærmen. Prøv to forskellige afstande, og aftal, hvad figuren skal gøre ved et manglende ekko.",
            "Næsebladet holder en klods foran en afstandssensor, og LUPP ser en lilla spilfigur hoppe på skærmen ved siden af sin baneskitse."),
        new("lupp-kamaeleonen", "lupp", "chameleon", "LUPP og Kamæleonen · Kan modellen sortere en ny brik?",
            "LUPP vil have spillet til at sortere brikker efter form. Kamæleonen holder en stribet trekant tilbage fra træningen. Når modellen har lært af eksemplerne, prøver de den nye brik og undersøger, om farven er kommet til at afgøre svaret.",
            "LUPP og Kamæleonen ser på spilbrikker med forskellige former. Kamæleonen holder en ny brik op, mens LUPP har sin store gule blyant ved siden af sig."),
        new("baever-raev", "beaver", "fox", "Bæver og Ræv · Hvem må åbne kortet?",
            "Bæver skriver en regel for, hvem der må læse et kort. Ræv prøver den med to testkonti. Ejerens kort kan åbnes med ejerens konto, mens det samme kort skal afvises for den anden konto. De gemmer begge forsøg som regressionstests.",
            "Bæver og Ræv tester adgang til to journaler. Kundens egen journal har et grønt flueben, og den anden er låst."),
        new("cyberbien-docker", "bee", "whale", "Cyberbien og Docker · Når den nye version melder fejl",
            "Docker holder øje med den nye version. Cyberbien sammenligner fejl og svartider med målingerne før udrulningen. De bruger tallene til at afgøre, om de skal fortsætte eller rulle tilbage.",
            "Cyberbien følger målinger på en skærm, mens havnekaptajnen Docker viser vejen tilbage til den tidligere version i sin logbog."),
        new("gitmejsen-bilbo", "gitmejsen", "bilbo", "Gitmejsen og Bilbo · To regler, én mergekonflikt",
            "Bilbo og Gitmejsen har ændret den samme bestillingsregel. Den ene ændring sætter et loft på antal glas, og den anden tjekker lageret. De samler reglerne og kører testene igen, så de ved, at begge grænser virker.",
            "Gitmejsen følger med, mens Bilbo tjekker to regler for syltetøjsordrer ved siden af et diagram, hvor to grene mødes."),
        new("kamaeleonen-raymond", "chameleon", "raymond", "Kamæleonen og Raymond · Hold samme violin samlet",
            "Raymond henter målingerne i SQL sammen med ID'et på hver violin. Kamæleonen deler derefter data efter violin, så alle målinger fra samme instrument ender i samme gruppe. Testen viser, hvordan modellen klarer sig på violiner, den møder for første gang.",
            "Raymond peger på kartoteket, mens Kamæleonen lægger tre kort fra den samme blåmærkede violin i én bakke."),
        new("cyberbien-naesebladet", "bee", "bat", "Cyberbien og Næsebladet · Hvem sendte målingen?",
            "Næsebladet måler afstanden i lydstudiet. Cyberbien hjælper med at sætte en gateway op, som verificerer sensorens identitet, før den tager imod målingen. De kontrollerer, at den registrerede sensor bliver godkendt, og at en ukendt enhed bliver afvist.",
            "Cyberbien og Næsebladet tjekker en afstandssensor i ekkostudiet. Gatewayen modtager målingen fra sensoren med et matchende mærke og afviser det fremmede mærke."),
        new("lupp-gummianden", "lupp", "gummianden", "LUPP og Gummianden · Platformen glemte at holde pause",
            "LUPP sætter spillet på pause. Spilleren står stille, men platformen kører videre. Gummianden får LUPP til at vise fejlen trin for trin. De sammenligner platformens position lige før og efter pausen og finder den opdatering, der stadig kører.",
            "LUPP og Gummianden ser på en turkis platform, der fortsætter opad på skærmen, mens spillets pausesymbol lyser."),
        new("gitmejsen-naesebladet", "gitmejsen", "bat", "Gitmejsen og Næsebladet · Mysteriet om den forsvundne tone",
            "En ændring i firmwaren har fået ekkotesten til at miste en tone. Næsebladet kører den samme test på en version midt mellem en, der virker, og en, der fejler. Gitmejsen bruger git bisect til at indsnævre feltet, indtil de står med den første ændring, hvor tonen forsvandt.",
            "Gitmejsen og Næsebladet lytter til vokscylinderen i midten, mens grønne og røde markører viser yderpunkterne i rækken."),
        new("docker-kamaeleonen", "whale", "chameleon", "Docker og Kamæleonen · Banjoen ved tolden",
            "Kamæleonen har trænet en model til at skelne instrumenter på deres lyd. Docker pakker modellen sammen med den forbehandling, den skal bruge. Ved toldboden sender de en kendt banjooptagelse gennem containeren og tjekker resultatet, før de ruller den ud.",
            "Kamæleonen afspiller en banjooptagelse i det flydende toldhus. Docker peger på den grønne lampe ved noden, efter at signalet er ført gennem de to sammenkoblede moduler."),
        new("raev-lupp", "fox", "lupp", "Ræv og LUPP · Spøgelset i genvejen",
            "LUPP har bygget to veje til samme checkpoint. På hovedruten tænder lanternen, og spillet gemmer spillerens fremskridt. Ræv tager genvejen med spøgelset og opdager, at checkpointet springes over. De prøver begge ruter igen efter rettelsen.",
            "Ræv følger genvejen med et papirspøgelse, mens LUPP sammenligner to testkort. Lanternen lyser efter hovedruten, men forbliver slukket efter genvejen."),
        new("raymond-vatson", "raymond", "vatson", "Raymond og Vatson · Nattoget klokken halv tre",
            "Da uret stilles tilbage ved skiftet til vintertid, kan stationen registrere to afgange med det samme lokale klokkeslæt. Vatson finder afgangene i loggen, og Raymond sorterer dem efter UTC i databasen. Billetten med 00:30 UTC kom en time før den med 01:30 UTC.",
            "På en togstation med et ur på 02.30 peger Raymond på billetten mærket 00:30 UTC. Vatson holder en strimmel fra loggen ved siden af."),
        new("raymond-bilbo-left-join", "raymond", "bilbo", "Raymond og Bilbo · Tæl alle kunder med LEFT JOIN",
            "Bilbo vil have alle kunder på bestillingslisten. På markedet lægger Raymond to sedler hos Alma, én hos Bo og lader Claras plads stå tom. Databasen skal give det samme overblik.",
            "Raymond peger på Claras tomme rum ved markedets syltetøjsbod. Alma har to bestillingssedler, Bo har én, og Clara er stadig med.")
        {
            Exercise = new("H1 · Databaseprogrammering · Mål 2 og 7",
                "Sæt forespørgslen ind efter øvedataene i PostgreSQL. Brug LEFT JOIN på kundens id, gruppér pr. kunde, og tæl ordrenøglerne med COUNT(orders.id). Prøv derefter COUNT(*) og sammenlign Claras tal.",
                "Alma får 2, Bo får 1, og Clara får 0. Med COUNT(*) får Clara 1, fordi hendes række stadig er med efter LEFT JOIN. COUNT(orders.id) springer NULL over.",
                """
                WITH customers(id, name) AS (
                    VALUES (1, 'Alma'), (2, 'Bo'), (3, 'Clara')
                ), orders(id, customer_id) AS (
                    VALUES (101, 1), (102, 1), (103, 2)
                )
                -- Skriv din SELECT her
                """)
        },
        new("naesebladet-gummianden-timeout", "bat", "gummianden", "Næsebladet og Gummianden · Test et manglende ekko",
            "Bag scenen prøver de tre målinger. Den midterste udløber, og Gummianden vil se, om displayet viser, at målingen mangler. Næsebladet tester visningen med tal fra et array.",
            "Gummianden lytter, mens Næsebladet tester en afstandssensor bag scenen. Tre billedfelter viser et kort ekko, et manglende ekko med et timeglas og et længere ekko.")
        {
            Exercise = new("IoT · Sensorer, timeout og testdata",
                "Skriv en funktion i C++, der formaterer én prøve som en afstand i cm eller teksten »Intet ekko«. Kør funktionen på alle tre øvedata. Værdien −1 er markøren for timeout.",
                "Visningen skal i rækkefølge give »18 cm«, »Intet ekko« og »25 cm«. Den sidste værdi viser, at visningen også kommer videre efter et manglende svar.",
                """
                constexpr int INTET_EKKO = -1;
                int maalinger[] = {18, INTET_EKKO, 25};
                """)
        },
        new("kamaeleonen-raev-confusion", "chameleon", "fox", "Kamæleonen og Ræv · Find fejlene i frugtsorteringen",
            "Ræv finder et æble i pærekassen. Kamæleonen tæller hver frugts faktiske type og modellens bud for at se, hvor modellen tager fejl.",
            "Ræv peger på et æble i pærekassen, mens Kamæleonen betjener sorteringen. Fire kort viser, hvordan æbler og pærer kan blive sorteret rigtigt eller forkert.")
        {
            Exercise = new("Machine Learning · Forvekslingsmatrix",
                "Modellen ser 10 æbler og 10 pærer. Den kalder 8 æbler for æbler og 2 for pærer. Den kalder 1 pære for et æble og 9 pærer for pærer. Tegn en 2 × 2-matrix med faktisk type på rækkerne og forudsagt type i kolonnerne. Begynd med æble på begge akser. Hvor stor en andel af æblerne finder modellen?",
                "Æblerækken bliver 8 og 2, pærerækken 1 og 9. Modellen finder 8 af 10 faktiske æbler, altså 80 %. Den klassificerer 17 af de 20 frugter korrekt.")
        },
        new("docker-gitmejsen-ci", "whale", "gitmejsen", "Docker og Gitmejsen · Stop containeren ved en rød test",
            "Gitmejsen finder en rød test på havnens kontrolpost. Containerbygget må vente, til testen er rettet og kørt igen.",
            "Gitmejsen ser en rød test ved bommen. Docker holder containeren tilbage på sporet foran lastekranen.")
        {
            Exercise = new("CI og deployment · Test før containerbuild",
                "Åbn et .NET-øvelsesprojekt med en Dockerfile og mindst én bestået test. Kør kommandoen i Bash fra projektets rod. Få derefter en assertion til at fejle, kør igen, og kig efter, om Docker går i gang. Gendan assertionen og kør en sidste gang.",
                "Når testen fejler, stopper kommandoen efter dotnet test. Når testene er grønne igen, går docker build i gang.",
                "dotnet test && docker build -t undervisning/syltetoej:lokal .")
        },
        new("raymond-baever-transaktion", "raymond", "beaver", "Raymond og Bæver · Tre brikker skal skifte kasse",
            "I Blåbækhuset flytter Bæver reservedele fra én lagerkasse til en anden. Raymond samler de to lagerændringer i én databasetransaktion, så en afbrudt flytning kan rulles tilbage.",
            "Raymond holder tre blå brikker over en vægt, mens Bæver tjekker lagerkassen i sit værksted.")
        {
            Exercise = new("SQL · Transaktioner",
                "Start med 10 brikker i kasse A og 4 i B. Flyt 3 fra A til B i én transaktion. Forestil dig, at programmet stopper efter det første trin. Hvad skal tallene vise efter ROLLBACK og efter en fuldført flytning?",
                "Efter ROLLBACK står der 10 i A og 4 i B. Efter COMMIT står der 7 i begge kasser.")
        },
        new("naesebladet-docker-offline", "bat", "whale", "Næsebladet og Docker · Målinger i stormvejr",
            "Forbindelsen ryger, mens Næsebladet måler afstanden ved kajen. Docker hjælper ham med at gemme målingerne i en lokal kø og vente på en kvittering, før et målekort kan fjernes.",
            "Næsebladet står ved radioen og afstandssensoren, mens Docker viser tre målekort i en kasse i flydehuset.")
        {
            Exercise = new("IoT og deployment · En kø til målinger",
                "Læg målingerne 101, 102 og 103 i kø. Modtageren kvitterer for 101, og så afbrydes forbindelsen, før de næste bliver sendt. Hvad ligger i køen, når forbindelsen kommer tilbage?",
                "101 er kvitteret og fjernes. 102 og 103 ligger stadig i køen og sendes i den rækkefølge.")
        },
        new("lupp-gitmejsen-spilgrene", "lupp", "gitmejsen", "LUPP og Gitmejsen · En bro eller tre trædesten?",
            "LUPP vil afprøve to udgaver af den samme bane. Gitmejsen laver en Git-gren til broen og en anden til trædestenene. De lader spilleren begynde samme sted med det samme mål i begge forsøg.",
            "Gitmejsen og LUPP ser to papbaner med samme start og mål. Den ene har en bro, den anden trædesten.")
        {
            Exercise = new("Game Design og Git · To grene fra samme start",
                "Tegn et fælles startpunkt i Git-historikken og to grene med hver sin ændring. Vælg én ting at måle i begge spiltests, fx tiden fra porten til stjernen.",
                "Begge grene starter ved den samme commit. I de to spiltests bruger du samme start, mål og målemetode, så du kan sammenligne ændringerne i ruten.")
        },
        new("lupp-cyberbien-spillerlog", "lupp", "bee", "LUPP og Cyberbien · Hvad skal stå i spillets log?",
            "LUPP vil vide, hvilken version der sendte spilleren tilbage til start. Cyberbien hjælper med at vælge de oplysninger, der skal med i loggen, så holdet kan finde fejlen med færre personoplysninger.",
            "I Papirhuset skjuler Cyberbien personfeltet på spillets logkort, mens LUPP følger billedspillet. Symbolet for den tekniske hændelse er stadig synligt.")
        {
            Exercise = new("Game Design og cybersikkerhed · Dataminimering",
                "Et logkort har spillerens navn, versionsnummer, bane og hændelsen »sendt til start«. Sammensæt et logkort, som holdet kan bruge til at tælle fejlen pr. version og bane.",
                "Behold versionsnummer, bane og hændelse. De tre felter er tilstrækkelige til optællingen.")
        },
        new("raymond-cyberbien-laeseadgang", "raymond", "bee", "Raymond og Cyberbien · Et kig i kataloget",
            "Raymond laver et view med bogtitler og udlånsstatus. Cyberbien hjælper ham med at give gæster læseadgang til viewet. Bibliotekaren har ansvaret for ændringer i hovedkataloget.",
            "Raymond viser Cyberbien en offentlig læsekopi under glas i bibliotekets læsesal. Det originale katalog står låst bag skranken.")
        {
            Exercise = new("SQL og cybersikkerhed · Læserettigheder til et view",
                "Et katalog har bogtitel, udlånsstatus og lånerens navn. Beslut, hvad gæster skal kunne se i viewet, og hvilken rettighed de behøver for at læse det.",
                "Viewet kan vise titel og udlånsstatus. Gæster får SELECT på viewet, og adgang til hovedkataloget gives særskilt til bibliotekaren.")
        },
        new("raev-gummianden-graense", "fox", "gummianden", "Ræv og Gummianden · Der er plads til syv",
            "Ræv tester en regel om højst syv brikker. Gummianden beder Ræv læse reglen højt efter at have set, at programmet afviser netop den syvende brik. De gemmer grænsetilfældene som tests.",
            "Ræv og Gummianden afprøver en tællegrænse ved en lille markedsbod. Kortene med 6 og 7 perler har grønne flueben, mens kortet med 8 får et rødt kryds.")
        {
            Exercise = new("Softwaretest og debugging · Test ved grænsen",
                "Prøv reglen med 6, 7 og 8 brikker. Hvilke to tilfælde skal godkendes? Ret udtrykket antal < 7, så det passer til reglen.",
                "6 og 7 skal godkendes. 8 skal afvises. Udtrykket antal <= 7 inkluderer den syvende brik.")
        },
        new("bilbo-vatson-validering", "bilbo", "vatson", "Bilbo og Vatson · Hvor mange glas?",
            "Vatson tager imod bestillinger ved Brombærbos dør. Bilbo hjælper ham med at fange en seddel uden antal, før den bliver til en ordre i systemet.",
            "Bilbo og Vatson kontrollerer to bestillinger foran Brombærbos grønne dør. En pil sender sedlen med tomt felt tilbage, mens den udfyldte ligger ved syltetøjet.")
        {
            Exercise = new("Backend og teknologi · Validering af input",
                "Afprøv tre fiktive ordrer. Den første bestiller 2 glas, den anden 0, og den tredje har tomt antalsfelt. Hvilke sendes videre, hvis en ordre kræver mindst ét glas?",
                "Ordren på 2 glas sendes videre. De to andre får en besked om at indtaste mindst ét glas.")
        },
        new("docker-raev-volume", "whale", "fox", "Docker og Ræv · Noterne bliver på kajen",
            "Ræv vil tjekke, om testnoterne er der efter en opdatering. Docker viser, hvordan et navngivet volume kan genbruges, når en container bliver udskiftet.",
            "Docker og Ræv udskifter den blå container med en grøn ved kajen. Deres mærkede notesbog bliver i en åben skattekiste på kajen.")
        {
            Exercise = new("Deployment og test · Data på et volume",
                "Forestil dig, at version 1 skriver »Første test bestået« til volumet noter. Version 2 får monteret det samme volume. Hvad skal Rævs test lede efter efter udskiftningen?",
                "Den tidligere note skal stadig kunne læses. Testen åbner version 2 og sammenligner indholdet med det, version 1 gemte.")
        },
        new("vatson-gummianden-kontaktspring", "vatson", "gummianden", "Vatson og Gummianden · Ét tryk, tre skift",
            "Vatson ser, at én fysisk knap hurtigt skifter mellem åben og lukket. Gummianden hjælper ham med at gennemgå tidslinjen og vente, til signalet er stabilt, før trykket registreres.",
            "Vatson og Gummianden tester en stor trykknap på bordet i Prototypehuset. Skærmen viser flere tætte pulser til venstre og én ren puls til højre.")
        {
            Exercise = new("Automation og debugging · Kontaktspring",
                "Kontakten lukker ved 0 ms, åbner ved 3 ms og lukker igen ved 7 ms. Den forbliver lukket. Hvornår kan der registreres ét tryk, hvis lukket skal have været stabilt i 20 ms?",
                "Trykket kan registreres ved 27 ms. De 20 ms regnes fra den sidste ændring ved 7 ms.")
        },
        new("kamaeleonen-gitmejsen-datasaet", "chameleon", "gitmejsen", "Kamæleonen og Gitmejsen · Gem det rigtige datasæt",
            "Kamæleonen vil gentage et forsøg med violinoptagelser. Gitmejsen hjælper med at skrive versionen af datasættet ind i forsøgsnoterne, så de kan finde optagelserne igen, selv efter nye er kommet til.",
            "Kamæleonen åbner en forseglet prøvebakke i Cremonaværkstedet, mens Gitmejsen sammenligner voksseglet med mærket i notesbogen.")
        {
            Exercise = new("Machine Learning og Git · Gentag et forsøg",
                "Forsøg A bruger datasættet v1 med 100 optagelser. Senere tilføjes 20 i v2. Hvilken version skal du vælge for at gentage A, og hvad skal du ellers gemme om forsøget?",
                "Vælg v1. Gem også versionen af koden samt indstillingerne og opdelingen af data, så du kan få samme grundlag for kørslen.")
        },
        new("bilbo-kamaeleonen-usikkert-svar", "bilbo", "chameleon", "Bilbo og Kamæleonen · Send det usikre glas til tjek",
            "Kamæleonens model genkender etiketter på syltetøjsglas. Bilbo hjælper med at bygge et API, der sender glasset til manuel kontrol, når modellens score er under den aftalte grænse.",
            "Kamæleonen sætter et glas marmelade med en tvetydig frugtlabel til side, og Bilbo gør klar til at undersøge det nærmere.")
        {
            Exercise = new("Backend og Machine Learning · En grænse for modellens svar",
                "Grænsen er 0,80. En score på 0,80 eller derover går videre automatisk. Hvad skal API'et gøre med scorerne 0,92, 0,80 og 0,61?",
                "0,92 og 0,80 går videre automatisk. 0,61 sendes til manuel kontrol.")
        },
        new("naesebladet-baever-hysterese", "bat", "beaver", "Næsebladet og Bæver · Hold lampen rolig",
            "En afstand ændrer sig lidt, når træklodsen står tæt på grænsen. Næsebladet måler, og Bæver hjælper med at vælge to grænser, så lampen beholder sin tilstand mellem dem.",
            "Bæver flytter et træmål mellem to farvede markeringer på måleskinnen, mens Næsebladet holder øje med den tændte lampe.")
        {
            Exercise = new("IoT og test · Hysterese",
                "Start med lyset slukket. Tænd ved 18 cm eller mindre og sluk ved 22 cm eller mere. Behold tilstanden mellem grænserne. Afprøv 24, 18, 20, 21 og 22 cm.",
                "Lampen er slukket ved 24 cm, tænder ved 18 cm, bliver tændt ved 20 og 21 cm og slukker igen ved 22 cm.")
        }
    ];

    public static IReadOnlyList<CharacterCollaboration> For(string slug) =>
        All.Where(scene => scene.FirstSlug == slug || scene.SecondSlug == slug).ToArray();
}

public sealed record CharacterCollaboration(string Id, string FirstSlug, string SecondSlug, string Title, string Description, string Alt)
{
    public const string SectionId = "character-collaborations-title";
    public CharacterCollaborationExercise? Exercise { get; init; }
    public string Anchor => $"collaboration-{Id}";
    public string Image => $"/images/characters/collaborations/{Id}.png";
    public string Preview => $"/images/characters/collaborations/{Id}.webp";
    public string PartnerSlug(string slug) => slug == FirstSlug ? SecondSlug : FirstSlug;
}

public sealed record CharacterCollaborationExercise(string Topic, string Task, string ExpectedResult, string? StarterCode = null);
