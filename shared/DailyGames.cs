namespace Mercantec.Tech.Contracts;

public static class DailyGameIds
{
    public const string Word = "dagens-ord";
    public const string Numbers = "taljagt";
}

public static class DailyGameHosts
{
    public static IReadOnlyList<string> Word { get; } =
        Array.AsReadOnly(new[] { "lupp", "vatson", "gummianden", "raymond", "bilbo", "chameleon",
            "whale", "bee", "bat", "beaver", "fox" });

    public static IReadOnlyList<string> Numbers { get; } = Array.AsReadOnly(new[] { "bilbo", "chameleon" });
}

public enum DailyGameStatus { Playing, Won, Lost }

public sealed record DailyGameDay(
    DateOnly Date, DateTimeOffset NextRoundAt, string HostSlug, string Theme,
    string? Clue = null, IReadOnlyList<int>? Tiles = null, int? Target = null,
    string? WordPattern = null);

public sealed record DailyGameRound(
    DailyGameDay Day, int Version, DailyGameStatus Status, int Streak, int BestStreak,
    IReadOnlyList<string> Guesses, IReadOnlyList<string> Hints,
    string? Answer = null, string? Explanation = null, string? Example = null,
    int? Value = null, int? Distance = null, int? Score = null,
    string? Expression = null, string? Error = null);

public sealed record DailyGameMove(DateOnly Date, int Version, Guid RequestId, string? Word = null,
    IReadOnlyList<string>? Tokens = null);
