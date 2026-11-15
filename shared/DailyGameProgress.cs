namespace Mercantec.Tech.Contracts;

public static class DailyGameProgressCatalog
{
    public const string Wordle = "wordle";

    public static IReadOnlyList<DailyGameProgressDefinition> Games { get; } = Array.AsReadOnly<DailyGameProgressDefinition>(
    [
        new(Wordle, "Dagens Wordle", "Du får 60 point ved første rigtige gæt og 10 færre for hvert ekstra forsøg. En runde uden løsning giver 0 point."),
        new(DailyGameIds.Word, "Dagens ord", "Du får 60 point ved første rigtige gæt. Hvert ekstra gæt koster 20 point, og hver åbnet ledetråd koster 5. En runde uden løsning giver 0 point."),
        new(DailyGameIds.Numbers, "Taljagt", "Du får 100 point for at ramme måltallet. For hvert tal, du er fra målet, trækkes ét point. Du får mindst 0 point.")
    ]);

    public static DailyGameProgressDefinition? Find(string? id) => Games.FirstOrDefault(game => game.Id == id);
}

public sealed record DailyGameProgressDefinition(string Id, string Title, string ScoreDescription);

public static class DailyGameScoring
{
    public static int Numbers(int distance) => Math.Max(0, 100 - distance);
}

public sealed record DailyHistory(IReadOnlyList<DailyHistoryEntry> Entries, int Completed, int Wins,
    int Streak, int BestStreak, int Page, bool HasMore);

public sealed record DailyHistoryEntry(DateOnly Date, bool Won, int Score, int? Attempts, int? Hints, int? Distance);

public sealed record DailyLeaderboard(DateOnly From, DateOnly To, IReadOnlyList<DailyLeaderboardEntry> Entries,
    DailyLeaderboardEntry? Own);

public sealed record DailyLeaderboardEntry(int Rank, string Name, int Score, int Played, int Wins, bool IsMe);

public sealed record DailyRankingSettings(bool Visible, string? DisplayName, IReadOnlyList<DailyRankingHold> Holds);

public sealed record DailyRankingHold(Guid Id, string Name);

public sealed record ChangeDailyRankingVisibility([property: System.Text.Json.Serialization.JsonRequired] bool Visible);
