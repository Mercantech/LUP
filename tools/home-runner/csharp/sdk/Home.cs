using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HomeSdk;

public static class Home
{
    private const double MaximumSeconds = 9_007_199_254_740;
    private static readonly TextReader Input = Console.In;
    private static readonly TextWriter Output = Console.Out;
    private static readonly List<object> Commands = [];
    private static readonly List<string> Logs = [];
    private static JsonElement snapshot;
    private static int logBytes;
    private static HomeError? fault;

    public static double Read(string alias) => Sensor(alias, JsonValueKind.Number).GetDouble();

    public static bool ReadBool(string alias)
    {
        var value = Sensor(alias, JsonValueKind.True, JsonValueKind.False);
        return value.GetBoolean();
    }

    public static void Set(string alias, bool value)
    {
        if (Kind(alias) != "lamp")
            throw Fail("INVALID_TYPE", "Enheden kan ikke tændes eller slukkes som lampe.");
        if (Commands.Count >= 256)
            throw Fail("INVALID_VALUE", "En aktivering kan højst indeholde 256 kommandoer.");
        Commands.Add(new { alias, action = "setPower", value });
    }

    public static void Wait(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || seconds > MaximumSeconds)
            throw Fail("INVALID_VALUE", "Ventetiden skal være et endeligt tal på mindst nul.");
        Yield((long)Math.Ceiling(seconds * 1000), false);
        Receive();
    }

    public static long NowMs() => snapshot.GetProperty("timeMs").GetInt64();

    public static void Log(object? value)
    {
        var remaining = Math.Min(16384 - logBytes, 2048);
        if (remaining <= 0 || Logs.Count >= 256)
            return;
        var line = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        var output = new StringBuilder();
        foreach (var rune in line.EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > remaining)
                break;
            output.Append(rune.ToString());
            remaining -= rune.Utf8SequenceLength;
            logBytes += rune.Utf8SequenceLength;
        }
        Logs.Add(output.ToString());
        logBytes++;
    }

    internal static void Receive()
    {
        var line = Input.ReadLine() ?? throw new EndOfStreamException("Simulatorforbindelsen er lukket.");
        using var document = JsonDocument.Parse(line);
        if (document.RootElement.GetProperty("type").GetString() != "snapshot")
            throw new InvalidDataException("Ugyldigt sensorøjebliksbillede.");
        snapshot = document.RootElement.Clone();
    }

    internal static void RedirectConsole()
    {
        var logger = new LogWriter();
        Console.SetOut(logger);
        Console.SetError(logger);
        Console.SetIn(TextReader.Null);
    }

    internal static void Yield(long delay, bool done)
    {
        if (fault is not null)
            throw fault;
        Emit(new
        {
            type = "batch", activation = Activation(), commands = Commands,
            nextDelayMs = delay, logs = Logs, done
        });
        Commands.Clear();
        Logs.Clear();
        logBytes = 0;
    }

    internal static void Report(Exception error)
    {
        error = fault ?? error;
        var diagnostic = error.ToString();
        Emit(new
        {
            type = "error",
            activation = Activation(),
            code = (error as HomeError)?.Code ?? "RUNTIME_ERROR",
            message = diagnostic[..Math.Min(diagnostic.Length, 12000)]
        });
    }

    private static string Kind(string alias)
    {
        if (string.IsNullOrEmpty(alias) || !snapshot.GetProperty("aliases").TryGetProperty(alias, out var value))
            throw Fail("UNKNOWN_ALIAS", $"Aliaset {alias} findes ikke i boligen.");
        return value.GetString() switch
        {
            "uninstalled" => throw Fail("DEVICE_NOT_INSTALLED", $"Enheden {alias} ligger på lager."),
            "locked" => throw Fail("CAPABILITY_LOCKED", $"Enheden {alias} er endnu låst."),
            { } kind => kind,
            _ => throw Fail("UNKNOWN_ALIAS", $"Aliaset {alias} findes ikke i boligen.")
        };
    }

    private static JsonElement Sensor(string alias, JsonValueKind type, JsonValueKind? alternative = null)
    {
        var kind = Kind(alias);
        if (kind is not ("daylight" or "presence" or "switch" or "brightness" or "light"))
            throw Fail("INVALID_TYPE", $"Enheden {alias} kan ikke aflæses som sensor.");
        if (!snapshot.GetProperty("values").TryGetProperty(alias, out var value))
            throw Fail("CAPABILITY_LOCKED", $"Sensoren {alias} er ikke tilgængelig i dette forsøg.");
        if (value.ValueKind != type && value.ValueKind != alternative)
            throw Fail("INVALID_TYPE", $"Sensoren {alias} returnerer en anden datatype.");
        return value;
    }

    private static HomeError Fail(string code, string message) => fault ??= new HomeError(code, message);

    private static string? Activation() =>
        snapshot.ValueKind == JsonValueKind.Object && snapshot.TryGetProperty("activation", out var value)
            ? value.GetString() : null;

    private static void Emit<T>(T value)
    {
        Output.WriteLine(JsonSerializer.Serialize(value));
        Output.Flush();
    }

    private sealed class HomeError(string code, string message) : Exception(message)
    {
        internal string Code { get; } = code;
    }

    private sealed class LogWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value is not ('\r' or '\n'))
                Log(value);
        }

        public override void Write(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                Log(value);
        }

        public override void WriteLine(string? value) => Log(value);
    }
}
