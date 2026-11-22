namespace Mercantec.Tech.Web.Characters;

internal static class KarlaMaterials
{
    private const string TeachingPath = "/images/characters/karla/teaching";

    public static IReadOnlyList<CharacterIllustration> Illustrations { get; } =
    [
        Lesson("01-widget-trae", "Widgettræet", "Karla samler et opskriftskort på mobilen ud fra klodser i et forgrenet widgettræ.", "Grundlag",
            "I Flutter er et opskriftskort selv en widget og kan være bygget af mindre widgets som billede, titel og knap.",
            "Lav en RecipeCard med Column, Image og Text, og find dens børn i Flutter Inspector.",
            1536, 1024,
            [
            new("Widgets og brugerflader", "https://docs.flutter.dev/ui", true, false)
            ]),
        Lesson("02-layout-constraints", "Plads i Row og Column", "Karla arrangerer tre lerbrikker vandret og lodret i to rammer på værkstedsbordet.", "Grundlag",
            "En forælder giver en widget rammer for dens størrelse. Row placerer børn vandret, og Column placerer dem lodret.",
            "Læg tre kort i en Row, gør vinduet smallere, og brug Expanded til at undersøge, hvordan kortene deler pladsen.",
            1536, 1024,
            [
            new("Layouts i Flutter", "https://docs.flutter.dev/ui/layout", true, false),
            new("Størrelsesrammer i Flutter", "https://docs.flutter.dev/ui/layout/constraints", true, false)
            ]),
        Lesson("03-layoutbuilder", "Samme app på mobil og tablet", "Karla sammenligner opskriftslisten på en smal mobil med listen og den valgte opskrift side om side på en tablet.", "Grundlag",
            "LayoutBuilder kan bruge den plads, widgetten får fra sin forælder, til at vælge et layout for det samme indhold.",
            "Prøv at vise liste og opskrift side om side, når der er plads til begge, og drej mobilen for at tjekke, at den valgte opskrift følger med.",
            1536, 1024,
            [
            new("Adaptive skærmlayouts", "https://docs.flutter.dev/ui/adaptive-responsive/general", true, false)
            ]),
        Lesson("04-material-komponenter", "Material-komponenter og tema", "Karla sammenligner et opskriftskort, en handlingsknap og en navigationsbjælke med de samme dele i appen.", "Grundlag",
            "Et fælles ThemeData giver Material-komponenterne appens farver og typografi på tværs af skærme.",
            "Byg et opskriftskort med Card og FilledButton, og skift appens farveskema ét sted for at se, hvordan begge dele reagerer.",
            1536, 1024,
            [
            new("Material-komponenter", "https://docs.flutter.dev/ui", true, false),
            new("API-reference for ThemeData", "https://api.flutter.dev/flutter/material/ThemeData-class.html", true, false)
            ]),
        Lesson("05-beroring-og-gestus", "Tryk og stryg", "Karla trykker på et hjerte på mobilen, hvor en kurvet bevægelse viser et stryg hen over kortene.", "Interaktion",
            "Et tryk kan markere en favorit, mens et stryg kan bladre i en liste eller vandret mellem kort.",
            "Lad et tryk på en IconButton vælge en favorit, og kontrollér, at det stadig er let at scrolle forbi knappen.",
            1536, 1024,
            [
            new("Tryk, træk og andre bevægelser", "https://docs.flutter.dev/ui/interactivity/gestures", true, false)
            ]),
        Lesson("06-lokal-tilstand", "Lokal tilstand med setState", "Karla viser det samme opskriftskort med et tomt hjerte før trykket og et udfyldt hjerte bagefter.", "Interaktion",
            "setState fortæller Flutter, at en widgets lokale tilstand er ændret, så brugerfladen kan afspejle den nye favoritstatus.",
            "Gem favoritstatus som en bool i en StatefulWidget, og kontrollér, at to tryk efter hinanden giver henholdsvis et fyldt og et tomt hjerte.",
            1536, 1024,
            [
            new("API-reference for setState", "https://api.flutter.dev/flutter/widgets/State/setState.html", true, false)
            ]),
        Lesson("07-navigation", "Fra liste til opskrift og tilbage", "Karla løfter en detaljeskærm med en tilbagepil, så opskriftslisten bliver synlig nedenunder.", "Interaktion",
            "Med Navigator.push kan du lægge opskriftens detaljeside oven på listen. Navigator.pop lukker detaljen og viser listen igen.",
            "Åbn en opskrift, og gå tilbage for at kontrollere, at du lander ved samme scrollposition i listen.",
            1536, 1024,
            [
            new("Navigation frem og tilbage", "https://docs.flutter.dev/cookbook/navigation/navigation-basics", true, false)
            ]),
        Lesson("08-formularvalidering", "Validering af en opskrift", "Karla peger på en formular, hvor et felt har en tydelig fejlmarkering og en besked under feltet.", "Interaktion",
            "Et TextFormField kan vise en fejlbesked fra en validator, når formen valideres.",
            "Lad gemmeknappen kalde validate(), og prøv både et tomt opskriftsnavn og et udfyldt navn for at kontrollere, hvornår der gemmes.",
            1536, 1024,
            [
            new("Byg en formular med validering", "https://docs.flutter.dev/cookbook/forms/validation", true, false)
            ]),
        Lesson("09-api-og-json", "Hent opskrifter fra et API", "Karla viser en indlæsningsindikator, en hentet opskrift og en fejl med en prøv igen-knap ved siden af et lille servermodul.", "Data",
            "Når en app henter JSON via HTTP, skal den kunne vise, mens den venter, vise de hentede data og hjælpe brugeren videre efter en fejl.",
            "Hent opskrifter fra et test-API, og afprøv en langsom forbindelse, et gyldigt svar og en netværksfejl med mulighed for at prøve igen.",
            1536, 1024,
            [
            new("Hent data fra internettet", "https://docs.flutter.dev/cookbook/networking/fetch-data", true, false)
            ]),
        Lesson("10-tilgaengelighed", "Tilgængelighed på små skærme", "Karla sammenligner to opskriftskort på en tablet. Det største har tydelig tekst, stort trykfelt og et symbol for oplæsning.", "Kvalitet",
            "Skalerbar tekst, tydelige trykfelter og meningsfulde tilgængelighedsnavne hjælper flere med at bruge appen.",
            "Slå større tekst og en skærmlæser til, og kontrollér, at favoritknappen er nem at ramme og får læst sit navn og sin tilstand op.",
            1536, 1024,
            [
            new("Tilgængelighed i Flutter", "https://docs.flutter.dev/ui/accessibility", true, false),
            new("API-reference for Semantics", "https://api.flutter.dev/flutter/widgets/Semantics-class.html", true, false)
            ]),
        Lesson("11-test-af-appen", "Unit-, widget- og integrationstest", "Karla afprøver regnereglen, favoritknappen og det samlede opskriftsflow på tre teststationer.", "Kvalitet",
            "En unit-test dækker eksempelvis portionsberegningen, en widgettest kan trykke på favoritknappen, og en integrationstest kan afprøve det samlede flow frem til den valgte opskrift.",
            "Skriv en test for portionsberegningen, en for favoritknappen og en, der åbner en opskrift og går tilbage i appen.",
            1536, 1024,
            [
            new("Test af Flutter-apps", "https://docs.flutter.dev/testing/overview", true, false)
            ]),
        Lesson("12-build-og-udgivelse", "Gør appen klar til udgivelse", "Karla gør den samme opskriftsapp klar til to telefoner med hver sin teststation og pakke.", "Kvalitet",
            "Den samme Flutter-kode kan bygge en Android-app og en iOS-app med særskilte pakker, signering og udgivelsestrin.",
            "Lav en tjekliste med version, signering og intern test for hver platform, og prøv release-buildet på en rigtig enhed før udgivelse.",
            1536, 1024,
            [
            new("Byg og udgiv til Android", "https://docs.flutter.dev/deployment/android", true, false),
            new("Byg og udgiv til iOS", "https://docs.flutter.dev/deployment/ios", true, false)
            ])
    ];

    private static CharacterIllustration Lesson(string slug, string title, string alt, string stage,
        string summary, string exercise, int width, int height, CharacterLink[] sources) =>
        new(title, $"{TeachingPath}/{slug}.png", alt, width, height)
        {
            Preview = $"{TeachingPath}/{slug}.webp",
            Stage = stage,
            Summary = summary,
            Explanation = [$"Prøv selv. {exercise}"],
            Sources = sources
        };
}
