namespace Mercantec.Tech.Web.Characters;

internal static class IbenMaterials
{
    public static IReadOnlyList<CharacterIllustration> Illustrations { get; } =
    [
        Lesson("01-komponenter", "Saml siden af Razor-komponenter", "Iben sætter et lanternekort ind i en model af en webside.", "Komponenter",
            "En .razor-fil definerer en komponent. Når du bruger dens navn som et tag to gange, får du to forekomster af samme komponent.",
            "Opret LanterneKort.razor og Galleri.razor i samme mappe. Ret overskriften i kortet, og se ændringen slå igennem begge steder.",
            """
            @* LanterneKort.razor *@
            <article>
                <h3>Ravlanternen</h3>
                <p>Et varmt lys til aftenværkstedet.</p>
            </article>

            @* Galleri.razor *@
            @page "/galleri"
            <LanterneKort />
            <LanterneKort />
            """,
            "Razor-komponenter", "components/"),
        Lesson("02-tovejsbinding", "Skru op og ned med @bind", "Iben viser sammenhængen mellem en skyder, en måler og lyset i en lanterne.", "Binding",
            "Med @bind læser skyderen sin værdi fra et C#-felt. Når browseren sender den valgte hændelse, skriver Blazor værdien tilbage til det samme felt.",
            "Brug en interaktiv komponent. Træk skyderen til 80, og tryk så på Dæmp. Forudsig, hvad der sker med skyderen, tallet og måleren.",
            """
            <label>
                Lysstyrke
                <input type="range" min="0" max="100"
                       @bind="lysstyrke" @bind:event="oninput" />
            </label>
            <meter min="0" max="100" value="@lysstyrke">@lysstyrke</meter>
            <output>@lysstyrke %</output>
            <button @onclick="Daemp">Dæmp</button>

            @code {
                private int lysstyrke = 40;
                private void Daemp() => lysstyrke = 20;
            }
            """,
            "Databinding i Blazor", "components/data-binding"),
        Lesson("03-oninput", "Opdater undervejs med oninput", "Iben sammenligner to tekstfelter og deres forhåndsvisninger.", "Binding",
            "På et almindeligt tekstfelt bruger @bind som udgangspunkt onchange. Med @bind:event=\"oninput\" får komponenten også besked under indtastningen.",
            "Skriv Lys i hvert felt i en interaktiv komponent. Kig på teksten efter det første bogstav, og tryk derefter Tab. Hvornår ændrer de to forhåndsvisninger sig?",
            """
            <label>Løbende <input @bind="live" @bind:event="oninput" /></label>
            <p>Forhåndsvisning: @live</p>

            <label>Ved ændring <input @bind="bekraeftet" /></label>
            <p>Forhåndsvisning: @bekraeftet</p>

            @code {
                private string live = "";
                private string bekraeftet = "";
            }
            """,
            "Vælg bindingshændelse", "components/data-binding"),
        Lesson("04-klik-og-tilstand", "Tænd lanternen med @onclick", "Iben trykker på en knap og viser en slukket og en tændt lanterne.", "Interaktion",
            "En klikhændelse kalder en C#-metode. Når metoden har ændret den lokale tilstand, gengiver Blazor komponenten med den nye værdi.",
            "Kør koden i en interaktiv komponent. Tryk to gange og følg både knappens tekst og statuslinjen.",
            """
            <button @onclick="SkiftLys">@(taendt ? "Sluk" : "Tænd")</button>
            <p aria-live="polite">Lanternen er @(taendt ? "tændt" : "slukket").</p>

            @code {
                private bool taendt;
                private void SkiftLys() => taendt = !taendt;
            }
            """,
            "Håndtering af hændelser", "components/event-handling"),
        Lesson("05-parametre", "Giv hvert kort sin egen farve", "Iben sammenligner tre lanterner i forskellige farver.", "Komponenter",
            "Med [Parameter] kan en komponent modtage værdier fra sin forælder. Hver forekomst af kortet bruger sit eget navn og sin egen farve.",
            "Gem kortet og paletten som to komponenter i samme mappe. Tilføj Safir som et fjerde kort alene ved at sætte to parametre.",
            """
            @* FarveKort.razor *@
            <article style="border-left: 0.4rem solid @Farve">
                <h3>@Navn</h3>
            </article>

            @code {
                [Parameter] public string Navn { get; set; } = "";
                [Parameter] public string Farve { get; set; } = "gray";
            }

            @* Palet.razor *@
            <FarveKort Navn="Rav" Farve="#d38712" />
            <FarveKort Navn="Ametyst" Farve="#7857ad" />
            <FarveKort Navn="Søglas" Farve="#25857a" />
            """,
            "Komponentparametre", "components/"),
        Lesson("06-eventcallback", "Send et valg tilbage med EventCallback", "Iben sender et valg fra et lille lanternekort til panelet ovenover.", "Interaktion",
            "Barnet sender lanternens navn gennem EventCallback<string>. Forælderen ejer den valgte værdi og viser den i sin overskrift.",
            "Brug de to filer i samme mappe under en interaktiv forælder. Tilføj en VaelgKnap for Søglas, og kontrollér, at begge knapper opdaterer én fælles overskrift.",
            """
            @* VaelgKnap.razor *@
            <button @onclick="SendValg">Vælg @Navn</button>

            @code {
                [Parameter] public string Navn { get; set; } = "";
                [Parameter] public EventCallback<string> Valgt { get; set; }
                private Task SendValg() => Valgt.InvokeAsync(Navn);
            }

            @* ValgPanel.razor *@
            <h3>Valgt lanterne: @valgt</h3>
            <VaelgKnap Navn="Rav" Valgt="ModtagValg" />

            @code {
                private string valgt = "ingen";
                private void ModtagValg(string navn) => valgt = navn;
            }
            """,
            "EventCallback mellem komponenter", "components/event-handling"),
        Lesson("07-lister-og-key", "Flyt det rigtige element med @key", "Iben flytter et lanternekort med sit eget kendemærke i en række.", "Komponenter",
            "@key fortæller Blazor, hvilken række der hører til hvilken lanterne. Nøglen skal være unik blandt søskende og stabil, når listen sorteres.",
            "Skriv en huskeseddel ved Rav i en interaktiv komponent, og vend listen. Fjern derefter @key, genindlæs og gentag forsøget. Hvilken lanterne følges sedlen med?",
            """
            <button @onclick="Vend">Vend listen</button>
            @foreach (var lampe in lamper)
            {
                <label @key="lampe.Id">
                    @lampe.Navn <input placeholder="Huskeseddel" />
                </label>
            }

            @code {
                private record Lampe(int Id, string Navn);
                private readonly List<Lampe> lamper =
                    [new(10, "Rav"), new(20, "Ametyst"), new(30, "Søglas")];
                private void Vend() => lamper.Reverse();
            }
            """,
            "Bevar elementers identitet med @key", "components/element-component-model-relationships"),
        Lesson("08-formularvalidering", "Valider navnet med EditForm", "Iben viser en formular med et tomt navnefelt og en godkendt indtastning.", "Interaktion",
            "DataAnnotationsValidator knytter modellens [Required] til formularen. ValidationMessage viser fejlen ved navnefeltet, og OnValidSubmit kører, når modellen er gyldig.",
            "Brug en interaktiv komponent. Tryk Godkend med et tomt felt, læs beskeden, skriv Lys, og godkend igen.",
            """
            @using System.ComponentModel.DataAnnotations

            <EditForm Model="model" OnValidSubmit="Godkend">
                <DataAnnotationsValidator />
                <label>Navn <InputText @bind-Value="model.Navn" /></label>
                <ValidationMessage For="() => model.Navn" />
                <button type="submit">Godkend</button>
            </EditForm>
            <p aria-live="polite">@status</p>

            @code {
                private readonly Lanterne model = new();
                private string status = "";

                private void Godkend() => status = $"Godkendt: {model.Navn}";

                private sealed class Lanterne
                {
                    [Required(ErrorMessage = "Giv lanternen et navn.")]
                    public string Navn { get; set; } = "";
                }
            }
            """,
            "Validering af Blazor-formularer", "forms/validation"),
        Lesson("09-service", "Del et katalog med dependency injection", "Iben viser to skærme med samme udvalg af lanterner fra en fælles reol.", "Data",
            "Registrér kataloget i appens servicecontainer. Med @inject kan flere komponenter læse de samme katalogdata. Her deles ét skrivebeskyttet katalog på tværs af brugerne i serverprocessen.",
            "Opret klassen i samme namespace som dine komponenter. Registrér den før builder.Build() i serverprojektets Program.cs, og vis kataloget i to komponenter.",
            """
            // LanterneKatalog.cs
            public sealed class LanterneKatalog
            {
                public IReadOnlyList<string> Navne { get; } =
                    Array.AsReadOnly(new[] { "Rav", "Ametyst", "Søglas" });
            }

            // Program.cs
            builder.Services.AddSingleton<LanterneKatalog>();

            @* Katalog.razor *@
            @inject LanterneKatalog KatalogData
            <ul>
                @foreach (var navn in KatalogData.Navne)
                {
                    <li>@navn</li>
                }
            </ul>
            """,
            "Dependency injection i Blazor", "fundamentals/dependency-injection"),
        Lesson("10-asynkrone-data", "Vis ventetid, svar og fejl", "Iben viser tre skærme med en indlæsningsindikator, lanterner og en prøv igen-knap.", "Data",
            "Mens en asynkron indlæsning venter, kan komponenten vise en status. Når opgaven afsluttes, skal den vise enten resultatet eller en fejl med mulighed for at prøve igen.",
            "Brug en interaktiv komponent. Vælg Simulér fejl og hent kataloget. Fjern markeringen og prøv igen. De tre tilstande kan afprøves lokalt uden et API.",
            """
            <label><input type="checkbox" @bind="simulerFejl" /> Simulér fejl</label>
            <button disabled="@henter" @onclick="Hent">
                @(fejl ? "Prøv igen" : "Hent katalog")
            </button>
            <p role="status">@besked</p>

            @code {
                private bool simulerFejl, henter, fejl;
                private string besked = "Klar";

                private async Task Hent()
                {
                    var svarMedFejl = simulerFejl;
                    henter = true;
                    besked = "Henter lanterner …";
                    await Task.Delay(700); // Simuleret svartid fra kataloget
                    fejl = svarMedFejl;
                    besked = fejl ? "Kataloget svarede ikke." : "Rav · Ametyst · Søglas";
                    henter = false;
                }
            }
            """,
            "Asynkron indlæsning og gyldig visningstilstand", "components/lifecycle"),
        Lesson("11-routing", "Åbn lanternen fra dens URL", "Iben følger et lysspor fra en liste med lanterner til detaljevisningen for én af dem.", "Navigation",
            "Ruteparameteren {Id:int} gør id'et i URL'en til et heltal på komponenten. Det samme detaljebillede kan derfor bruges til mange lanterner og åbnes via et direkte link.",
            "Opret detaljesiden og åbn /lanterner/17. Følg linket til nummer 24 og brug browserens tilbageknap. Hvilken URL skal du sende en klassekammerat for at vise nummer 17?",
            """
            @page "/lanterner/{Id:int}"

            <h2>Lanterne @Id</h2>
            <a href="lanterner/17">Vis lanterne 17</a>
            <a href="lanterner/24">Vis lanterne 24</a>

            @code {
                [Parameter] public int Id { get; set; }
            }
            """,
            "Ruteparametre og typekrav", "fundamentals/routing"),
        Lesson("12-rendering", "Fra statisk HTML til InteractiveServer", "Iben trykker på en kontakt i et interaktivt lanternekort ved siden af en statisk visning.", "Rendering",
            "Statisk servergengivelse leverer komponentens HTML. Med InteractiveServer håndteres knappens Blazor-hændelser på serveren via en forbindelse til browseren.",
            "Brug en Blazor Web App med Interactive Server aktiveret og interaktivitet pr. komponent. Prøv siden med og uden @rendermode. Hvilken del af siden kan ses før det første klik i begge forsøg?",
            """
            @page "/lysproeve"
            @rendermode InteractiveServer

            <button @onclick="Taend">Tænd lanternen</button>
            <p role="status">@(taendt ? "Lys!" : "Mørke")</p>

            @code {
                private bool taendt;
                private void Taend() => taendt = true;
            }
            """,
            "Rendertilstande i Blazor Web Apps", "components/render-modes")
    ];

    private static CharacterIllustration Lesson(string slug, string title, string alt, string stage,
        string summary, string exercise, string code, string sourceLabel, string docsPath)
    {
        const string teachingPath = "/images/characters/iben/teaching";
        const string docsRoot = "https://learn.microsoft.com/en-us/aspnet/core/blazor/";
        return new(title, $"{teachingPath}/{slug}.png", alt, 1536, 1024)
        {
            Preview = $"{teachingPath}/{slug}.webp",
            Stage = stage,
            Summary = summary,
            Explanation = [$"Prøv selv. {exercise}"],
            Code = code,
            Sources = [new(sourceLabel, $"{docsRoot}{docsPath}?view=aspnetcore-10.0", true, false)]
        };
    }
}
