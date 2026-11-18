namespace Mercantec.Tech.Web.Characters;

internal static class CharacterCollaborations
{
    public const string Path = "/characters/collaborations";

    public static IReadOnlyList<CharacterCollaboration> All { get; } =
    [
        new("bilbo-raymond", ["bilbo", "raymond"], "Bilbo og Raymond · Fra C# til SQL",
            "Bilbo vil finde alle bestillinger på brombærsyltetøj. Han skriver forespørgslen i LINQ, og Raymond kigger på den SQL, EF Core og databaseprovideren oversætter den til. Databasen udfører forespørgslen, og EF Core omsætter resultatet til de typer, Bilbo bruger i C#.",
            "Bilbo holder en bestillingsseddel, mens Raymond peger på en tabel. En tavle viser vejen fra C# gennem EF Core til SQL."),
        new("docker-bilbo", ["whale", "bilbo"], "Docker og Bilbo · Tjek den nye version",
            "Docker ruller en ny version af syltetøjsboden ud. Bilbo prøver at åbne en bestilling, mens Docker tjekker, om tjenesten er klar til trafik. De aftaler på forhånd, hvornår de skal rulle tilbage.",
            "Havnekaptajnen Docker og Bilbo Backend ser på et advarselsikon og den tidligere grønne version på en bærbar computer ved kajens syltetøjsbod."),
        new("bilbo-gummianden", ["bilbo", "gummianden"], "Bilbo og Gummianden · Hvor kom den anden ordre fra?",
            "Bilbo forklarer Gummianden, hvad der sker fra det første klik til kvitteringen. De følger de to kald i loggen og undersøger, om de kom fra et dobbeltklik eller et genforsøg efter et mistet svar.",
            "Bilbo Backend viser Gummianden to ens bestillingssedler ved siden af ét glas brombærsyltetøj i sin kælder."),
        new("naesebladet-vatson", ["bat", "vatson"], "Næsebladet og Vatson · Fra afstand til handling",
            "Næsebladet måler afstanden til en klods. Vatson får lampen til at tænde, når klodsen kommer inden for den aftalte grænse. Flyt klodsen frem og tilbage, og undersøg, hvad styringen skal gøre, hvis der slet ikke kommer en måling.",
            "Næsebladet holder en træklods foran afstandssensoren, og Vatson viser den tændte lampe ved siden af styringen."),
        new("naesebladet-lupp", ["bat", "lupp"], "Næsebladet og LUPP · Afstand som spilinput",
            "Næsebladet bruger en afstandssensor som controller. LUPP ser, om spilleren kan forstå sammenhængen mellem bevægelsen foran sensoren og det, der sker på skærmen. Prøv to forskellige afstande, og aftal, hvad figuren skal gøre ved et manglende ekko.",
            "Næsebladet holder en klods foran en afstandssensor, og LUPP ser en lilla spilfigur hoppe på skærmen ved siden af sin baneskitse."),
        new("lupp-kamaeleonen", ["lupp", "chameleon"], "LUPP og Kamæleonen · Kan modellen sortere en ny brik?",
            "LUPP vil have spillet til at sortere brikker efter form. Kamæleonen holder en stribet trekant tilbage fra træningen. Når modellen har lært af eksemplerne, prøver de den nye brik og undersøger, om farven er kommet til at afgøre svaret.",
            "LUPP og Kamæleonen ser på spilbrikker med forskellige former. Kamæleonen holder en ny brik op, mens LUPP har sin store gule blyant ved siden af sig."),
        new("baever-raev", ["beaver", "fox"], "Bæver og Ræv · Hvem må åbne kortet?",
            "Bæver skriver en regel for, hvem der må læse et kort. Ræv prøver den med to testkonti. Ejerens kort kan åbnes med ejerens konto, mens det samme kort skal afvises for den anden konto. De gemmer begge forsøg som regressionstests.",
            "Bæver og Ræv tester adgang til to journaler. Kundens egen journal har et grønt flueben, og den anden er låst."),
        new("cyberbien-docker", ["bee", "whale"], "Cyberbien og Docker · Når den nye version melder fejl",
            "Docker holder øje med den nye version. Cyberbien sammenligner fejl og svartider med målingerne før udrulningen. De bruger tallene til at afgøre, om de skal fortsætte eller rulle tilbage.",
            "Cyberbien følger målinger på en skærm, mens havnekaptajnen Docker viser vejen tilbage til den tidligere version i sin logbog."),
        new("gitmejsen-bilbo", ["gitmejsen", "bilbo"], "Gitmejsen og Bilbo · To regler, én mergekonflikt",
            "Bilbo og Gitmejsen har ændret den samme bestillingsregel. Den ene ændring sætter et loft på antal glas, og den anden tjekker lageret. De samler reglerne og kører testene igen, så de ved, at begge grænser virker.",
            "Gitmejsen følger med, mens Bilbo tjekker to regler for syltetøjsordrer ved siden af et diagram, hvor to grene mødes."),
        new("kamaeleonen-raymond", ["chameleon", "raymond"], "Kamæleonen og Raymond · Hold samme violin samlet",
            "Raymond henter målingerne i SQL sammen med ID'et på hver violin. Kamæleonen deler derefter data efter violin, så alle målinger fra samme instrument ender i samme gruppe. Testen viser, hvordan modellen klarer sig på violiner, den møder for første gang.",
            "Raymond peger på kartoteket, mens Kamæleonen lægger tre kort fra den samme blåmærkede violin i én bakke."),
        new("cyberbien-naesebladet", ["bee", "bat"], "Cyberbien og Næsebladet · Hvem sendte målingen?",
            "Næsebladet måler afstanden i lydstudiet. Cyberbien hjælper med at sætte en gateway op, som verificerer sensorens identitet, før den tager imod målingen. De kontrollerer, at den registrerede sensor bliver godkendt, og at en ukendt enhed bliver afvist.",
            "Cyberbien og Næsebladet tjekker en afstandssensor i ekkostudiet. Gatewayen modtager målingen fra sensoren med et matchende mærke og afviser det fremmede mærke."),
        new("lupp-gummianden", ["lupp", "gummianden"], "LUPP og Gummianden · Platformen glemte at holde pause",
            "LUPP sætter spillet på pause. Spilleren står stille, men platformen kører videre. Gummianden får LUPP til at vise fejlen trin for trin. De sammenligner platformens position lige før og efter pausen og finder den opdatering, der stadig kører.",
            "LUPP og Gummianden ser på en turkis platform, der fortsætter opad på skærmen, mens spillets pausesymbol lyser."),
        new("gitmejsen-naesebladet", ["gitmejsen", "bat"], "Gitmejsen og Næsebladet · Mysteriet om den forsvundne tone",
            "En ændring i firmwaren har fået ekkotesten til at miste en tone. Næsebladet kører den samme test på en version midt mellem en, der virker, og en, der fejler. Gitmejsen bruger git bisect til at indsnævre feltet, indtil de står med den første ændring, hvor tonen forsvandt.",
            "Gitmejsen og Næsebladet lytter til vokscylinderen i midten, mens grønne og røde markører viser yderpunkterne i rækken."),
        new("docker-kamaeleonen", ["whale", "chameleon"], "Docker og Kamæleonen · Banjoen ved tolden",
            "Kamæleonen har trænet en model til at skelne instrumenter på deres lyd. Docker pakker modellen sammen med den forbehandling, den skal bruge. Ved toldboden sender de en kendt banjooptagelse gennem containeren og tjekker resultatet, før de ruller den ud.",
            "Kamæleonen afspiller en banjooptagelse i det flydende toldhus. Docker peger på den grønne lampe ved noden, efter at signalet er ført gennem de to sammenkoblede moduler."),
        new("raev-lupp", ["fox", "lupp"], "Ræv og LUPP · Spøgelset i genvejen",
            "LUPP har bygget to veje til samme checkpoint. På hovedruten tænder lanternen, og spillet gemmer spillerens fremskridt. Ræv tager genvejen med spøgelset og opdager, at checkpointet springes over. De prøver begge ruter igen efter rettelsen.",
            "Ræv følger genvejen med et papirspøgelse, mens LUPP sammenligner to testkort. Lanternen lyser efter hovedruten, men forbliver slukket efter genvejen."),
        new("raymond-vatson", ["raymond", "vatson"], "Raymond og Vatson · Nattoget klokken halv tre",
            "Da uret stilles tilbage ved skiftet til vintertid, kan stationen registrere to afgange med det samme lokale klokkeslæt. Vatson finder afgangene i loggen, og Raymond sorterer dem efter UTC i databasen. Billetten med 00:30 UTC kom en time før den med 01:30 UTC.",
            "På en togstation med et ur på 02.30 peger Raymond på billetten mærket 00:30 UTC. Vatson holder en strimmel fra loggen ved siden af."),
        new("raymond-bilbo-left-join", ["raymond", "bilbo"], "Raymond og Bilbo · Tæl alle kunder med LEFT JOIN",
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
        new("naesebladet-gummianden-timeout", ["bat", "gummianden"], "Næsebladet og Gummianden · Test et manglende ekko",
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
        new("kamaeleonen-raev-confusion", ["chameleon", "fox"], "Kamæleonen og Ræv · Find fejlene i frugtsorteringen",
            "Ræv finder et æble i pærekassen. Kamæleonen tæller hver frugts faktiske type og modellens bud for at se, hvor modellen tager fejl.",
            "Ræv peger på et æble i pærekassen, mens Kamæleonen betjener sorteringen. Fire kort viser, hvordan æbler og pærer kan blive sorteret rigtigt eller forkert.")
        {
            Exercise = new("Machine Learning · Forvekslingsmatrix",
                "Modellen ser 10 æbler og 10 pærer. Den kalder 8 æbler for æbler og 2 for pærer. Den kalder 1 pære for et æble og 9 pærer for pærer. Tegn en 2 × 2-matrix med faktisk type på rækkerne og forudsagt type i kolonnerne. Begynd med æble på begge akser. Hvor stor en andel af æblerne finder modellen?",
                "Æblerækken bliver 8 og 2, pærerækken 1 og 9. Modellen finder 8 af 10 faktiske æbler, altså 80 %. Den klassificerer 17 af de 20 frugter korrekt.")
        },
        new("docker-gitmejsen-ci", ["whale", "gitmejsen"], "Docker og Gitmejsen · Stop containeren ved en rød test",
            "Gitmejsen finder en rød test på havnens kontrolpost. Containerbygget må vente, til testen er rettet og kørt igen.",
            "Gitmejsen ser en rød test ved bommen. Docker holder containeren tilbage på sporet foran lastekranen.")
        {
            Exercise = new("CI og deployment · Test før containerbuild",
                "Åbn et .NET-øvelsesprojekt med en Dockerfile og mindst én bestået test. Kør kommandoen i Bash fra projektets rod. Få derefter en assertion til at fejle, kør igen, og kig efter, om Docker går i gang. Gendan assertionen og kør en sidste gang.",
                "Når testen fejler, stopper kommandoen efter dotnet test. Når testene er grønne igen, går docker build i gang.",
                "dotnet test && docker build -t undervisning/syltetoej:lokal .")
        },
        new("raymond-baever-transaktion", ["raymond", "beaver"], "Raymond og Bæver · Tre brikker skal skifte kasse",
            "I Blåbækhuset flytter Bæver reservedele fra én lagerkasse til en anden. Raymond samler de to lagerændringer i én databasetransaktion, så en afbrudt flytning kan rulles tilbage.",
            "Raymond holder tre blå brikker over en vægt, mens Bæver tjekker lagerkassen i sit værksted.")
        {
            Exercise = new("SQL · Transaktioner",
                "Start med 10 brikker i kasse A og 4 i B. Flyt 3 fra A til B i én transaktion. Forestil dig, at programmet stopper efter det første trin. Hvad skal tallene vise efter ROLLBACK og efter en fuldført flytning?",
                "Efter ROLLBACK står der 10 i A og 4 i B. Efter COMMIT står der 7 i begge kasser.")
        },
        new("naesebladet-docker-offline", ["bat", "whale"], "Næsebladet og Docker · Målinger i stormvejr",
            "Forbindelsen ryger, mens Næsebladet måler afstanden ved kajen. Docker hjælper ham med at gemme målingerne i en lokal kø og vente på en kvittering, før et målekort kan fjernes.",
            "Næsebladet står ved radioen og afstandssensoren, mens Docker viser tre målekort i en kasse i flydehuset.")
        {
            Exercise = new("IoT og deployment · En kø til målinger",
                "Læg målingerne 101, 102 og 103 i kø. Modtageren kvitterer for 101, og så afbrydes forbindelsen, før de næste bliver sendt. Hvad ligger i køen, når forbindelsen kommer tilbage?",
                "101 er kvitteret og fjernes. 102 og 103 ligger stadig i køen og sendes i den rækkefølge.")
        },
        new("lupp-gitmejsen-spilgrene", ["lupp", "gitmejsen"], "LUPP og Gitmejsen · En bro eller tre trædesten?",
            "LUPP vil afprøve to udgaver af den samme bane. Gitmejsen laver en Git-gren til broen og en anden til trædestenene. De lader spilleren begynde samme sted med det samme mål i begge forsøg.",
            "Gitmejsen og LUPP ser to papbaner med samme start og mål. Den ene har en bro, den anden trædesten.")
        {
            Exercise = new("Game Design og Git · To grene fra samme start",
                "Tegn et fælles startpunkt i Git-historikken og to grene med hver sin ændring. Vælg én ting at måle i begge spiltests, fx tiden fra porten til stjernen.",
                "Begge grene starter ved den samme commit. I de to spiltests bruger du samme start, mål og målemetode, så du kan sammenligne ændringerne i ruten.")
        },
        new("lupp-cyberbien-spillerlog", ["lupp", "bee"], "LUPP og Cyberbien · Hvad skal stå i spillets log?",
            "LUPP vil vide, hvilken version der sendte spilleren tilbage til start. Cyberbien hjælper med at vælge de oplysninger, der skal med i loggen, så holdet kan finde fejlen med færre personoplysninger.",
            "I Papirhuset skjuler Cyberbien personfeltet på spillets logkort, mens LUPP følger billedspillet. Symbolet for den tekniske hændelse er stadig synligt.")
        {
            Exercise = new("Game Design og cybersikkerhed · Dataminimering",
                "Et logkort har spillerens navn, versionsnummer, bane og hændelsen »sendt til start«. Sammensæt et logkort, som holdet kan bruge til at tælle fejlen pr. version og bane.",
                "Behold versionsnummer, bane og hændelse. De tre felter er tilstrækkelige til optællingen.")
        },
        new("raymond-cyberbien-laeseadgang", ["raymond", "bee"], "Raymond og Cyberbien · Et kig i kataloget",
            "Raymond laver et view med bogtitler og udlånsstatus. Cyberbien hjælper ham med at give gæster læseadgang til viewet. Bibliotekaren har ansvaret for ændringer i hovedkataloget.",
            "Raymond viser Cyberbien en offentlig læsekopi under glas i bibliotekets læsesal. Det originale katalog står låst bag skranken.")
        {
            Exercise = new("SQL og cybersikkerhed · Læserettigheder til et view",
                "Et katalog har bogtitel, udlånsstatus og lånerens navn. Beslut, hvad gæster skal kunne se i viewet, og hvilken rettighed de behøver for at læse det.",
                "Viewet kan vise titel og udlånsstatus. Gæster får SELECT på viewet, og adgang til hovedkataloget gives særskilt til bibliotekaren.")
        },
        new("raev-gummianden-graense", ["fox", "gummianden"], "Ræv og Gummianden · Der er plads til syv",
            "Ræv tester en regel om højst syv brikker. Gummianden beder Ræv læse reglen højt efter at have set, at programmet afviser netop den syvende brik. De gemmer grænsetilfældene som tests.",
            "Ræv og Gummianden afprøver en tællegrænse ved en lille markedsbod. Kortene med 6 og 7 perler har grønne flueben, mens kortet med 8 får et rødt kryds.")
        {
            Exercise = new("Softwaretest og debugging · Test ved grænsen",
                "Prøv reglen med 6, 7 og 8 brikker. Hvilke to tilfælde skal godkendes? Ret udtrykket antal < 7, så det passer til reglen.",
                "6 og 7 skal godkendes. 8 skal afvises. Udtrykket antal <= 7 inkluderer den syvende brik.")
        },
        new("bilbo-vatson-validering", ["bilbo", "vatson"], "Bilbo og Vatson · Hvor mange glas?",
            "Vatson tager imod bestillinger ved Brombærbos dør. Bilbo hjælper ham med at fange en seddel uden antal, før den bliver til en ordre i systemet.",
            "Bilbo og Vatson kontrollerer to bestillinger foran Brombærbos grønne dør. En pil sender sedlen med tomt felt tilbage, mens den udfyldte ligger ved syltetøjet.")
        {
            Exercise = new("Backend og teknologi · Validering af input",
                "Afprøv tre fiktive ordrer. Den første bestiller 2 glas, den anden 0, og den tredje har tomt antalsfelt. Hvilke sendes videre, hvis en ordre kræver mindst ét glas?",
                "Ordren på 2 glas sendes videre. De to andre får en besked om at indtaste mindst ét glas.")
        },
        new("docker-raev-volume", ["whale", "fox"], "Docker og Ræv · Noterne bliver på kajen",
            "Ræv vil tjekke, om testnoterne er der efter en opdatering. Docker viser, hvordan et navngivet volume kan genbruges, når en container bliver udskiftet.",
            "Docker og Ræv udskifter den blå container med en grøn ved kajen. Deres mærkede notesbog bliver i en åben skattekiste på kajen.")
        {
            Exercise = new("Deployment og test · Data på et volume",
                "Forestil dig, at version 1 skriver »Første test bestået« til volumet noter. Version 2 får monteret det samme volume. Hvad skal Rævs test lede efter efter udskiftningen?",
                "Den tidligere note skal stadig kunne læses. Testen åbner version 2 og sammenligner indholdet med det, version 1 gemte.")
        },
        new("vatson-gummianden-kontaktspring", ["vatson", "gummianden"], "Vatson og Gummianden · Ét tryk, tre skift",
            "Vatson ser, at én fysisk knap hurtigt skifter mellem åben og lukket. Gummianden hjælper ham med at gennemgå tidslinjen og vente, til signalet er stabilt, før trykket registreres.",
            "Vatson og Gummianden tester en stor trykknap på bordet i Prototypehuset. Skærmen viser flere tætte pulser til venstre og én ren puls til højre.")
        {
            Exercise = new("Automation og debugging · Kontaktspring",
                "Kontakten lukker ved 0 ms, åbner ved 3 ms og lukker igen ved 7 ms. Den forbliver lukket. Hvornår kan der registreres ét tryk, hvis lukket skal have været stabilt i 20 ms?",
                "Trykket kan registreres ved 27 ms. De 20 ms regnes fra den sidste ændring ved 7 ms.")
        },
        new("kamaeleonen-gitmejsen-datasaet", ["chameleon", "gitmejsen"], "Kamæleonen og Gitmejsen · Gem det rigtige datasæt",
            "Kamæleonen vil gentage et forsøg med violinoptagelser. Gitmejsen hjælper med at skrive versionen af datasættet ind i forsøgsnoterne, så de kan finde optagelserne igen, selv efter nye er kommet til.",
            "Kamæleonen åbner en forseglet prøvebakke i Cremonaværkstedet, mens Gitmejsen sammenligner voksseglet med mærket i notesbogen.")
        {
            Exercise = new("Machine Learning og Git · Gentag et forsøg",
                "Forsøg A bruger datasættet v1 med 100 optagelser. Senere tilføjes 20 i v2. Hvilken version skal du vælge for at gentage A, og hvad skal du ellers gemme om forsøget?",
                "Vælg v1. Gem også versionen af koden samt indstillingerne og opdelingen af data, så du kan få samme grundlag for kørslen.")
        },
        new("bilbo-kamaeleonen-usikkert-svar", ["bilbo", "chameleon"], "Bilbo og Kamæleonen · Send det usikre glas til tjek",
            "Kamæleonens model genkender etiketter på syltetøjsglas. Bilbo hjælper med at bygge et API, der sender glasset til manuel kontrol, når modellens score er under den aftalte grænse.",
            "Kamæleonen sætter et glas marmelade med en tvetydig frugtlabel til side, og Bilbo gør klar til at undersøge det nærmere.")
        {
            Exercise = new("Backend og Machine Learning · En grænse for modellens svar",
                "Grænsen er 0,80. En score på 0,80 eller derover går videre automatisk. Hvad skal API'et gøre med scorerne 0,92, 0,80 og 0,61?",
                "0,92 og 0,80 går videre automatisk. 0,61 sendes til manuel kontrol.")
        },
        new("naesebladet-baever-hysterese", ["bat", "beaver"], "Næsebladet og Bæver · Hold lampen rolig",
            "En afstand ændrer sig lidt, når træklodsen står tæt på grænsen. Næsebladet måler, og Bæver hjælper med at vælge to grænser, så lampen beholder sin tilstand mellem dem.",
            "Bæver flytter et træmål mellem to farvede markeringer på måleskinnen, mens Næsebladet holder øje med den tændte lampe.")
        {
            Exercise = new("IoT og test · Hysterese",
                "Start med lyset slukket. Tænd ved 18 cm eller mindre og sluk ved 22 cm eller mere. Behold tilstanden mellem grænserne. Afprøv 24, 18, 20, 21 og 22 cm.",
                "Lampen er slukket ved 24 cm, tænder ved 18 cm, bliver tændt ved 20 og 21 cm og slukker igen ved 22 cm.")
        },
        new("lupp-bilbo-raymond-gitmejsen-highscore", ["lupp", "bilbo", "raymond", "gitmejsen"], "Fra spilbane til highscore",
            "LUPP sender scoren fra spillet. Bilbo tjekker, at den ligger inden for spillets regler, og Raymond gemmer og sorterer de godkendte scorer. Gitmejsen gør testen til en del af udgivelsen.",
            "LUPP ser på spilbanen, Bilbo tjekker et stjernekort, og Raymond lægger en score i stativet, hvor Gitmejsen holder et testmærke.")
        {
            Exercise = new("Game Design, API, SQL og Git",
                "Scorerne er 90, 120, −10 og 75. Spillet tillader hele tal fra 0 til 500. Skriv, hvilke scorer API’et skal godkende, og hvordan Raymond skal vise dem på listen.",
                "−10 afvises. Listen viser fra toppen 120, 90 og 75. Gem begge regler som tests i projektet.")
        },
        new("naesebladet-vatson-docker-gummianden-signal", ["bat", "vatson", "whale", "gummianden"], "Hvor stopper målingen?",
            "Næsebladet ser målingen ved sensoren, og Vatson finder den i gatewayen. Docker leder efter den i modtagertjenesten, mens Gummianden hjælper dem med at gennemgå ét led ad gangen.",
            "Næsebladet og Vatson følger en måling frem til gatewayen. Docker og Gummianden står ved den mørke modtager på den anden side af kabelbruddet.")
        {
            Exercise = new("IoT, deployment og debugging",
                "Måling M17 ses kl. 10.00 i både sensorens og gatewayens log. Modtageren har endnu ingen M17. Hvilket led undersøger holdet først?",
                "Undersøg først leveringen fra gatewayen til modtageren. Holdet har allerede fulgt M17 frem til gatewayen.")
        },
        new("bilbo-raymond-baever-raev-sidste-glas", ["bilbo", "raymond", "beaver", "fox"], "To bestillinger på det sidste glas",
            "Ræv og Bæver prøver at bestille det samme sidste glas på én gang. Bilbo gør reservationen til ét kald, og Raymond sørger for, at databasen kun trækker ét glas fra lageret, hvis der stadig er et glas tilbage.",
            "Bæver og Ræv holder hver sin bestilling ved det sidste syltetøjsglas, mens Bilbo og Raymond tjekker, hvilken bestilling der kan godkendes.")
        {
            Exercise = new("Samtidighed, SQL og softwaretest",
                "Lageret starter på 1. Send to bestillinger samtidig i et testmiljø, og tjek svarene og antallet på lager bagefter. Hvad skal testen kræve?",
                "Én bestilling godkendes, og den anden får besked om, at varen er udsolgt. Lageret ender på 0. Testen behøver kun at kræve én vinder.")
        },
        new("lupp-vatson-raev-feedback", ["lupp", "vatson", "fox"], "Kan signalet læses uden farve?",
            "LUPP vælger former til spillets to signaler. Vatson gør dem synlige på prototypen, og Ræv prøver at læse dem med et filter, der fjerner farverne.",
            "LUPP, Vatson og Ræv afprøver en trækonsol i Papirhuset. Ræv holder en grå prøve foran den røde trekant, mens den grønne cirkel og LUPPs to formkort er synlige.")
        {
            Exercise = new("Game Design og test · Tilgængelig feedback",
                "En grøn cirkel betyder godkendt, og en rød trekant betyder stop. Vis begge signaler i gråtoner. Hvilken del af signalet kan spilleren stadig bruge?",
                "Cirklen og trekanten kan stadig skelnes. Brug formen sammen med en læsbar forklaring, så signalet også giver mening uden farven.")
        },
        new("docker-bilbo-raymond-migration", ["whale", "bilbo", "raymond"], "Gamle og nye bestillinger ved samme kaj",
            "Docker skal have to versioner kørende under en udrulning. Bilbo sender bestillinger fra begge versioner, og Raymond tilføjer et felt, som den nye version kan bruge, mens gamle bestillinger stadig kan gemmes.",
            "Docker, Bilbo og Raymond arbejder ved havnens pakkebord. Runde og firkantede ordrekort ligger i hver sin bane, og arkivskuffen har fået et ekstra felt.")
        {
            Exercise = new("Deployment, API og databaser · Kompatibilitet",
                "Tilføj et valgfrit felt til en leveringsnote. Prøv at oprette én bestilling fra den gamle version og én fra den nye. Hvilke data må hver version sende?",
                "Den gamle version kan oprette en bestilling med de eksisterende felter. Den nye kan sende leveringsnoten med. Begge består testen.")
        },
        new("raymond-baever-docker-backup", ["raymond", "beaver", "whale"], "Kan vi få de tre poster tilbage?",
            "Docker åbner en sikkerhedskopi i et separat testmiljø. Raymond læser de gendannede poster, og Bæver sammenligner dem med de tre poster, de forventer at finde.",
            "Raymond og Bæver tæller tre genoprettede brikker ved vandværkstedet. Docker åbner backupkassen, mens de lukkede driftsbakker står urørte bag bordet.")
        {
            Exercise = new("Databaser og drift · Gendannelse af backup",
                "Kopien skal indeholde posterne Stjerne, Cirkel og Trekant. Gendan den i et testmiljø, og find både antallet og navnene. Hvad skal resultatet vise?",
                "Der skal være præcis tre poster, én med hvert af de tre forventede navne. Sammenligning af navnene kan fange en fejl, selv hvis antallet er rigtigt.")
        },
        new("vatson-cyberbien-baever-opdatering", ["vatson", "bee", "beaver"], "Prøv opdateringen på én enhed",
            "Vatson sætter den nye version på værkstedets testenhed. Bæver kontrollerer, om målingerne kommer ind som forventet, og Cyberbien holder de øvrige enheder på den nuværende version, mens de ser resultatet an.",
            "Vatson, Cyberbien og Bæver prøver en opdatering på én enhed på testbordet. De øvrige enheder står samlet i en separat bakke.")
        {
            Exercise = new("Automation og softwaretest · Trinvis opdatering",
                "En testenhed skal levere tre målinger efter opdatering. Kun to af de tre kommer frem. Hvad gør holdet med næste gruppe?",
                "De venter med næste gruppe og undersøger, hvorfor den sidste måling mangler. En ny test med alle tre målinger giver grundlag for næste trin.")
        },
        new("cyberbien-bilbo-gummianden-supportlog", ["bee", "bilbo", "gummianden"], "Følg stjernen gennem loggen",
            "Bilbo finder et kald til bestillingssystemet. Gummianden følger det næste trin ved hjælp af det fælles spor-id, og Cyberbien viser, hvordan de kan dele loguddraget til fejlfinding med hemmelige felter skjult.",
            "Cyberbien, Bilbo og Gummianden finder samme stjernestempel på to logsedler i marmeladekøkkenet. En foldet papirflig dækker et felt, som de ikke behøver at læse.")
        {
            Exercise = new("Backend og debugging · Spor-id",
                "Tre loglinjer har mærkerne STJERNE, CIRKEL og STJERNE. Hvilke to hører til samme spor? Vælg fx spor-id, tidspunkt og hændelse til dit uddrag.",
                "De to linjer mærket STJERNE hører til samme spor. Spor-id, tidspunkt og hændelse er nok til at finde dem i denne øvelse.")
        },
        new("cyberbien-raev-lupp-privat-lobby", ["bee", "fox", "lupp"], "Hvem må komme ind i spilrummet?",
            "LUPP skitserer en privat lobby. Cyberbien hjælper med reglen for, hvornår en gæst får adgang, og Ræv tester forløbet både før og efter værtens godkendelse.",
            "Cyberbien, Ræv og LUPP afprøver en privat lobby i en papirverden. Værten har åbnet porten for én godkendt gæst, mens en ny anmodning venter udenfor.")
        {
            Exercise = new("Spil, adgang og test · Privat lobby",
                "En gæst sender en anmodning til en privat lobby. Skriv to testtilfælde med forventet resultat for samme gæst, før og efter værten godkender.",
                "Før godkendelsen venter gæsten uden adgang til spilrummet. Efter godkendelsen kan gæsten deltage.")
        },
        new("kamaeleonen-naesebladet-vatson-kalibrering", ["chameleon", "bat", "vatson"], "Når linealen og sensoren er uenige",
            "Næsebladet måler en kendt afstand med sensoren. Kamæleonen sammenligner med linealen, og Vatson noterer målingerne, så de kan se, om afvigelsen går igen.",
            "Kamæleonen sammenligner afstanden med en lineal, Næsebladet retter sensoren mod træmålet, og Vatson noterer resultatet i sit ringbind.")
        {
            Exercise = new("IoT og data · Kalibrering",
                "Ved 20 cm viser sensoren 23 cm, og ved 40 cm viser den 43 cm. Hvad er afvigelsen i de to forsøg? Hvilken kontrolmåling ville du lave bagefter?",
                "Begge målinger ligger 3 cm over linealen. Prøv en ny kendt afstand for at tjekke, om samme afvigelse også gælder dér.")
        },
        new("kamaeleonen-raev-gitmejsen-skygge", ["chameleon", "fox", "gitmejsen"], "Kan modellen stadig se en pære i skyggen?",
            "Ræv afprøver de samme pærer i lys og skygge. Kamæleonen sammenligner resultaterne, mens Gitmejsen gemmer dem med versionsnummeret på den model, de brugte.",
            "Ræv flytter en pære ind i skyggen ved prøveboden. Kamæleonen sammenligner de to resultater, mens Gitmejsen holder styr på modelmærket.")
        {
            Exercise = new("Machine Learning og test · Lys og skygge",
                "Den samme model finder 9 af 10 pærer i lys og 6 af de samme 10 i skygge. Beregn andelen i begge tilfælde. Hvad har holdet holdt ens?",
                "Andelene er 90 % og 60 %. De samme pærer og den samme model gør det muligt at sammenligne de to lysforhold i forsøget.")
        },
        new("kamaeleonen-raymond-bilbo-evaluering", ["chameleon", "raymond", "bilbo"], "Gem både modellens bud og den rigtige etiket",
            "Kamæleonens model giver et forkert bud på en etiket. Bilbo ser efter og retter etiketten, og Raymond gemmer modellens oprindelige bud sammen med den bekræftede etiket, så de kan tælle fejlene senere.",
            "Kamæleonen viser en forkert æbleforudsigelse for en pære. Bilbo tilføjer den korrekte pære, og Raymond arkiverer kortet med begge oplysninger.")
        {
            Exercise = new("Machine Learning, backend og SQL · Gem en rettelse",
                "Modellen skriver »æble«, mens en person bekræfter »pære«. Vælg værdier til felterne forudsagt og bekræftet. Hvordan opdager du fejlen ved en senere optælling?",
                "Gem æble som forudsagt og pære som bekræftet. Tæl rækker, hvor de to værdier er forskellige, for at finde fejlsvarene.")
        },
        new("lupp-bilbo-raymond-gummianden-seed", ["lupp", "bilbo", "raymond", "gummianden"], "Tilbage til præcis den samme bane",
            "LUPP vil vise en fejl i en tilfældigt bygget bane. Bilbo henter banens starttal, som Raymond har gemt sammen med versionen. Gummianden får LUPP til at gentage de samme trin i den genskabte bane.",
            "LUPP, Bilbo, Raymond og Gummianden sammenligner to ens papbaner. Begge har et kort med det samme blå agern.")
        {
            Exercise = new("Game Design, databaser og debugging · Genskab et forsøg",
                "Fejlen opstår i version 2 på en bane med seed 42, når spilleren går til højre og derefter hopper. Hvilke oplysninger skal du bruge til at gentage forsøget?",
                "Brug version 2, seed 42 og de samme spillerhandlinger i samme rækkefølge. Så har du samme udgangspunkt og kan undersøge, om fejlen gentager sig.")
        },
        new("naesebladet-vatson-baever-gummianden-simuleret-ur", ["bat", "vatson", "beaver", "gummianden"], "Flyt uret, mens sensoren venter",
            "Næsebladet får en måling ind. Vatson stiller testens ur frem, mens Bæver holder øje med, hvornår status skifter. Gummianden noterer tidspunktet, så de kan gentage testen uden ventetid.",
            "Vatson stiller et testur frem, mens Næsebladet, Bæver og Gummianden følger sensoren og statuslampen.")
        {
            Exercise = new("IoT og softwaretest · Simuleret ur",
                "Sidste måling kom ved 0 ms. Visningen skal skifte til »Måling for gammel«, når den er mindst 100 ms gammel. Stil testuret på 99, 100 og 101 ms. Hvilken status forventer du?",
                "Ved 99 ms vises målingen stadig som frisk. Ved både 100 og 101 ms viser systemet »Måling for gammel«.")
        },
        new("raymond-bilbo-cyberbien-raev-midlertidig-rolle", ["raymond", "bilbo", "bee", "fox"], "Et gæstekort, der udløber",
            "Raymond gemmer gæstekortets udløbstid. Bilbo bruger den i appens adgangskontrol, Cyberbien gennemgår reglen, og Ræv afprøver den lige før og ved udløbet.",
            "Raymond, Bilbo og Cyberbien tjekker et midlertidigt gæstekort. Ræv viser to kort med porten åben og lukket omkring udløbet.")
        {
            Exercise = new("Backend og cybersikkerhed · Udløb af adgang",
                "Et gæstekort giver læseadgang frem til klokken 14.00. Test klokken 13.59 og præcis 14.00 med samme kort. Hvad skal appen svare?",
                "Klokken 13.59 giver kortet læseadgang. Præcis klokken 14.00 er kortet udløbet, og appen afviser adgangen.")
        },
        new("raymond-bilbo-docker-koeliste", ["raymond", "bilbo", "whale"], "To sedler ved samme klokkeslæt",
            "Bilbo vil bladre i bestillingerne ved kajen. Raymond sorterer dem efter tidspunkt og bruger ordrenummeret, når to sedler har samme tidspunkt. Docker kontrollerer, at rækkefølgen er den samme ved næste hentning.",
            "Raymond sætter to bestillingssedler med forskellige stempler i en fast rækkefølge på kajen. Bilbo og Docker sammenligner rækken under det samme tidevandsur.")
        {
            Exercise = new("SQL og backend · Sortering med lige værdier",
                "Ordre 41 og 42 kom begge kl. 10.10. Ordre 43 kom kl. 10.11. Vis to ordrer pr. side. Sortér først efter tidspunkt og dernæst efter ordrenummer, begge stigende.",
                "Side 1 viser 41 og 42. Side 2 viser 43. Ordrenummeret afgør rækkefølgen for de to ordrer fra kl. 10.10.")
        },
        new("gitmejsen-raymond-kamaeleonen-dobbeltmaaling", ["gitmejsen", "raymond", "chameleon"], "Ét duplikat og to ens målinger",
            "Kamæleonen ser ens værdier i sit datasæt. Gitmejsen finder den prøve, der kom ind to gange, og Raymond bruger målingernes id til at bevare de selvstændige prøver.",
            "Gitmejsen lægger en gentaget leveringsseddel med samme bladsegl til side. Raymond og Kamæleonen beholder to enslydende prøver med forskellige røde og blå mærker.")
        {
            Exercise = new("SQL og Machine Learning · Dubletter",
                "En sensor sender M7 med værdien 18, den samme M7 igen og til sidst M8 med værdien 18. Hvilke målinger skal gemmes, hvis hvert id kun må forekomme én gang?",
                "Gem M7 og M8. Den gentagne M7 tæller én gang. M8 har sin egen identitet, selv om værdien også er 18.")
        },
        new("gitmejsen-bilbo-docker-featureflag", ["gitmejsen", "bilbo", "whale"], "Samme udgivelse, to indstillinger",
            "Docker bruger det samme containerimage til to øvemiljøer. Bilbo gør den nye bestillingsside afhængig af en indstilling, og Gitmejsen tjekker, at miljøerne bruger samme kodeversion.",
            "Bilbo tænder lanternen ved den ene syltetøjsbod, mens nabobodens låge er lukket. Gitmejsen og Docker sammenligner to ens kasser med det samme brombærmærke.")
        {
            Exercise = new("Backend, Git og deployment · Feature flag",
                "Sæt den nye bestillingsside til »til« i prøvemiljøet og »fra« i det andet miljø. Begge kører samme image. Hvad skal en besøgende se i hvert miljø?",
                "Prøvemiljøet åbner den nye side. Det andet miljø viser den nuværende side. Forskellen kommer fra indstillingen, mens begge miljøer kører samme image.")
        },
        new("baever-raev-cyberbien-oevedata", ["beaver", "fox", "bee"], "Byg et hold af fiktive elever",
            "Bæver gør databasen klar til en test. Ræv laver tre opdigtede elevkort til forskellige situationer, og Cyberbien hjælper med at holde virkelige elevoplysninger væk fra øvelsen.",
            "Bæver, Ræv og Cyberbien laver farverige øvekort med opdigtede dyrepiktogrammer ved skovens testbord. Klassens notesbog står lukket på hylden.")
        {
            Exercise = new("Softwaretest og cybersikkerhed · Fiktive testdata",
                "Lav tre fiktive elevkort til en holdliste. Ét skal være aktivt, ét skal være afmeldt, og ét skal vente på at blive optaget. Hvem skal vises i listen over aktive elever?",
                "Kun kortet med status »aktiv« skal vises. De tre fiktive kort er nok til at afprøve filtreringen.")
        },
        new("vatson-docker-cyberbien-afhaengighed", ["vatson", "whale", "bee"], "Hvilken del brugte vi sidst?",
            "Vatson sammenligner to næsten ens moduler. Cyberbien finder versionsmærket i godkendelseslisten, og Docker bruger den aftalte version, når han gentager bygget.",
            "Vatson, Docker og Cyberbien sammenligner to næsten ens moduler ved kystværkstedet. Kun modulets stjerneformede segl matcher kortet i den gemte mappe.")
        {
            Exercise = new("Deployment og cybersikkerhed · Fast version af en afhængighed",
                "Det godkendte build brugte bibliotek 2.4.1. Nu findes 2.4.2. Hvilken version bruger du til at gentage det samme build, og hvordan undersøger du 2.4.2?",
                "Brug 2.4.1 for at gentage bygget med samme biblioteksversion. Prøv 2.4.2 separat, og skift først den aftalte version, når testene er bestået.")
        },
        new("lupp-baever-raev-gendannelse", ["lupp", "beaver", "fox"], "Et gemmepunkt, der overlever afbrydelsen",
            "LUPP tegner to gemmepunkter i spillet. Bæver lægger den nye gemning klar til kontrol, og Ræv afbryder spillet midt i forsøget for at se, hvad der kan indlæses bagefter.",
            "LUPP, Bæver og Ræv undersøger et papirspil efter en afbrudt lagring. Spillebrikken er væltet, men den tidligere komplette kontroltilstand ligger bevaret i sin egen træramme.")
        {
            Exercise = new("Game Design og softwaretest · Afbrudt gemning",
                "Spillets seneste færdige gemning er ved porten. Spilleren når tårnet, men spillet afbrydes, mens den nye gemning er ved at blive skrevet. Hvor skal spilleren starte ved genåbning?",
                "Spilleren starter ved porten, hvor den seneste fuldførte gemning ligger. Gentag testen med en fuldført gemning ved tårnet, og kontrollér, at spillet nu starter dér.")
        },
        new("kamaeleonen-raymond-naesebladet-udfald", ["chameleon", "raymond", "bat"], "Hvad gør vi med den måling, der mangler?",
            "Næsebladet sender to brugbare målinger og melder et manglende ekko i midten. Raymond markerer hullet i datasættet, og Kamæleonen undersøger, hvordan det påvirker gennemsnittet.",
            "Midterste måleramme står tom med et timeglas. Raymond noterer udfaldet, mens Næsebladet undersøger sensoren, og Kamæleonen sammenligner de to brugbare målinger.")
        {
            Exercise = new("IoT, SQL og data · Manglende målinger",
                "Målingerne er 18 cm, mangler og 24 cm. Beregn gennemsnittet af de målte værdier. Beregn derefter, hvad gennemsnittet ville blive, hvis den manglende værdi blev sat til 0.",
                "De to målinger giver 21 cm i gennemsnit. Regner du det indsatte nul med, bliver resultatet 14 cm. Markér derfor den manglende værdi, før du regner.")
        },
        new("kamaeleonen-docker-raev-vintertest", ["chameleon", "whale", "fox"], "Virker frugtsorteringen også om vinteren?",
            "Ræv samler vinterfrugter til en ny test ved kajen. Kamæleonen sammenligner modellens resultater med den tidligere sæson, og Docker tjekker, hvilken modelversion der kører.",
            "Ræv lægger vinterens røde og grønne æbler frem ved sorteringskassen. Kamæleonen sammenligner resultaterne, og Docker følger den ravfarvede statuslampe.")
        {
            Exercise = new("Machine Learning og deployment · Data ændrer sig",
                "Samme model genkender 20 af 20 frugter i den gamle test og 14 af 20 i vintertesten. Beregn begge andele. Hvilke forhold vil du undersøge i næste forsøg?",
                "Andelene er 100 % og 70 %. Du kan fx undersøge lys, sne på frugten eller andre frugtsorter ved at variere ét forhold ad gangen.")
        },
        new("vatson-kamaeleonen-lupp-reaktionstid", ["vatson", "chameleon", "lupp"], "Spillet skal svare inden for 80 millisekunder",
            "Vatson måler tiden fra knappens tryk. Kamæleonen måler modellens behandling, og LUPP måler spillets visning. De lægger tiderne sammen for at se, hvor spilleren venter længst.",
            "Vatson ordner den turkise del af perlekæden, Kamæleonen undersøger den lange ravfarvede del, og LUPP følger spillets reaktion på skærmen.")
        {
            Exercise = new("Game Design og Machine Learning · Reaktionstid",
                "Indlæsningen tager 20 ms, modellen tager 65 ms, og visningen tager 15 ms. Trinnene kører i rækkefølge. Hvor meget skal modellens tid falde for at nå højst 80 ms i alt?",
                "Trinnene tager 100 ms i alt. Modellens tid skal falde med mindst 20 ms til højst 45 ms, hvis de to andre trin tager lige så lang tid som før.")
        },
        new("naesebladet-vatson-raymond-docker-mqtt", ["bat", "vatson", "raymond", "whale"], "Send kun temperatur til oversigten",
            "Næsebladet måler, og Vatson sætter rum og måletype på beskederne. Docker kører MQTT-brokeren. Raymond vælger det abonnement, der skal fylde temperaturoversigten.",
            "Næsebladet og Vatson sender målekort til Docker. Raymond modtager temperaturkort fra to rum, mens fugtkortet bliver sorteret til en anden bakke.")
        {
            Exercise = new("H3 · IoT-kæden · MQTT-topics",
                "En øvebroker modtager rum/a/temp, rum/b/temp og rum/a/fugt. Raymond abonnerer på rum/+/temp. Hvilke beskeder får han i oversigten?",
                "Han får rum/a/temp og rum/b/temp. Tegnet + matcher ét niveau i emnet, mens det sidste niveau skal hedde temp.")
            {
                TeachingSource = new("Næsebladets IoT-kort om MQTT", "/characters/bat#character-gallery-title", false, false)
            }
        },
        new("naesebladet-baever-gummianden-millis", ["bat", "beaver", "gummianden"], "Når millis går rundt",
            "Næsebladet tester, om den næste opdatering stadig bliver udløst, når uret går rundt. Bæver holder reglen samlet i én sammenligning. Gummianden får dem til at regne med de to værdier på hver sin side af skiftet.",
            "Næsebladet, Bæver og Gummianden følger et kort interval mellem to knopper på hver sin side af tællerens samling.")
        {
            Exercise = new("H3 C++ · Datatyper · Forløbet tid",
                "Brug et simuleret 32-bit ur. Sidste tidspunkt er 4 294 967 290, og det aktuelle er 4 efter omslaget. Beregn tiden som en 32-bit værdi uden fortegn. Skal en opdatering med intervallet 10 ms køre?",
                "Der er gået 10 ms. Subtraktionen som 32-bit værdi uden fortegn regner rundt over omslaget. Opdateringen kører ved grænsen på 10 ms.",
                "const uint32_t last = 4294967290U;\nconst uint32_t now = 4U;\nconst uint32_t elapsed = now - last;")
            {
                TeachingSource = new("C++ H3 · Datatyper og millis", "https://cpp.mercantec.tech/h3/datatyper", true, false)
            }
        },
        new("lupp-raev-gummianden-dobbelt-halvt", ["lupp", "fox", "gummianden"], "Fordobl eller halvér farten i Rune Run",
            "LUPP holder banen ens i tre forsøg. Ræv ændrer kun farten, og Gummianden hjælper med at skille den målte tid fra oplevelsen af, hvor svær banen er at styre.",
            "LUPP, Ræv og Gummianden sammenligner tre lige lange papbaner. De lilla brikker er nået forskelligt langt fra start.")
        {
            Exercise = new("Game Design · Rune Run · Fordobl eller halvér",
                "En figur skal løbe 12 felter. Normal fart er 2 felter pr. sekund. Beregn tiden ved halv, normal og dobbelt fart. Kør derefter forsøget i jeres prototype og notér, om figuren er lige let at styre.",
                "Ved samme strækning tager det 12, 6 og 3 sekunder. Styringen skal bedømmes i spiltesten, fx ved at tælle overskudte drejninger.")
            {
                TeachingSource = new("Rune Run · LUPPs undervisningskort", "/characters/lupp#character-gallery-title", false, false)
            }
        },
        new("raymond-bilbo-baever-normalisering", ["raymond", "bilbo", "beaver"], "Ét produkt på to bestillinger",
            "Bilbo viser to bestillinger, der begge har brombærsyltetøj. Raymond placerer produktets navn ét sted. Bæver prøver at omdøbe produktet og kontrollerer, at begge bestillinger stadig peger på det.",
            "Raymond, Bilbo og Bæver forbinder to ordremapper med ét fælles syltetøjskatalog. Små varelinjekort med samme brombærmærke danner forbindelserne.")
        {
            Exercise = new("SQL-H1 · Mål 5 og 17 · Relationer og normalisering",
                "Tegn Produkter, Ordrer og Ordrelinjer. Produkt P7 indgår med antal 2 på ordre 41 og antal 1 på ordre 42. Skriv de to ordrelinjer, og vis, hvor du ændrer produktets navn.",
                "Ordrelinjerne peger på henholdsvis ordre 41 og 42 og begge på P7. Antal gemmes på hver ordrelinje. Produktets aktuelle navn ændres én gang i Produkter.")
            {
                TeachingSource = new("SQL-H1 · Database-design og normalisering", "https://sql.mercantec.tech/learn/relations-04-normalization", true, false)
            }
        },
        new("raymond-docker-gitmejsen-indeks", ["raymond", "whale", "gitmejsen"], "Hvilken vej fandt ordren?",
            "Raymond sammenligner planer for den samme SELECT. Docker måler gentagne kørsler i øvemiljøet, og Gitmejsen noterer datasættet og forespørgslen, så målingen kan gentages.",
            "Raymond sammenligner den lange søgebakke med et fanebladskartotek ved havnen. Docker tager tid, og Gitmejsen følger den samme klokkemærkede prøve.")
        {
            Exercise = new("Databaseprogrammering · Mål 15 og 16 · Indeks og måling",
                "Kør samme SELECT på de samme øvedata før og efter et indeks på order_date. Kig i EXPLAIN efter den vej, PostgreSQL vælger. Hvis tiderne er 12, 13 og 18 ms før og 8, 9 og 20 ms efter, hvad er de to medianer?",
                "Medianerne er 13 og 9 ms. Sammenhold målingen med planen. PostgreSQL kan stadig vælge Seq Scan, fx når tabellen er lille eller forespørgslen læser mange rækker.",
                "EXPLAIN (ANALYZE, BUFFERS)\nSELECT * FROM orders\nWHERE order_date >= DATE '2024-04-01';")
            {
                TeachingSource = new("SQL-H1 · Backup, indeks og EXPLAIN", "https://sql.mercantec.tech/learn/drift-02-backup-explain", true, false)
            }
        },
        new("bilbo-gummianden-raev-http-svar", ["bilbo", "gummianden", "fox"], "Tre svar fra bestillings-API’et",
            "Bilbo skriver kontrakten for bestillings-API’et. Ræv afprøver en oprettelse, en ugyldig seddel og et opslag uden træffer. Gummianden læser statuskoden og svaret med dem.",
            "Bilbo, Gummianden og Ræv gennemgår tre tests ved syltetøjsbordet. Kortene viser en oprettet bestilling, et mangelfuldt skema og en tom hylde.")
        {
            Exercise = new("H2 · ASP.NET Core · Statuskoder og API-test",
                "Prøv en gyldig POST /orders, en POST uden det krævede antal og en GET på en ordre, som ikke findes. Vælg statuskode og tjek, at en vellykket oprettelse viser, hvor ordren kan findes.",
                "Forvent 201 med en Location til den nye ordre, 400 med en valideringsfejl og 404 for opslaget, der ikke finder ordren.")
            {
                TeachingSource = new("H2 · Web, API og test", "/courses/template/h2", false, false)
            }
        },
        new("cyberbien-raev-bilbo-auth", ["bee", "fox", "bilbo"], "Kender vi gæsten, og må gæsten komme ind?",
            "Bilbo beskytter en læserute i holdets øve-API. Cyberbien skelner mellem identitet og rettigheder, mens Ræv tjekker begge svar med holdets fiktive testbrugere.",
            "Cyberbien, Ræv og Bilbo sammenligner to adgangsforsøg i læsesalen. Det ene kort mangler identifikation, mens det andet viser et kendt medlem med et mærke til en anden hylde.")
        {
            Exercise = new("H2 · Sikkerhed · AuthN og AuthZ",
                "Kald en beskyttet øverute først uden gyldig identitet og derefter som en gyldig bruger uden den krævede rolle. Hvilken HTTP-status forventer du i hvert tilfælde?",
                "Forvent 401, når identiteten mangler eller er ugyldig. Forvent 403, når appen kender brugeren, men brugeren mangler den krævede rettighed.")
            {
                TeachingSource = new("H2 · Sikkerhed og test", "/courses/template/h2", false, false)
            }
        },
        new("docker-gitmejsen-bilbo-runbook", ["whale", "gitmejsen", "bilbo"], "Kan et andet hold gentage jeres deploy?",
            "Gitmejsen samler versionen og trinene i en runbook. Docker giver vejledningen til en makker ved reservestationen, og Bilbo tjekker, om det samme API svarer efter opstart.",
            "Docker, Gitmejsen og Bilbo følger en foldet billedvejledning ved kysten. Den rene reservestation gentager den fungerende stations opsætning med samme mærkede kasse og forventede grønne status.")
        {
            Exercise = new("DevNet · Deploy Or Die · Runbook",
                "Overlad runbooken til et andet hold på en ren øvemaskine. Bed dem starte samme image, sætte øvemiljøets port og åbne /health. Hvad skal de kunne finde i vejledningen?",
                "De skal kunne finde image og version, lokal opsætning, startkommando og den kontrol, der viser, at tjenesten svarer, fx status 200 på /health. Notér ét trin, hvis holdet måtte spørge undervejs.")
            {
                TeachingSource = new("DevNet · Deploy Or Die-forløbet", "/courses/template/deploy-or-die", false, false)
            }
        },
        new("vatson-docker-baever-iot-enheder", ["vatson", "whale", "beaver"], "Én måling på tværs af sensor og dashboard",
            "Vatson får sensorens råtal ind. Docker holder samme aftalte datakontrakt i øvemiljøet, og Bæver tjekker, hvad dashboardet viser i grader Celsius.",
            "Vatson, Docker og Bæver sammenholder en temperaturmåling fra et modul med en visning i testen. Termometrene står lige højt, mens råværdien og den omregnede værdi har hver sit enhedsmærke.")
        {
            Exercise = new("H3 · IoT, API og dashboard · Enheder i kæden",
                "Sensoren sender temperatur 26 000 i milligrader Celsius. Lad API og dashboard bruge samme måling, og vælg, hvor værdien omregnes én gang. Hvad skal dashboardtesten forvente?",
                "26 000 milligrader giver 26 °C. Hvis API’et foretager omregningen og tydeligt angiver °C, skal dashboardet vise 26 °C uden at dividere igen.")
            {
                TeachingSource = new("H3 · Fra Arduino til dashboard", "/courses/template/h3", false, false)
            }
        },
        new("kamaeleonen-raymond-vatson-regression", ["chameleon", "raymond", "vatson"], "Hvor tungt vejer den store fejl?",
            "Vatson lægger de målte afstande frem. Kamæleonen sammenligner dem med tre forudsigelser, og Raymond regner både med afstanden og med afstanden i anden for at se, hvor meget den store fejl fylder.",
            "Vatson og Kamæleonen sammenligner tre forudsigelser med målingerne. Raymond peger på den røde række, hvor afstanden mellem de to mærker er størst.")
        {
            Exercise = new("ML-VUF · Dag 5 · Regression og loss",
                "De absolutte fejl er 1, 2 og 7 cm. Beregn MAE og MSE. Hvilken af de tre observationer bidrager mest til MSE, og med hvilket kvadrat?",
                "MAE er 10/3 cm, cirka 3,33 cm. MSE er 18 cm². Fejlen på 7 cm bidrager med 49 cm² inden gennemsnittet beregnes.")
            {
                TeachingSource = new("ML-VUF · Dag 5 om regression", "https://ml.mercantec.tech/vuf/#/dag/5", true, false)
            }
        },
        new("kamaeleonen-raymond-lupp-klynger", ["chameleon", "raymond", "lupp"], "Tre klynger på LUPPs spillebord",
            "LUPP lægger brikkerne efter to egenskaber. Raymond tegner dem som punkter, og Kamæleonen afprøver, hvordan punkterne kan samles i klynger ud fra deres placering.",
            "LUPPs brikker ligger i tre adskilte klynger på papiret. Kamæleonen og Raymond undersøger grupperingen ud fra brikkernes placering.")
        {
            Exercise = new("ML-VUF · Dag 7 · Clustering",
                "Tegn punkterne (1,1), (1,2), (5,5), (5,6), (9,1) og (9,2). Prøv k = 3, og markér de tre grupper, du forventer. Hvad kan et gruppenummer fortælle om brikkernes type?",
                "De oplagte grupper er de tre punktpar omkring x = 1, x = 5 og x = 9. Gruppenummeret identificerer en klynge. Brikkernes virkelige type kræver en særskilt undersøgelse.")
            {
                TeachingSource = new("ML-VUF · Dag 7 om modelkvalitet og clustering", "https://ml.mercantec.tech/vuf/#/dag/7", true, false)
            }
        },
        new("lupp-gitmejsen-raev-resource-loop", ["lupp", "gitmejsen", "fox"], "Ét boost til hele runden",
            "LUPP giver Rune Run-figuren én opladning. Ræv prøver at bruge den forskellige steder på samme bane. Gitmejsen gemmer begge varianter med resultatet fra hver spiltest.",
            "Ræv lægger en mønt ved prøvens indgang, mens LUPP følger ressourcekredsløbet. Gitmejsen gemmer kortet med den afprøvede version.")
        {
            Exercise = new("Game Design · Rune Run · Ressourcekredsløb",
                "Du har ét boost ved starten af hver runde. Prøv at bruge det tidligt i det første forsøg og sent på samme bane i det andet. Tegn kredsløbet, og sammenlign tiderne eller de runer, der blev samlet.",
                "Figuren starter med én opladning, bruger den, og får en ny ved næste runde. Sammenlign målingen fra samme bane. Den bedste timing afhænger af, hvad du måler, og hvornår boostet gør en forskel.")
            {
                TeachingSource = new("Rune Run · LUPPs undervisningskort", "/characters/lupp#character-gallery-title", false, false)
            }
        }
    ];

    public static IReadOnlyList<CharacterCollaboration> For(string slug) =>
        All.Where(scene => scene.ParticipantSlugs.Contains(slug)).ToArray();
}

public sealed record CharacterCollaboration(string Id, IReadOnlyList<string> ParticipantSlugs, string Title, string Description, string Alt)
{
    public const string SectionId = "character-collaborations-title";
    public CharacterCollaborationExercise? Exercise { get; init; }
    public string Anchor => $"collaboration-{Id}";
    public string Image => $"/images/characters/collaborations/{Id}.png";
    public string Preview => $"/images/characters/collaborations/{Id}.webp";
}

public sealed record CharacterCollaborationExercise(string Topic, string Task, string ExpectedResult, string? StarterCode = null)
{
    public CharacterLink? TeachingSource { get; init; }
}
