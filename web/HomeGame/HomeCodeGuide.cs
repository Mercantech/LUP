namespace Mercantec.Tech.Web.HomeGame;

internal static class HomeCodeGuide
{
    public static string Label(string language) => language switch
    {
        "python" => "Python",
        "csharp" => "C#",
        "arduino" => "Arduino-C++",
        _ => language
    };

    public static string Lamp(string language, string alias) => language switch
    {
        "csharp" => $"using HomeSdk;\n\n{SetSyntax(language, alias)}\n",
        "arduino" => $"#include <VirtualHome.h>\n\nvoid setup() {{\n    home.begin();\n    {SetSyntax(language, alias)}\n}}\n\nvoid loop() {{}}\n",
        _ => $"import home\n\n{SetSyntax(language, alias)}\n"
    };

    public static string Daylight(string language, string lamp, string sensor) => language switch
    {
        "csharp" => $"using HomeSdk;\n\nwhile (true)\n{{\n    Home.Set(\"{lamp}\", Home.Read(\"{sensor}\") < 300);\n    Home.Wait(0.2);\n}}\n",
        "arduino" => $"#include <VirtualHome.h>\n\nvoid setup() {{ home.begin(); }}\n\nvoid loop() {{\n    home.set(\"{lamp}\", home.read(\"{sensor}\") < 300);\n    delay(200);\n}}\n",
        _ => $"import home\n\nwhile True:\n    home.set(\"{lamp}\", home.read(\"{sensor}\") < 300)\n    home.wait(0.2)\n"
    };

    public static string SetSyntax(string language, string alias) => language switch
    {
        "csharp" => $"Home.Set(\"{alias}\", true);",
        "arduino" => $"home.set(\"{alias}\", true);",
        _ => $"home.set(\"{alias}\", True)"
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
