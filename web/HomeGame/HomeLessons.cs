namespace Mercantec.Tech.Web.HomeGame;

internal sealed record HomeLesson(string Title, string Aim, string Criterion, string Observation, string Hint);

internal static class HomeLessons
{
    public static readonly IReadOnlyList<HomeLesson> All =
    [
        new("Lys ved bordet", "Din avatar vil læse. Placér lampen, og tænd den med et program.",
            "Hold læsefeltet mellem 300 og 600 lux i mørke.", "Find læsefeltet foran skrivebordet og se lux i Målinger.",
            "Brug lampens kodealias i en tænd-kommando. Ved 40 lux i rummet skal lampen normalt stå højst to felter fra læsefeltet."),
        new("Placering betyder noget", "Flyt skrivebordet til det markerede sted. Flyt lampen, hvis lyset er for svagt.",
            "Placér bordet ved (2, 6) uden rotation, og hold læsefeltet mellem 300 og 600 lux i mørke.",
            "Rækkevidden viser lampens bidrag. Lysstyrken falder med afstanden.",
            "Genbrug programmet, der tænder lampen. Flyt lampen tæt på det nye læsefelt og prøv igen."),
        new("Dagslysstyring", "Montér dagslyssensoren. Brug målingen til at tænde lampen, når det bliver mørkt.",
            "Hold 300–600 lux under læsning i mørke. Sluk lampen i dagslys.",
            "Dagslyssensoren måler lyset udendørs. Se de to målinger under Afprøv.",
            "Sammenlign dagslyset med 300 lux. Gentag målingen efter en kort ventetid i simulationen."),
        new("Tilstedeværelse", "Montér tilstedeværelsessensoren. Tænd kun lys, når din avatar er hjemme og har brug for det.",
            "Hold læsefeltet lyst i mørke. Sluk i dagslys og mens hjemmet er tomt.",
            "Sensoren registrerer også en avatar, der sidder stille. Brug Rækkevidde til at undersøge læsefeltet.",
            "Kombinér målingerne af dagslys og tilstedeværelse med en logisk og-betingelse."),
        new("Manuel betjening", "Montér kontakten. Lad din avatar slå det automatiske lys til og fra.",
            "Hold lampen slukket, når autolys er fra. Når autolys er til, gælder reglerne om dagslys og tilstedeværelse.",
            "Skift kontakten under en fri afprøvning og følg enhedstilstanden.",
            "Inddrag kontakten som den tredje betingelse. Uden dens tilladelse skal lampen være slukket."),
        new("Et sammenhængende hjem", "Afprøv hele dit hjem. Beskriv en forbedring, en lærer kan læse.",
            "Bestå alle situationerne med samme indretning og program.",
            "Sammenlign lys, kommandoer og energi for mørke, dagslys, fravær og hjemkomst.",
            "Beskriv, hvad sensorerne målte, hvorfor lampen reagerede, og hvordan du afprøvede din løsning.")
    ];

    public static string Label(string language) => language switch
    {
        "python" => "Python",
        "csharp" => "C#",
        "arduino" => "Arduino-C++",
        _ => language
    };

    public static string Starter(string language) => language switch
    {
        "csharp" => "using HomeSdk;\n\n// Skriv din første kommando her.\n",
        "arduino" => "#include <VirtualHome.h>\n\nvoid setup() {\n    home.begin();\n    // Skriv din første kommando her.\n}\n\nvoid loop() {\n}\n",
        _ => "import home\n\n# Skriv din første kommando her.\n"
    };

    public static string Skeleton(string language, int task) => (language, task) switch
    {
        ("csharp", <= 2) => "using HomeSdk;\n\n// Home.Set(\"desk_lamp\", ...);\n",
        ("csharp", _) => "using HomeSdk;\n\nwhile (true)\n{\n    var daylight = Home.Read(\"daylight\");\n    // Beregn, om lampen skal tændes.\n    Home.Wait(0.2);\n}\n",
        ("arduino", <= 2) => "#include <VirtualHome.h>\n\nvoid setup() {\n    home.begin();\n    // home.set(\"desk_lamp\", ...);\n}\n\nvoid loop() {\n}\n",
        ("arduino", _) => "#include <VirtualHome.h>\n\nvoid setup() { home.begin(); }\n\nvoid loop() {\n    auto daylight = home.read(\"daylight\");\n    // Beregn, om lampen skal tændes.\n    delay(200);\n}\n",
        (_, <= 2) => "import home\n\n# home.set(\"desk_lamp\", ...)\n",
        _ => "import home\n\nwhile True:\n    daylight = home.read(\"daylight\")\n    # Beregn, om lampen skal tændes.\n    home.wait(0.2)\n"
    };

    public static string SetSyntax(string language) => language switch
    {
        "csharp" => "Home.Set(\"desk_lamp\", true);",
        "arduino" => "home.set(\"desk_lamp\", true);",
        _ => "home.set(\"desk_lamp\", True)"
    };

    public static string ReadSyntax(string language, string alias, bool boolean) => language switch
    {
        "csharp" => $"Home.{(boolean ? "ReadBool" : "Read")}(\"{alias}\")",
        "arduino" => $"home.{(boolean ? "readBool" : "read")}(\"{alias}\")",
        _ => $"home.read(\"{alias}\")"
    };

    public static string WaitSyntax(string language) => language switch
    {
        "csharp" => "Home.Wait(0.2);",
        "arduino" => "delay(200);",
        _ => "home.wait(0.2)"
    };

    public static string TimeSyntax(string language) => language switch
    {
        "csharp" => "Home.NowMs()",
        "arduino" => "millis()",
        _ => "home.now_ms()"
    };

    public static string LogSyntax(string language) => language switch
    {
        "csharp" => "Home.Log(vaerdi);",
        "arduino" => "Serial.println(vaerdi);",
        _ => "home.log(vaerdi)"
    };
}
