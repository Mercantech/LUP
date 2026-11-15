using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Wordle;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.DailyGames;

/// <summary>Reads official results from each game's existing storage using the common history and ranking rules.</summary>
internal sealed class DailyGameProgressService(AppDbContext db)
{
    internal const string Wordle = DailyGameProgressCatalog.Wordle;
    private const int HistoryPageSize = 30;
    private const int RankLimit = 50;

    private static readonly IReadOnlyDictionary<string, Func<StoredPlay, DailyHistoryEntry>> Games =
        new Dictionary<string, Func<StoredPlay, DailyHistoryEntry>>(StringComparer.Ordinal)
        {
            [Wordle] = ScoreWordle,
            [DailyGameIds.Word] = ScoreWord,
            [DailyGameIds.Numbers] = ScoreNumbers
        };

    internal static bool Supports(string game) => Games.ContainsKey(game);

    internal async Task<DailyHistory> History(string game, Guid subject, DateOnly today, int page, CancellationToken ct)
    {
        var completed = Completed(game).Where(play => play.Subject == subject && play.Date <= today);
        var summary = await completed.Select(play => new { play.Date, play.Won })
            .OrderByDescending(play => play.Date).ToArrayAsync(ct);
        var streaks = WordleRules.Streaks(summary.Reverse().Select(play => play.Date), today);
        var offset = (page - 1) * HistoryPageSize;
        var stored = offset >= summary.Length ? [] : await completed.OrderByDescending(play => play.Date)
            .Skip(offset).Take(HistoryPageSize).ToArrayAsync(ct);
        return new(stored.Select(Games[game]).ToArray(), summary.Length, summary.Count(play => play.Won),
            streaks.Current, streaks.Best, page, summary.Length > offset + stored.Length);
    }

    internal async Task<DailyLeaderboard?> Leaderboard(string game, string period, Guid? holdId, ClaimsPrincipal user,
        Guid subject, DateOnly today, CancellationToken ct)
    {
        var (startDate, endDate) = Period(period, today);
        var preferences = db.DailyGameRankingPreferences.AsNoTracking();
        if (holdId is Guid hold)
        {
            if (!await new CourseAccess(db).CanAccessAsync(hold, user, ct))
                return null;

            preferences = preferences.Where(preference =>
                db.Memberships.Any(member => member.HoldId == hold && member.UserSub == preference.UserSub));
        }

        var rows = await (
            from play in Completed(game)
            join preference in preferences on play.Subject equals preference.UserSub
            join profile in db.ProfileNames.AsNoTracking() on preference.UserSub equals profile.UserSub
            where play.Date >= startDate && play.Date <= today && !profile.DisplayName.Contains("@")
            select new { Play = play, Name = profile.DisplayName })
            .ToArrayAsync(ct);
        var totals = rows.GroupBy(row => (row.Play.Subject, row.Name))
            .Select(group => new
            {
                group.Key.Subject,
                group.Key.Name,
                Score = group.Sum(row => Games[game](row.Play).Score),
                Played = group.Count(),
                Wins = group.Count(row => row.Play.Won)
            })
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.Name, StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), ignoreCase: true))
            .ThenBy(row => row.Subject);

        var entries = new List<DailyLeaderboardEntry>(RankLimit);
        DailyLeaderboardEntry? own = null;
        var previousScore = -1;
        var rank = 0;
        var position = 0;
        foreach (var row in totals)
        {
            position++;
            if (row.Score != previousScore) rank = position;
            previousScore = row.Score;
            var entry = new DailyLeaderboardEntry(rank, row.Name, row.Score, row.Played, row.Wins, row.Subject == subject);
            if (entries.Count < RankLimit) entries.Add(entry);
            if (entry.IsMe) own = entry;
            if (entries.Count == RankLimit && own is not null) break;
        }
        return new(startDate, endDate, entries, own);
    }

    internal async Task<DailyRankingSettings> Settings(ClaimsPrincipal user, Guid subject, CancellationToken ct)
    {
        var name = await db.ProfileNames.AsNoTracking().Where(profile => profile.UserSub == subject)
            .Select(profile => profile.DisplayName).SingleOrDefaultAsync(ct);
        var visible = IsRankingName(name)
            && await db.DailyGameRankingPreferences.AsNoTracking().AnyAsync(row => row.UserSub == subject, ct);
        var holds = await new CourseAccess(db).AccessibleHolds(user).AsNoTracking()
            .OrderBy(hold => hold.Name).ThenBy(hold => hold.Id)
            .Select(hold => new DailyRankingHold(hold.Id, hold.Name)).ToArrayAsync(ct);
        return new(visible, IsRankingName(name) ? name : null, holds);
    }

    internal async Task<bool> SetVisibility(Guid subject, bool visible, CancellationToken ct)
    {
        if (!visible)
        {
            await db.DailyGameRankingPreferences.Where(row => row.UserSub == subject).ExecuteDeleteAsync(ct);
            return true;
        }

        // Read and insert together, so removing the profile cannot leave an orphaned preference.
        var written = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO daily_game_ranking_preferences ("UserSub")
            SELECT "UserSub" FROM profile_names WHERE "UserSub" = {subject} AND "DisplayName" NOT LIKE '%@%'
            ON CONFLICT ("UserSub") DO NOTHING
            """, ct);
        return written > 0 || await db.DailyGameRankingPreferences.AnyAsync(row => row.UserSub == subject
            && db.ProfileNames.Any(profile => profile.UserSub == subject && !profile.DisplayName.Contains("@")), ct);
    }

    internal static (DateOnly From, DateOnly To) Period(string period, DateOnly today)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return period switch
        {
            "week" => (monday, monday.AddDays(6)),
            "month" => (new DateOnly(today.Year, today.Month, 1),
                new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),
            _ => (today, today)
        };
    }

    private static bool IsRankingName(string? name) => !string.IsNullOrWhiteSpace(name) && !name.Contains('@');

    private IQueryable<StoredPlay> Completed(string game)
    {
        if (game == Wordle)
            return db.WordlePlays.AsNoTracking().Where(play => play.Status != WordleStatus.Playing)
                .Select(play => new StoredPlay
                {
                    Subject = play.UserSub, Date = play.Date, Won = play.Status == WordleStatus.Won, Attempts = play.Attempts
                });

        var plays = db.DailyGamePlays.AsNoTracking()
            .Where(play => play.Game == game && play.Status != DailyGameStatus.Playing);
        if (game != DailyGameIds.Numbers)
            return plays.Select(play => new StoredPlay
            {
                Subject = play.UserSub, Date = play.Date, Won = play.Status == DailyGameStatus.Won, State = play.StateJson
            });

        return from play in plays
            join puzzle in db.DailyGamePuzzles.AsNoTracking()
                on new { play.Game, play.Date } equals new { puzzle.Game, puzzle.Date }
            select new StoredPlay
            {
                Subject = play.UserSub, Date = play.Date, Won = play.Status == DailyGameStatus.Won,
                State = play.StateJson, Puzzle = puzzle.DataJson
            };
    }

    private static DailyHistoryEntry ScoreWordle(StoredPlay play)
    {
        var attempts = play.Attempts;
        return new(play.Date, play.Won, play.Won && attempts is >= 1 and <= 6 ? (7 - attempts.Value) * 10 : 0,
            attempts, null, null);
    }

    private static DailyHistoryEntry ScoreWord(StoredPlay play)
    {
        var state = ReadState(play.State);
        return new(play.Date, play.Won, play.Won ? Math.Max(0, (4 - state.Guesses.Count) * 20 - state.HintCount * 5) : 0,
            state.Guesses.Count, state.HintCount, null);
    }

    private static DailyHistoryEntry ScoreNumbers(StoredPlay play)
    {
        var state = ReadState(play.State);
        var target = JsonSerializer.Deserialize<DailyPuzzleData>(play.Puzzle!)?.Target;
        var distance = target is int goal && state.Value is int value ? Math.Abs(goal - value) : (int?)null;
        return new(play.Date, play.Won, distance is int delta ? DailyGameScoring.Numbers(delta) : 0, null, null, distance);
    }

    private static DailyPlayState ReadState(string? value) =>
        JsonSerializer.Deserialize<DailyPlayState>(value!) ?? throw new InvalidDataException("Invalid daily game result.");

    private sealed class StoredPlay
    {
        public Guid Subject { get; init; }
        public DateOnly Date { get; init; }
        public bool Won { get; init; }
        public int? Attempts { get; init; }
        public string? State { get; init; }
        public string? Puzzle { get; init; }
    }
}
