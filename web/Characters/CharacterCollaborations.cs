namespace Mercantec.Tech.Web.Characters;

internal static class CharacterCollaborations
{
    private static readonly CharacterCollaboration[] Scenes =
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
            "På en togstation med et ur på 02.30 peger Raymond på billetten mærket 00:30 UTC. Vatson holder en strimmel fra loggen ved siden af.")
    ];

    public static IReadOnlyList<CharacterCollaboration> For(string slug) =>
        Scenes.Where(scene => scene.FirstSlug == slug || scene.SecondSlug == slug).ToArray();
}

public sealed record CharacterCollaboration(string Id, string FirstSlug, string SecondSlug, string Title, string Description, string Alt)
{
    public const string SectionId = "character-collaborations-title";
    public string Anchor => $"collaboration-{Id}";
    public string Image => $"/images/characters/collaborations/{Id}.png";
    public string Preview => $"/images/characters/collaborations/{Id}.webp";
    public string PartnerSlug(string slug) => slug == FirstSlug ? SecondSlug : FirstSlug;
}
