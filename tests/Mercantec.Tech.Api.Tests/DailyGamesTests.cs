using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.DailyGames;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Wordle;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Mercantec.Tech.Api.Tests;

public sealed class DailyGamesTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Guid _user = Guid.NewGuid();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Day = DailyGameRules.Start;
    private const string Word = DailyGameIds.Word;
    private const string Numbers = DailyGameIds.Numbers;

    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        await _connection.OpenAsync();
        await using var db = Open();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public void WordPuzzlesRotatePublishedHostsAndHaveUniqueTermsAndExamples()
    {
        var answers = Enumerable.Range(0, 55)
            .Select(offset => DailyGameRules.Create(Word, Day.AddDays(offset), new byte[32])).ToArray();
        var wordle = WordleLexicon.Load();
        Assert.Equal(55, answers.Select(data => data.Answer).Distinct().Count());
        for (var index = 0; index < answers.Length; index++)
        {
            var puzzle = answers[index];
            Assert.Equal(DailyGameHosts.Word[index % DailyGameHosts.Word.Count], puzzle.Host);
            Assert.Equal(2, puzzle.ExtraHints!.Length);
            Assert.DoesNotContain(puzzle.Answer!, puzzle.Clue!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(puzzle.Answer, DailyGameRules.NormalizeWord(puzzle.Answer));
            Assert.False(wordle.Allows(puzzle.Answer!));
            Assert.False(string.IsNullOrWhiteSpace(puzzle.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(puzzle.Example));
        }
    }

    [Fact]
    public void NumberGeneratorAlwaysGivesAValidSolutionUsingAvailableTiles()
    {
        for (var offset = 0; offset < 1000; offset++)
        {
            var puzzle = DailyGameRules.Create(Numbers, Day.AddDays(offset), new byte[32]);
            Assert.Equal(DailyGameHosts.Numbers[offset % DailyGameHosts.Numbers.Count], puzzle.Host);
            Assert.Equal(6, puzzle.Tiles!.Length);
            Assert.All(puzzle.Tiles!, tile => Assert.InRange(tile, 1, 100));
            Assert.InRange(puzzle.Target!.Value, 100, 999);
            Assert.True(DailyNumberExpression.TryEvaluate(puzzle.Tiles!, ExampleTokens(puzzle), out var actual, out _));
            Assert.Equal(puzzle.Target, actual);
        }
    }

    [Fact]
    public async Task WordHintsAndGuessesArePrivateAndRetrySafe()
    {
        await using var db = Open();
        var player = Controller(db, _user);
        var metadata = Assert.IsType<DailyGameDay>(Assert.IsType<OkObjectResult>(await player.Daily(Word, default)).Value);
        var original = await Puzzle(db, Word);
        Assert.Equal("lupp", metadata.HostSlug);
        Assert.NotNull(metadata.Clue);
        Assert.DoesNotContain("Answer", JsonSerializer.Serialize(metadata));
        Assert.Equal(new string('_', original.Answer!.Length), metadata.WordPattern);
        var started = Ok(await player.Round(Word, default));
        Assert.Null(started.Answer);
        Assert.Equal(metadata.WordPattern, started.Day.WordPattern);
        var invalid = Assert.IsType<UnprocessableEntityObjectResult>(await player.Guess(Word, Move(0, "😀"), default));
        Assert.Equal("invalid_word", Assert.IsType<DailyGameRound>(invalid.Value).Error);

        var hint = Move(0);
        var hinted = Ok(await player.Hint(Word, hint, default));
        Assert.Equal(1, hinted.Version);
        Assert.Single(hinted.Hints);
        Assert.Empty(hinted.Guesses);
        Assert.Null(hinted.Answer);
        Assert.Equal(original.Answer[0], hinted.Day.WordPattern![0]);
        var replay = Ok(await player.Hint(Word, hint, default));
        Assert.Equal(1, replay.Version);
        Assert.Equal(hinted.Day.WordPattern, replay.Day.WordPattern);
        var stale = Assert.IsType<ConflictObjectResult>(await player.Guess(Word, Move(0, "banan"), default));
        Assert.Equal("round_changed", Assert.IsType<DailyGameRound>(stale.Value).Error);
        var twiceHinted = Ok(await player.Hint(Word, Move(1), default));
        Assert.Equal(2, twiceHinted.Hints.Count);
        Assert.Contains('_', twiceHinted.Day.WordPattern!);
        Assert.NotEqual(hinted.Day.WordPattern, twiceHinted.Day.WordPattern);
        Assert.Equal(twiceHinted.Day.WordPattern, Ok(await player.Round(Word, default)).Day.WordPattern);
        var noMore = Assert.IsType<UnprocessableEntityObjectResult>(await player.Hint(Word, Move(2), default));
        Assert.Equal("hints_exhausted", Assert.IsType<DailyGameRound>(noMore.Value).Error);
        var other = Controller(db, Guid.NewGuid());
        var otherRound = Ok(await other.Round(Word, default));
        Assert.Empty(otherRound.Hints);
        Assert.Equal(metadata.WordPattern, otherRound.Day.WordPattern);
        other.Request.Headers["X-LUP-Daily-Subject"] = _user.ToString();
        var account = Assert.IsType<ConflictObjectResult>(await other.Guess(Word, Move(0, "banan"), default));
        Assert.Equal("account_changed", JsonSerializer.SerializeToElement(account.Value).GetProperty("error").GetString());

        var correct = original.AcceptedAnswers!.FirstOrDefault() ?? original.Answer!;
        var finished = Ok(await player.Guess(Word, Move(2, " " + correct.ToUpperInvariant() + " "), default));
        Assert.Equal(DailyGameStatus.Won, finished.Status);
        Assert.Equal(original.Answer, finished.Answer);
        Assert.Equal(original.Example, finished.Example);
        Assert.Equal(1, finished.Streak);
        Assert.Equal(0, Ok(await player.Round(Numbers, default)).Streak);
    }

    [Theory]
    [InlineData("bøs-bås", 0, "___-___")]
    [InlineData("rød tråd", 1, "r__ _r__")]
    [InlineData("bøs-bås", 2, "bø_-b__")]
    [InlineData("sø", 2, "s_")]
    [InlineData("aa", 2, "__")]
    public void WordPatternKeepsSeparatorsAndNeverRevealsEntireAnswer(string answer, int hints, string expected)
    {
        Assert.Equal(expected, DailyGameRules.WordPattern(answer, hints));
    }

    [Fact]
    public async Task NumberSubmissionScoresOnServerAndMidnightUsesCopenhagenDate()
    {
        await using var db = Open();
        var player = Controller(db, _user);
        var starting = Ok(await player.Round(Numbers, default));
        Assert.Null(starting.Example);
        Assert.Null(starting.Day.WordPattern);
        var duplicate = Assert.IsType<UnprocessableEntityObjectResult>(
            await player.Submit(Numbers, Move(0, tokens: ["0", "+", "0"]), default));
        Assert.Equal("invalid_expression", Assert.IsType<DailyGameRound>(duplicate.Value).Error);
        var expression = Move(0, tokens: ["0", "+", "1"]);
        var result = Ok(await player.Submit(Numbers, expression, default));
        Assert.Equal(starting.Day.Tiles![0] + starting.Day.Tiles![1], result.Value);
        Assert.Equal(Math.Abs(result.Value!.Value - starting.Day.Target!.Value), result.Distance);
        Assert.Equal(Math.Max(0, 100 - result.Distance!.Value), result.Score);
        Assert.NotNull(result.Example);
        Assert.Equal(1, result.Streak);
        Assert.Equal(result.Expression, Ok(await player.Submit(Numbers, expression, default)).Expression);

        _clock.Now = new DateTimeOffset(2026, 11, 14, 23, 0, 0, TimeSpan.Zero);
        var stale = Assert.IsType<DailyGameRound>(Assert.IsType<ConflictObjectResult>(
            await player.Submit(Numbers, Move(1, tokens: ["0", "+", "1"]), default)).Value);
        Assert.Equal("day_changed", stale.Error);
        Assert.Equal(Day.AddDays(1), stale.Day.Date);
        Assert.Equal("chameleon", stale.Day.HostSlug);
        Assert.Equal(1, stale.Streak);
        var solution = ExampleTokens(await Puzzle(db, Numbers, stale.Day.Date));
        var second = Ok(await player.Submit(Numbers, new(stale.Day.Date, 0, Guid.NewGuid(), Tokens: solution), default));
        Assert.Equal(DailyGameStatus.Won, second.Status);
        Assert.Equal(100, second.Score);
        Assert.Equal(2, second.Streak);
        _clock.Now = _clock.Now.AddDays(2);
        var missed = Ok(await player.Round(Numbers, default));
        Assert.Equal(0, missed.Streak);
        Assert.Equal(2, missed.BestStreak);
    }

    [Fact]
    public async Task PrivacyExportIncludesOnlyOwnDailyRounds()
    {
        await using var db = Open();
        var player = Controller(db, _user);
        await player.Guess(Word, Move(0, "banan"), default);
        await Controller(db, Guid.NewGuid()).Guess(Word, Move(0, "kanin"), default);
        using var package = JsonDocument.Parse(Assert.IsType<FileContentResult>(
            await new PrivacyController(db) { ControllerContext = player.ControllerContext }.Export(default)).FileContents);
        var record = Assert.Single(package.RootElement.GetProperty("PlatformRecords")
            .GetProperty("dailyGamePlays").EnumerateArray());
        Assert.Equal("banan", Assert.Single(record.GetProperty("guesses").EnumerateArray()).GetString());
        Assert.DoesNotContain(db.DailyGameSchedules.Single().Key, package.RootElement.GetRawText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoTabsCannotSpendTwoMovesAtTheSameVersion(bool prefill)
    {
        var postgresConnection = Environment.GetEnvironmentVariable("LUP_DAILY_GAMES_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(postgresConnection))
        {
            await using var sqlite = new SqliteConnection($"Data Source=daily-game-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await sqlite.OpenAsync();
            await CheckRace(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(sqlite.ConnectionString), prefill);
            return;
        }

        var postgres = new NpgsqlConnectionStringBuilder(postgresConnection);
        if (postgres.Host is not ("127.0.0.1" or "localhost") || postgres.Database?.EndsWith("_qa", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Daily games PostgreSQL tests require a local _qa database.");
        await CheckRace(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgresConnection), prefill);
    }

    private async Task CheckRace(DbContextOptionsBuilder<AppDbContext> options, bool prefill)
    {
        await using var setup = new AppDbContext(options.Options);
        if (setup.Database.IsSqlite()) await setup.Database.EnsureCreatedAsync();
        var player = Controller(setup, _user);
        await player.Daily(Word, default);
        if (prefill) await player.Guess(Word, Move(0, "kanin"), default);

        var version = prefill ? 1 : 0;
        var race = new SaveRace();
        var racingOptions = options.AddInterceptors(race).Options;
        await using var first = new AppDbContext(racingOptions);
        await using var second = new AppDbContext(racingOptions);
        try
        {
            var results = await Task.WhenAll(
                Controller(first, _user).Guess(Word, Move(version, "banan"), default),
                Controller(second, _user).Hint(Word, Move(version), default));
            var winner = Ok(Assert.Single(results.OfType<OkObjectResult>()));
            var loser = Assert.IsType<DailyGameRound>(Assert.Single(results.OfType<ConflictObjectResult>()).Value);
            Assert.Equal("round_changed", loser.Error);
            Assert.Equal(version + 1, winner.Version);
            Assert.Equal(winner.Version, loser.Version);
            Assert.Equal(winner.Guesses, loser.Guesses);
            Assert.Equal(winner.Hints, loser.Hints);
        }
        finally
        {
            await setup.DailyGamePlays.Where(play => play.UserSub == _user).ExecuteDeleteAsync();
        }
    }

    private static string[] ExampleTokens(DailyPuzzleData puzzle)
    {
        var remaining = Enumerable.Range(0, puzzle.Tiles!.Length).ToList();
        return puzzle.Example!.Split(' ').Select(token =>
        {
            if (!int.TryParse(token, out var tile)) return token;
            var index = remaining.First(candidate => puzzle.Tiles[candidate] == tile);
            remaining.Remove(index);
            return index.ToString(CultureInfo.InvariantCulture);
        }).ToArray();
    }

    private static async Task<DailyPuzzleData> Puzzle(AppDbContext db, string game, DateOnly? date = null) =>
        JsonSerializer.Deserialize<DailyPuzzleData>((await db.DailyGamePuzzles
            .SingleAsync(row => row.Game == game && row.Date == (date ?? Day))).DataJson)!;

    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    private static DailyGameMove Move(int version, string? word = null, string[]? tokens = null) =>
        new(Day, version, Guid.NewGuid(), word, tokens);
    private static DailyGameRound Ok(IActionResult result) => Assert.IsType<DailyGameRound>(Assert.IsType<OkObjectResult>(result).Value);

    private DailyGamesController Controller(AppDbContext db, Guid subject)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject.ToString())], "test")) };
        context.Request.Headers["X-LUP-Daily-Subject"] = subject.ToString();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DailyGames:ScheduleKey"] = "daily-game-test" }).Build();
        return new DailyGamesController(db, _clock, configuration) { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SaveRace : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _ready;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _ready) == 2) _gate.TrySetResult();
            await _gate.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}
