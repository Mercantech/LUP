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

    public static string Lamp(string language, string alias) => Output(language, alias, true);

    public static string Output(string language, string alias, bool enabled) => language switch
    {
        "csharp" => $"using HomeSdk;\n\n{SetSyntax(language, alias, enabled)}\n",
        "arduino" => $"#include <VirtualHome.h>\n\nvoid setup() {{\n    home.begin();\n    {SetSyntax(language, alias, enabled)}\n}}\n\nvoid loop() {{}}\n",
        _ => $"import home\n\n{SetSyntax(language, alias, enabled)}\n"
    };

    public static string Daylight(string language, string lamp, string sensor) => language switch
    {
        "csharp" => $"using HomeSdk;\n\nwhile (true)\n{{\n    Home.Set(\"{lamp}\", Home.Read(\"{sensor}\") < 300);\n    Home.Wait(0.2);\n}}\n",
        "arduino" => $"#include <VirtualHome.h>\n\nvoid setup() {{ home.begin(); }}\n\nvoid loop() {{\n    home.set(\"{lamp}\", home.read(\"{sensor}\") < 300);\n    delay(200);\n}}\n",
        _ => $"import home\n\nwhile True:\n    home.set(\"{lamp}\", home.read(\"{sensor}\") < 300)\n    home.wait(0.2)\n"
    };

    public static string ReadClock(string language) => language switch
    {
        "csharp" => "using HomeSdk;\n\nHome.Log(Home.NowMs());\n",
        "arduino" => "#include <VirtualHome.h>\n\nvoid setup() {\n    home.begin();\n    Serial.println(millis());\n}\n\nvoid loop() {}\n",
        _ => "import home\n\nhome.log(home.now_ms())\n"
    };

    public static string SensorExample(string language, string alias, bool boolean)
    {
        var read = ReadSyntax(language, alias, boolean);
        return language switch
        {
            "csharp" => $"using HomeSdk;\n\nHome.Log({read});\n",
            "arduino" => $"#include <VirtualHome.h>\n\nvoid setup() {{ home.begin(); }}\n\nvoid loop() {{\n    Serial.println({read});\n    delay(200);\n}}\n",
            _ => $"import home\n\nhome.log({read})\n"
        };
    }

    public static string Rule(string language, string alias, string sensor, string kind)
    {
        var read = ReadSyntax(language, sensor, kind == "sign");
        string condition = kind switch
        {
            "blind" => read + " > 20",
            "sign" => read,
            "pump" => read + " < 35",
            _ => read + " < 300"
        };
        return language switch
        {
            "csharp" => $"using HomeSdk;\n\nwhile (true)\n{{\n    Home.Set(\"{alias}\", {condition});\n    Home.Wait(0.2);\n}}\n",
            "arduino" => $"#include <VirtualHome.h>\n\nvoid setup() {{ home.begin(); }}\n\nvoid loop() {{\n    home.set(\"{alias}\", {condition});\n    delay(200);\n}}\n",
            _ => $"import home\n\nwhile True:\n    home.set(\"{alias}\", {condition})\n    home.wait(0.2)\n"
        };
    }

    public static string SetSyntax(string language, string alias, bool enabled = true) => language switch
    {
        "csharp" => $"Home.Set(\"{alias}\", {(enabled ? "true" : "false")});",
        "arduino" => $"home.set(\"{alias}\", {(enabled ? "true" : "false")});",
        _ => $"home.set(\"{alias}\", {(enabled ? "True" : "False")})"
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
