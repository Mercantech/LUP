using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.DailyGames;

internal sealed record DailyWord(string Host, string Answer, string Clue, string Hint, string FinalHint, string Explanation)
{
    internal string Example => DailyWordCatalog.Examples[Answer];
    internal string[] AcceptedAnswers => DailyWordCatalog.Aliases.GetValueOrDefault(Answer) ?? [];
}

internal static class DailyWordCatalog
{
    private static readonly DailyWord[] Words =
    [
        new("lupp", "mekanik", "Hvad kalder vi en regel, der omsætter en spillers handling til noget i spillet?", "Et tryk på hop kan være et eksempel, hvis figuren faktisk får lov til at lette.", "Begynder med M og handler om, hvad spilleren konkret kan gøre.", "En spilmekanik er en regel for, hvad der sker, når spilleren eller spillet gør noget. Et hop kan eksempelvis koste energi."),
        new("lupp", "prototyp", "Hvad bygger en spildesigner for at afprøve idéen, før hele spillet er lavet?", "Den kan bestå af papir, simple figurer eller kun én spilbar bane.", "Begynder med PRO og bruges til at afprøve en antagelse tidligt.", "En prototyp gør det billigt at lære, om en idé virker, før holdet bruger tid på den færdige løsning."),
        new("lupp", "målgruppe", "Hvad kalder vi de mennesker, som et spil er designet til?", "Deres erfaring og situation påvirker valg af styring og sværhedsgrad.", "Anden del af ordet betyder en samling personer.", "Målgruppen hjælper designeren med at vælge regler, tempo og temaer, der passer til de tiltænkte spillere."),
        new("lupp", "feedback", "Hvad giver spillet, når det med lyd eller billeder viser konsekvensen af din handling?", "Det fortæller eksempelvis, om et tryk blev registreret.", "Begynder med F og bruges også, når nogen reagerer på et udkast.", "Feedback gør spillets reaktion synlig for spilleren. En lyd kan eksempelvis bekræfte, at en genstand blev samlet op."),
        new("lupp", "sværhedsgrad", "Hvilket ord beskriver, hvor krævende en udfordring er for spilleren?", "Træning og tidligere erfaring kan ændre, hvordan den opleves.", "Begynder med SV og slutter med et ord for et niveau.", "Sværhedsgrad handler blandt andet om hvor præcist, hurtigt eller forudseende spilleren skal handle for at lykkes."),

        new("vatson", "sensor", "Hvilken komponent måler noget i den fysiske verden til et automationsanlæg?", "Den kan registrere temperatur, afstand eller tryk.", "Begynder med S og giver styringen et input.", "En sensor omsætter en fysisk påvirkning til en værdi eller et signal, som styringen kan læse."),
        new("vatson", "relæ", "Hvad kalder vi en kontakt, der kan styres med et elektrisk signal?", "En lille styrestrøm kan få den til at tænde en anden kreds.", "Ordet har bogstavet Æ til sidst.", "Et relæ bruges blandt andet til at lade ét kredsløb styre en anden belastning, som en lampe eller motor."),
        new("vatson", "kalibrering", "Hvilken proces sammenligner et måleinstrument med en kendt reference?", "Man bruger eksempelvis et kendt lod til at kontrollere en vægt.", "Begynder med KAL og handler om målingens afvigelse.", "Kalibrering fastlægger forskellen mellem et måleinstruments aflæsning og en reference. En efterfølgende justering kan rette afvigelsen."),
        new("vatson", "aktuator", "Hvilken komponent omsætter styringens signal til fysisk bevægelse?", "En motor eller cylinder kan spille denne rolle.", "Begynder med A og virker på omgivelserne efter en kommando.", "En aktuator udfører en fysisk handling efter signal fra et styresystem, for eksempel ved at åbne en ventil."),
        new("vatson", "endestop", "Hvad kan vise en styring, at en bevægelig del er nået helt ud til sin grænse?", "Det kan eksempelvis være en kontakt for lukket eller åben position.", "To orddele beskriver afslutningen af bevægelsen.", "Et endestop registrerer en grænseposition, så styringen kan standse eller ændre en bevægelse."),

        new("gummianden", "fejlsøgning", "Hvad hedder arbejdet med at finde årsagen til et problem i et program?", "Gør problemet gentageligt, og ændr én ting ad gangen.", "Sidste del af ordet betyder, at du leder efter noget.", "Fejlsøgning bruger observationer, hypoteser og små forsøg til at finde årsagen bag et symptom."),
        new("gummianden", "hypotese", "Hvad kalder vi en forklaring, som vi endnu skal afprøve?", "Du kan teste den med et forsøg, som kan vise, om du tog fejl.", "Begynder med H og er mere konkret end en løs idé.", "En hypotese forudsiger noget, vi kan undersøge, for eksempel hvad der sker med et bestemt input."),
        new("gummianden", "stopmærke", "Hvad sætter du i koden for at sætte programmet på pause på en bestemt linje?", "Pausen lader dig se værdierne i variablerne, før koden fortsætter.", "Første del betyder at standse. På engelsk hedder det breakpoint.", "Et stopmærke i en debugger pauser udførelsen på et sted, du har valgt, så du kan undersøge programmets tilstand."),
        new("gummianden", "logfil", "Hvad kan du åbne for at læse beskeder, som et program tidligere har skrevet?", "Den indeholder ofte tidsstempler og fejlbeskeder i rækkefølge.", "Slutter med det almindelige ord for et gemt dokument på en computer.", "En logfil bevarer hændelser over tid. Den kan vise, hvad der skete lige inden en fejl, også efter at programmet er stoppet."),
        new("gummianden", "isolering", "Hvad kalder vi fremgangsmåden, hvor vi fjerner dele af et eksempel for at undersøge den fejlende del alene?", "Med et mindre eksempel er der færre mulige årsager at undersøge.", "Begynder med ISO og kan også bruges om at adskille noget fysisk.", "Isolering af en fejl gør det lettere at se, om problemet skyldes en bestemt funktion, et input eller en afhængighed."),

        new("raymond", "primærnøgle", "Hvad kaldes den nøgle, der entydigt identificerer hver række i en tabel?", "Værdien skal være udfyldt og unik for hver række.", "Første del af ordet betyder den vigtigste.", "En primærnøgle gør det muligt at finde én bestemt række og for andre tabeller at henvise til den."),
        new("raymond", "fremmednøgle", "Hvilken nøgle kobler en række til en eksisterende række i den samme eller en anden tabel?", "En ordre kan bruge den til at pege på den kunde, der placerede ordren.", "Første del betyder noget, der hører til et andet sted.", "En fremmednøgle håndhæver en relation mellem tabeller eller rækker, ved at en udfyldt værdi skal have en gyldig reference."),
        new("raymond", "transaktion", "Hvad kalder vi en gruppe databaseændringer, der kan lykkes eller rulles tilbage samlet?", "Ved en overførsel kan både fratrækningen og tilføjelsen høre til samme gruppe.", "Begynder med TRANS og kan afsluttes med COMMIT eller ROLLBACK.", "En transaktion lader flere databaserettelser optræde som én samlet operation, også når en del af arbejdet fejler."),
        new("raymond", "indeks", "Hvilken struktur kan gøre det hurtigere at finde rækker i en stor databasetabel?", "Den kan sammenlignes med emneregisteret bagerst i en bog.", "Begynder med I og kan kræve ekstra arbejde, når tabellen ændres.", "Et indeks vedligeholder oplysninger om kolonner, så databasen i nogle tilfælde kan finde relevante rækker uden at gennemgå hele tabellen."),
        new("raymond", "forespørgsel", "Hvad kalder vi en anmodning til databasen om at hente eller bearbejde oplysninger?", "I SQL kan den for eksempel begynde med SELECT.", "Ordet indeholder det danske udsagnsord for at stille et spørgsmål.", "En forespørgsel beskriver, hvilke oplysninger en database skal finde eller bearbejde. SQL'en udtrykker kriterierne for arbejdet."),

        new("bilbo", "parameter", "Hvad kalder vi en navngiven plads til en værdi, som en metode modtager?", "Når du kalder metoden, giver du den plads en konkret værdi.", "Begynder med P. Det konkrete input kaldes ofte et argument.", "En parameter er en del af metodens definition. Ved et metodekald får den en værdi fra det tilsvarende argument."),
        new("bilbo", "returværdi", "Hvad hedder det resultat, en funktion sender tilbage til den kode, der kaldte den?", "Den kommer eksempelvis fra en return-sætning.", "Første del betyder tilbage.", "En returværdi gør det muligt for kaldende kode at bruge resultatet af en funktion, eksempelvis ved at gemme det i en variabel."),
        new("bilbo", "undtagelse", "Hvad kaldes den hændelse, der afbryder et normalt programforløb, når noget særligt går galt?", "I C# kan en try/catch forsøge at håndtere den.", "Begynder med UND. Det engelske ord er exception.", "En undtagelse kan signalere en fejl til den kode, der kan håndtere den, uden at hver normal returværdi skal indeholde et fejlsignal."),
        new("bilbo", "grænseflade", "Hvad kalder vi kontrakten, der beskriver, hvordan en del af systemet kan bruges?", "I C# kan flere forskellige klasser opfylde samme kontrakt.", "På engelsk hedder det interface.", "En grænseflade beskriver de handlinger og typer, som brugeren af en komponent kan regne med."),
        new("bilbo", "variabel", "Hvad kalder vi et navn i et program, som henviser til en værdi?", "Den kan for eksempel indeholde et beregnet antal eller en tekst.", "Begynder med V og lyder som noget, der kan variere.", "En variabel gør det muligt at gemme eller genbruge en værdi under et navn, der forklarer dens rolle."),

        new("chameleon", "træningsdata", "Hvad kaldes de eksempler, der bruges til at lære en model sammenhænge?", "De holdes adskilt fra det materiale, der bruges til den endelige evaluering.", "Første del er noget, du også kan gøre i et motionscenter.", "Træningsdata er de eksempler, modellen bruger til at tilpasse sine parametre."),
        new("chameleon", "overtilpasning", "Hvad hedder det, når en model lærer detaljer fra træningen så tæt, at den virker dårligere på nye eksempler?", "Testresultatet kan være meget dårligere end træningsresultatet.", "Første del af ordet betyder mere end nødvendigt.", "Overtilpasning opstår, når modellen lærer tilfældige træk eller støj i træningsdata, som ikke hjælper på nye data."),
        new("chameleon", "egenskab", "Hvad kalder vi et beskrivende input til en maskinlæringsmodel?", "For en bolig kan det være størrelse, antal værelser eller alder.", "På engelsk kaldes det ofte en feature.", "En egenskab er en oplysning, som modellen kan bruge, når den finder mønstre eller laver en forudsigelse."),
        new("chameleon", "klassifikation", "Hvad hedder opgaven med at placere et input i en kategori?", "Modellen kan for eksempel vælge, om en mail er spam.", "Begynder med K og ligner ordet, vi bruger for en skoleklasse.", "Klassifikation bruges, når resultatet er en kategori, eksempelvis om et billede viser en cykel, bil eller bus."),
        new("chameleon", "regression", "Hvad kaldes en opgave, hvor modellen forudsiger et tal som en boligpris?", "Målet kan være en målt værdi, som kan ligge mellem to andre værdier.", "Begynder med RE. Lineær er en almindelig variant.", "Regression bruges til at forudsige en talværdi, for eksempel temperatur eller pris."),

        new("whale", "container", "Hvad kaldes et isoleret miljø, hvor en tjeneste kører med de filer og biblioteker, den behøver?", "Docker bruger et image som udgangspunkt, når den startes.", "Navnet bruges også om en standardiseret kasse til fragt.", "En container samler en kørende proces og dens miljø med isolation fra andre processer. Den bruger operativsystemets kerne."),
        new("whale", "udrulning", "Hvad kalder vi arbejdet med at sætte en ny version i drift?", "Efter en vellykket build skal versionen startes der, hvor brugerne kan nå den.", "Begynder med UD. På engelsk hedder det deployment.", "En udrulning flytter en version fra build til et køremiljø og kontrollerer, at den nye version virker der."),
        new("whale", "manifest", "Hvad kaldes en deklarativ beskrivelse af, hvilke ressourcer et klyngesystem skal oprette?", "For Kubernetes kan den være en YAML-fil med navn og styringsregler.", "Ordet findes også i skibsfart som betegnelsen for en liste over lasten.", "Et manifest beskriver den ønskede tilstand. Værktøjet læser det og forsøger at bringe miljøet i den beskrevne tilstand."),
        new("whale", "objektlager", "Hvilken type lager læser og skriver filer som objekter med nøgler?", "MinIO tilbyder et S3-kompatibelt eksempel.", "Første del af ordet er en enhed med indhold og metadata.", "Et objektlager bruger nøgler og metadata til at arbejde med indholdet. En tjeneste henter normalt et objekt ved at angive bucket og nøgle."),
        new("whale", "klynge", "Hvad kaldes flere maskiner, der sammen leverer ressourcer til tjenester?", "Arbejdet kan fordeles på flere noder, så én maskine ikke skal håndtere det hele.", "Begynder med K og betyder også en tæt gruppe.", "En klynge grupperer flere noder, som sammen kan køre og administrere arbejdsopgaver."),

        new("bee", "adgangskode", "Hvilken hemmelig værdi kan bruges som en faktor, når du logger ind?", "En passwordmanager kan generere og gemme en unik værdi for hver konto.", "Første del er det, du forsøger at få til kontoen.", "En adgangskode er et eksempel på noget, brugeren ved. En ekstra faktor kan give beskyttelse, hvis koden bliver kendt af andre."),
        new("bee", "kryptering", "Hvad kaldes omdannelsen, der gør en besked ulæselig uden den rette nøgle?", "HTTPS bruger denne teknik til at beskytte trafikken mod aflæsning undervejs.", "Begynder med KRY og adskiller sig fra en kontrolsum ved, at indholdet kan genskabes med nøglen.", "Kryptering beskytter fortroligheden af indhold under overførsel eller lagring, hvis de nødvendige nøgler forbliver beskyttede."),
        new("bee", "netfiskeri", "Hvilket ord beskriver et digitalt svindelforsøg, hvor en besked lokker modtageren til at udlevere noget?", "En afsender kan eksempelvis lade som om, den er en kendt tjeneste.", "Navnet er et billede på at lokke med en krog. På engelsk phishing.", "Netfiskeri forsøger at få en modtager til at handle på en vildledende besked. Tjek anmodningen gennem en kanal, du allerede stoler på."),
        new("bee", "sikkerhedskopi", "Hvad skal du have, hvis du vil gendanne data efter tab eller skade?", "En ekstra kopi skal afprøves ved faktisk at gendanne data fra den.", "Anden del af ordet er en gengivelse af noget eksisterende.", "En sikkerhedskopi gør det muligt at få data tilbage efter tab. Gendannelsestest hjælper med at afsløre, om den er brugbar."),
        new("bee", "beredskab", "Hvad kalder vi forberedelsen, der gør en organisation klar til at håndtere en alvorlig digital hændelse?", "Den omfatter blandt andet ansvar, kontaktveje og afprøvede procedurer.", "Begynder med BERE og handler om at være klar, før noget går galt.", "Et beredskab gør det muligt for personer og systemer at reagere koordineret på en hændelse."),

        new("bat", "spænding", "Hvilken elektrisk størrelse måles i volt?", "Den beskriver forskel i elektrisk potentiale mellem to punkter.", "Ordet begynder med SP og kan også beskrive stemningen i en nervepirrende situation.", "Spænding er forskellen i elektrisk potentiale. Den kan sætte strøm i bevægelse, når kredsløbet er lukket."),
        new("bat", "mikrocontroller", "Hvad kalder vi en lille computerchip med processor, hukommelse og indgange til styring?", "Den kan sidde i en lampe, et måleinstrument eller et lille IoT-kort.", "Første del af ordet betyder meget lille. Sidste del handler om styring.", "En mikrocontroller indeholder typisk de vigtigste computerdele på samme chip og kan styre sensorer eller andre enheder."),
        new("bat", "modstand", "Hvad bremser strømmen i en elektrisk kreds og måles i ohm?", "Komponenten kan eksempelvis hjælpe med at beskytte en lysdiode.", "Ordet kan også betyde at stritte imod.", "En modstand begrænser strømmen for en given spænding. Den kan blandt andet bruges til at få en lysdiode til at arbejde sikkert."),
        new("bat", "måleenhed", "Hvad skal du skrive sammen med et tal for at vise, om sensoren målte volt, grader eller sekunder?", "Tallet 20 kan have helt forskellig betydning afhængigt af denne oplysning.", "Begynder med MÅLE og slutter med ordet for én standardstørrelse.", "En måleenhed gør en tallæsning fortolkelig. Når værdien og enheden følges ad, kan modtageren bruge målingen korrekt."),
        new("bat", "firmware", "Hvad kaldes softwaren, der ligger i en fysisk enhed og styrer dens grundfunktioner?", "Den kan ligge i flashhukommelse og opdateres af producenten.", "Begynder med F og har samme sidste fire bogstaver som software.", "Firmware er software, der er tæt knyttet til en enheds hardware. Den styrer blandt andet, hvordan enheden starter og taler med sine komponenter."),

        new("beaver", "enhedstest", "Hvilken slags test undersøger en lille del af programmet som en enkelt metode?", "Den kan ofte køres uden en hel database og uden en kørende webserver.", "Begynder med det danske ord for én selvstændig del.", "En enhedstest kontrollerer afgrænset opførsel. Hurtige og fokuserede tests gør det lettere at finde årsagen, når noget ændres."),
        new("beaver", "regressionstest", "Hvilken test beskytter mod, at en allerede rettet fejl kommer tilbage?", "Man gemmer en test for det input, der tidligere viste fejlen.", "Sidste del af ordet er TEST.", "En regressionstest kontrollerer en opførsel, der tidligere virkede. Den kan køre automatisk, hver gang koden ændres."),
        new("beaver", "logning", "Hvad kaldes det at registrere hændelser fra et system til senere fejlfinding og kontrol?", "Tidspunkt, hændelsestype og et relevant resultat er ofte nyttige oplysninger.", "Begynder med LOG. Følsomme værdier skal behandles med omtanke.", "Logning gør det muligt at forstå, hvad systemet har oplevet. Velvalgte hændelser giver spor uden at afsløre hemmelige data."),
        new("beaver", "overvågning", "Hvilken aktivitet følger løbende et systems tilstand og melder, når noget ændrer sig?", "Den kan reagere, når svartider stiger eller en tjeneste holder op med at svare.", "Ordet indeholder et udsagnsord for at holde vagt.", "Overvågning bruger målinger og signaler til at opdage problemer, før eller mens de påvirker brugerne."),
        new("beaver", "robusthed", "Hvilken egenskab betyder, at en løsning kan håndtere fejl og uventede forhold på en fornuftig måde?", "En tjeneste kan for eksempel give en tydelig besked, når en afhængighed fejler.", "Begynder med R og er beslægtet med at være modstandsdygtig.", "Robusthed gør et system i stand til at håndtere variation og fejl med forudsigelig adfærd."),

        new("fox", "afgrænsning", "Hvilket ord beskriver beslutningen om, hvilke systemer og funktioner en godkendt sikkerhedstest omfatter?", "Det aftales før testen, så alle ved, hvor og hvornår der må testes.", "Ordet indeholder GRÆNS. På engelsk bruges ofte scope.", "Afgrænsning fastlægger de aftalte systemer, miljøer og aktiviteter, så en test gennemføres inden for den givne tilladelse."),
        new("fox", "sårbarhed", "Hvad kaldes en svaghed, der kan føre til et sikkerhedsproblem?", "Den kan eksempelvis ligge i software, konfiguration eller en arbejdsproces.", "Begynder med SÅR. Dens betydning afhænger blandt andet af konsekvensen.", "En sårbarhed er en svaghed, som vurderes i forhold til, hvordan den kan påvirke et systems sikkerhed."),
        new("fox", "validering", "Hvad kalder vi kontrollen af, om et input følger de regler, vi accepterer?", "En dato, et beløb eller en filtype bør kontrolleres på serveren.", "Begynder med VAL og handler om, hvorvidt noget er gyldigt.", "Validering holder ugyldige værdier ude af den del af koden, der regner med at få et bestemt format eller interval."),
        new("fox", "testmiljø", "Hvad kaldes et separat sted, hvor vi kan afprøve systemet uden at ændre den udgave, brugerne har i drift?", "Her kan fiktive konti og data bruges til kontrollerede forsøg.", "Første del er det, du kommer for at udføre.", "Et testmiljø gør det muligt at gentage kontrollerede forsøg og sammenligne forventet adfærd med det faktiske resultat."),
        new("fox", "tilladelse", "Hvad skal på plads, inden du afprøver et system som led i en sikkerhedstest?", "En klar aftale beskriver, hvem der må teste, og hvilke systemer den dækker.", "Begynder med T og betyder, at nogen har givet dig lov.", "Tilladelse fastlægger, hvem der kan udføre en sikkerhedstest. Den hænger sammen med en aftalt afgrænsning og kontaktperson."),
    ];

    internal static readonly Dictionary<string, string[]> Aliases = new(StringComparer.Ordinal)
    {
        ["mekanik"] = ["spilmekanik"],
        ["prototyp"] = ["prototype", "spilprototype"],
        ["fejlsøgning"] = ["debugging", "fejlfinding"],
        ["stopmærke"] = ["breakpoint", "stoppunkt"],
        ["logfil"] = ["log"],
        ["primærnøgle"] = ["primær nøgle", "primary key"],
        ["fremmednøgle"] = ["fremmed nøgle", "foreign key"],
        ["indeks"] = ["index", "databaseindeks"],
        ["forespørgsel"] = ["sql-forespørgsel", "query"],
        ["returværdi"] = ["retur værdi", "return value"],
        ["undtagelse"] = ["exception"],
        ["grænseflade"] = ["interface"],
        ["træningsdata"] = ["træningssæt", "training data"],
        ["overtilpasning"] = ["overfitting"],
        ["egenskab"] = ["feature", "inputvariabel"],
        ["klassifikation"] = ["klassificering", "classification"],
        ["udrulning"] = ["deployment", "deploy"],
        ["objektlager"] = ["objektlagring", "object storage"],
        ["klynge"] = ["cluster"],
        ["adgangskode"] = ["kodeord", "password"],
        ["netfiskeri"] = ["phishing"],
        ["sikkerhedskopi"] = ["backup", "sikkerhedsbackup"],
        ["mikrocontroller"] = ["microcontroller", "mikrokontroller"],
        ["enhedstest"] = ["unit test", "unittest"],
        ["testmiljø"] = ["test miljø", "test environment"],
        ["afgrænsning"] = ["scope"],
        ["sårbarhed"] = ["vulnerability"]
    };

    internal static readonly Dictionary<string, string> Examples = new(StringComparer.Ordinal)
    {
        ["mekanik"] = "I et platformspil kan spilleren holde hopknappen nede for at nå højere.",
        ["prototyp"] = "Tegn et kort på papir, og brug mønter som spillebrikker, når du tester reglerne.",
        ["målgruppe"] = "Et spil til nye spillere kan introducere én styringsknap ad gangen.",
        ["feedback"] = "En kort lydeffekt fortæller, at spilleren er løbet tør for energi.",
        ["sværhedsgrad"] = "Gør platformene bredere i den første bane, før spilleren skal time lange hop.",
        ["sensor"] = "En temperaturmåler kan få en ventilator til at starte, når værdien overstiger 30 grader.",
        ["relæ"] = "Et styrekort kan aktivere en separat strømkreds til en lampe via et relæ.",
        ["kalibrering"] = "Kontrollér, om vægtens display viser 100 gram, når et kendt lod på 100 gram ligger på den.",
        ["aktuator"] = "En elektrisk motor flytter spjældet, når styringen ændrer signalet.",
        ["endestop"] = "En løfteplatform kan registrere sin øverste stilling og standse motoren.",
        ["fejlsøgning"] = "Find det korteste input, der stadig udløser den forkerte beregning.",
        ["hypotese"] = "Prøv at give funktionen en tom liste for at teste, om det alene udløser fejlen.",
        ["stopmærke"] = "Sæt et stopmærke før løkken, og se hvilken værdi tælleren har, inden den starter.",
        ["logfil"] = "Find klokkeslættet for genstarten i logfilen, og se de tre hændelser lige før den.",
        ["isolering"] = "Afprøv den fejlende funktion med fast input, uden at starte hele appen.",
        ["primærnøgle"] = "Hver kunde får sit eget kundenummer, også når to kunder har samme navn.",
        ["fremmednøgle"] = "KundeId på en ordre henviser til en række i kundetabellen.",
        ["transaktion"] = "En ændring af både lagerbeholdning og ordrestatistik kan gemmes samlet.",
        ["indeks"] = "En tabel med millioner af ordrelinjer kan få et indeks for OrdreId.",
        ["forespørgsel"] = "Vis alle ordrer fra den seneste måned, sorteret efter bestillingsdato.",
        ["parameter"] = "I definitionen Hilsen(navn) er navn den plads, der modtager en værdi.",
        ["returværdi"] = "Når LægSammen(2, 3) kaldes, kan programmet bruge resultatet 5 i næste beregning.",
        ["undtagelse"] = "Et forsøg på at læse en fil, som er væk, kan behandles ved at vise en præcis fejlbesked.",
        ["grænseflade"] = "En e-mailtjeneste og en testudgave kan begge tilbyde den samme Send-metode.",
        ["variabel"] = "Gem spillerens point som et tal ved navn point, så det kan opdateres senere.",
        ["træningsdata"] = "En model kan lære fra 800 mærkede billeder og bedømmes på 200 andre billeder.",
        ["overtilpasning"] = "En model genkender næsten alle træningsbilleder, men rammer ofte forkert på nye billeder.",
        ["egenskab"] = "En boligmodels input kan indeholde antallet af kvadratmeter.",
        ["klassifikation"] = "Et system sætter mærket cykel, bil eller bus på et billede.",
        ["regression"] = "Brug tidligere salg til at forudsige en pris i kroner for en ny bolig.",
        ["container"] = "En webserver og en database kan køre i hver sit isolerede miljø på samme maskine.",
        ["udrulning"] = "Start den nye webversion, og kontrollér derefter, at spiloversigten svarer.",
        ["manifest"] = "En erklæring kan fortælle Kubernetes, at tjenesten skal have tre kopier.",
        ["objektlager"] = "Gem en PNG i en bucket under dens SHA-256, og hent den igen med samme nøgle.",
        ["klynge"] = "En tjeneste kan fortsætte på to andre noder, mens den tredje node vedligeholdes.",
        ["adgangskode"] = "Opret en særskilt, stærk kode til skolekontoen i en passwordmanager.",
        ["kryptering"] = "En sikkerhedskopi kan være krypteret, så filerne kræver en særskilt nøgle for at kunne læses.",
        ["netfiskeri"] = "En mail kræver pludselig, at du logger ind gennem et link. Åbn tjenesten på dens kendte adresse.",
        ["sikkerhedskopi"] = "Gendan jævnligt et udvalg af noter og billeder på en separat testserver.",
        ["beredskab"] = "Holdet øver på forhånd, hvem der kontaktes, og hvem der samler observationer under nedetid.",
        ["spænding"] = "Et voltmeter viser spændingsforskellen mellem batteriets to poler.",
        ["mikrocontroller"] = "En lille chip læser fugtsensoren og tænder en lampe, hvis jorden er tør.",
        ["modstand"] = "En seriemodstand kan begrænse strømmen gennem en lysdiode.",
        ["måleenhed"] = "Skriv 22 °C, så værdien ikke forveksles med 22 °F.",
        ["firmware"] = "Et tastatur kan få nye funktioner fra en opdatering af programmet i sin egen chip.",
        ["enhedstest"] = "En test kontrollerer, at en beregningsmetode returnerer 5 for inputtene 2 og 3.",
        ["regressionstest"] = "Gem en test, der bekræfter, at en tom indkøbskurv fortsat giver totalen 0.",
        ["logning"] = "Gem tidspunkt og statuskode ved fejl uden at gemme brugerens adgangstoken.",
        ["overvågning"] = "En alarm bliver udløst, hvis webstedet fejler på flere sundhedstjek i træk.",
        ["robusthed"] = "Vis en tydelig genforsøgsknap, når en bruger mister netforbindelsen.",
        ["afgrænsning"] = "Skriv i øvelsesplanen, at testholdet kun må bruge den særskilte testserver.",
        ["sårbarhed"] = "En gennemgang kan finde, at data om en konto ses af en rolle, som ikke bør have adgang.",
        ["validering"] = "Afvis en fødselsdato i et format, tjenesten ikke kan tolke entydigt.",
        ["testmiljø"] = "Prøv ændringen med fiktive brugere, før den bliver gjort tilgængelig på lup.dk.",
        ["tilladelse"] = "Få aftalt det konkrete øvelsesmiljø og de tilladte aktiviteter med den ansvarlige på forhånd."
    };

    internal static DailyWord Choose(int dayNumber, byte[] key)
    {
        var hosts = DailyGameHosts.Word;
        var host = hosts[dayNumber % hosts.Count];
        var occurrence = dayNumber / hosts.Count;
        var options = Words.Where(word => word.Host == host).ToArray();
        var cycle = (occurrence / options.Length).ToString(CultureInfo.InvariantCulture);
        return options.OrderBy(word => Convert.ToHexString(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes($"daily-word:{cycle}:{word.Answer}"))), StringComparer.Ordinal)
            .ElementAt(occurrence % options.Length);
    }
}
