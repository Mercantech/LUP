namespace Mercantec.Tech.Web.Characters;

internal static class CharacterHomes
{
    private static readonly IReadOnlyDictionary<string, CharacterHome> Homes = new Dictionary<string, CharacterHome>
    {
        ["lupp"] = Create("lupp", "Papirhuset",
            "LUPP bor i et hus af foldet karton med en blommefarvet dør og et rundt, turkis vindue ud mod haven. Når der er ro på, folder LUPP nye spilbaner ud på sofabordet og prøver dem med sin gule blyant inden for rækkevidde.",
            "LUPP sidder med sin gule blyant foran Papirhusets blommefarvede dør, foldede koraltag og runde turkise vindue.",
            "LUPP sidder på en pude i Papirhuset. En papbane ligger slået op på bordet foran det runde turkise vindue."),
        ["vatson"] = Create("vatson", "Prototypehuset",
            "Vatson bor i et lille turkis hus med en rund glasport og en analog måler ved indgangen. Ved arbejdsbænken afprøver han sine prototyper og har ringbindet inden for rækkevidde.",
            "Vatson svæver foran Prototypehusets rundede glasport. Ved døren sidder en rund analog måler.",
            "Vatson svæver ved arbejdsbænken med sit turkise ringbind. En lille prøvelampe lyser ved den rundede glasport."),
        ["gummianden"] = Create("gummianden", "Emaljehuset",
            "Gummianden bor i et lille emaljehus i en gård med fliser. Indenfor har anden en blød lytteplads med te og notesbøger til fejlfinding.",
            "Gummianden står i den flisebelagte gård foran Emaljehusets ovale dør med orange håndtag.",
            "Gummianden sidder med en kop te i en blød lænestol, omgivet af turkise fliser og notesbøger."),
        ["raymond"] = Create("raymond", "Katalogvillaen",
            "Raymond bor i en rødstensvilla med blå dør og et rundt vindue i rav, creme og blå. Han fører kartotek over sine bøger i stuen og bruger presset rosmarin fra haven som bogmærke.",
            "Raymond åbner den blå dør til sin rødstensvilla med en bog under armen. Det runde vindue er delt i tre farvede felter.",
            "Raymond sætter et kort i egetræskartoteket under det runde, stribede vindue. En kop te og en rosmarinkvist ligger ved lænestolen."),
        ["bilbo"] = Create("bilbo", "Brombærbo",
            "Bilbo bor under en græsklædt bakke bag en rund grøn dør, omgivet af brombærbuske. Han koger syltetøj ved ildstedet og sætter glassene på række i spisekammeret.",
            "Bilbo står barfodet med et glas brombærsyltetøj foran Brombærbos runde grønne dør og buede jordtag.",
            "Bilbo fylder et syltetøjsglas ved ildstedet, tæt på den grønne runddør og hylderne med brombærsyltetøj."),
        ["chameleon"] = Create("chameleon", "Cremonaværkstedet",
            "Kamæleonen bor i et honningfarvet hus, hvor stueetagen er violinværksted. Når limen tørrer, tager han banjoen ned fra væggen og spiller.",
            "Kamæleonen står foran sit honningfarvede hus med en ufærdig violin. Huset har en grøn buedør og et rundt vindue med træroset.",
            "Kamæleonen arbejder på en violin foran det runde rosetvindue i sit hjem. Banjoen hænger på væggen ved den grønne buedør."),
        ["whale"] = Create("whale", "Lanternehuset",
            "Docker bor i havnefogedens gamle flydehus, som ligger fortøjet ved containerkajen. Han passer den gule lanterne hver aften og fører tidevandsbog ved koøjet.",
            "Docker vinker fra vandet ved rampen til sit skiferblå flydehus. En gul lanterne lyser over døren ved det store koøje.",
            "Docker sidder under flydehusets store messingkoøje med et modelcontainerskib. Et tidevandsur og hans knyttede reb hænger i stuen."),
        ["bee"] = Create("bee", "Ravkuben",
            "Cyberbien bor i Ravkuben, et sekskantet træhus med en rund ravgul dør på en blomstrende bakke. Indenfor har han sit kontrolhjørne og en honninghylde, hvor han prøver at brygge den samme kop te på så mange måder som muligt.",
            "Cyberbien står på blomsterbakken foran sit sekskantede træhus med en rund, ravgul dør og en række små sekskantede vinduer.",
            "Cyberbien brygger te ved sin sekskantede honninghylde. Den runde, ravgule dør og de små sekskantede vinduer ses bag ham."),
        ["bat"] = Create("bat", "Ekkoloftet",
            "Næsebladet bor på loftet over et gammelt teater, hvor den ravfarvede rude lyser ud over tagene. Her lytter han til sine optagelser og gemmer sin yndlingsteske.",
            "Næsebladet står ved messingklokken på trappen til Ekkoloftet over det gamle teater. Den ravfarvede halvroset lyser i gavlen.",
            "Næsebladet står ved grammofonen på Ekkoloftet. Vokscylindre og en teske ligger under den ravfarvede halvroset."),
        ["gitmejsen"] = Create("gitmejsen", "Grenhuset",
            "Gitmejsen bor i et lille træhus i en stor fyrs grenkløft. Hun gemmer frø i sprækkerne og bruger dem til at holde styr på sine forsøg.",
            "Gitmejsen med blåt tørklæde sidder foran Grenhuset, et træhus med blå skodder oppe i en stor fyr.",
            "Gitmejsen lægger et frø i sit arkiv under den blåskoddede rude. En tavle med kogler og et lille hvilested står bag hende."),
        ["beaver"] = Create("beaver", "Blåbækhuset",
            "Bæver bor i Blåbækhuset, et træhus ved bækken med blå skodder og et vandhjul tæt på den lave dæmning. På inspektionsvæggen hænger hans reservedele og målinger, og ved siden af står lænestolen til tepausen.",
            "Bæver står med sin tablet foran et træhus med blå skodder. Ved huset drejer et vandhjul i bækken tæt på en lav dæmning.",
            "Bæver sidder med te og sin tablet i lænestolen. På inspektionsvæggen hænger et logpanel og håndskårne træstykker under en lampe."),
        ["fox"] = Create("fox", "Genvejshuset",
            "Ræv bor i Genvejshuset, et rødstenshus med mostag og en lille skovsti, der ender i en buet sidedør. Indenfor kan reolen drejes til side og give adgang til en læsekrog med bøger og skitser.",
            "Ræv står med sin notesbog foran rødstenshuset med mostag. En smal skovsti fører gennem en lille stenbue til sidedøren.",
            "Ræv drejer reolen til side og viser sin læsekrog med bøger, skitser og en lampe. Ved siden af ses den buede sidedør."),
    };

    public static CharacterHome? For(string slug) => Homes.GetValueOrDefault(slug);

    private static CharacterHome Create(string slug, string name, string description, string outsideAlt, string insideAlt)
    {
        var path = $"/images/characters/{slug}/home";
        return new(name, description,
        [
            new("Foran hjemmet", $"{path}/outside.png", outsideAlt, 768, 512) { Preview = $"{path}/outside.webp" },
            new("Indenfor", $"{path}/inside.png", insideAlt, 768, 512) { Preview = $"{path}/inside.webp" }
        ]);
    }
}

public sealed record CharacterHome(string Name, string Description, IReadOnlyList<CharacterIllustration> Views)
{
    public const string SectionId = "character-home-title";
}
