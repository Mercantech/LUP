using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.DailyGames;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class DailyGameProgressTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private static readonly DateOnly Today = new(2026, 11, 23);

    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        await _connection.OpenAsync();
        await using var db = Open();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task HistoryUsesExistingResultsWithoutPublishingAnswersAndPagesNewestFirst()
    {
        await using var db = Open();
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        for (var offset = 0; offset <= 30; offset++)
        {
            var date = Today.AddDays(-offset);
            db.WordlePuzzles.Add(new() { Date = date, Answer = "facit", Explanation = "hemmelighed" });
            db.WordlePlays.Add(new() { UserSub = me, Date = date, Status = WordleStatus.Won, Attempts = 3 });
        }
        db.WordlePlays.Add(new() { UserSub = other, Date = Today, Status = WordleStatus.Won, Attempts = 1 });
        await db.SaveChangesAsync();

        var controller = Controller(db, me);
        var first = Ok<DailyHistory>(await controller.History(DailyGameProgressCatalog.Wordle, default));
        Assert.Equal((31, 31, 31, 31), (first.Completed, first.Wins, first.Streak, first.BestStreak));
        Assert.True(first.HasMore);
        Assert.Equal(Today, first.Entries[0].Date);
        Assert.Equal(40, first.Entries[0].Score);
        Assert.DoesNotContain("facit", JsonSerializer.Serialize(first));
        Assert.DoesNotContain("hemmelighed", JsonSerializer.Serialize(first));
        var last = Ok<DailyHistory>(await controller.History(DailyGameProgressCatalog.Wordle, default, 2));
        Assert.Equal(Today.AddDays(-30), Assert.Single(last.Entries).Date);
        Assert.False(last.HasMore);
        Assert.Empty(Ok<DailyHistory>(await controller.History(DailyGameIds.Word, default)).Entries);
        Assert.IsType<BadRequestObjectResult>(await controller.History(DailyGameIds.Word, default, 0));
    }

    [Fact]
    public async Task RankingsRequireOptInRespectHoldAccessAndGiveEqualScoresEqualRank()
    {
        await using var db = Open();
        var me = Guid.NewGuid();
        var peer = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var hidden = Guid.NewGuid();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H1", Forloeb = new Forloeb { Id = Guid.NewGuid(), Name = "H1" } };
        db.Hold.Add(hold);
        db.Memberships.AddRange(new Membership { Hold = hold, UserSub = me }, new Membership { Hold = hold, UserSub = peer },
            new Membership { Hold = hold, UserSub = hidden });
        db.ProfileNames.AddRange(new ProfileName { UserSub = me, DisplayName = "Vera" },
            new ProfileName { UserSub = peer, DisplayName = "Ægir" },
            new ProfileName { UserSub = outsider, DisplayName = "Ude" },
            new ProfileName { UserSub = hidden, DisplayName = "Skjult" });
        db.WordlePuzzles.Add(new() { Date = Today });
        db.WordlePlays.AddRange(new WordlePlay { Date = Today, UserSub = me, Attempts = 2, Status = WordleStatus.Won },
            new WordlePlay { Date = Today, UserSub = peer, Attempts = 2, Status = WordleStatus.Won },
            new WordlePlay { Date = Today, UserSub = outsider, Attempts = 1, Status = WordleStatus.Won },
            new WordlePlay { Date = Today, UserSub = hidden, Attempts = 1, Status = WordleStatus.Won });
        await db.SaveChangesAsync();

        var controller = Controller(db, me);
        var settings = Ok<DailyRankingSettings>(await controller.Settings(default));
        Assert.False(settings.Visible);
        Assert.Equal(hold.Id, Assert.Single(settings.Holds).Id);
        Assert.True(Ok<DailyRankingSettings>(await controller.SetVisibility(new(true), default)).Visible);
        await Controller(db, peer).SetVisibility(new(true), default);
        await Controller(db, outsider).SetVisibility(new(true), default);
        var group = Ok<DailyLeaderboard>(await controller.Leaderboard(DailyGameProgressCatalog.Wordle, default, "week", hold.Id));
        Assert.Equal(Today, group.From);
        Assert.Equal(Today.AddDays(6), group.To);
        Assert.Equal(["Vera", "Ægir"], group.Entries.Select(entry => entry.Name));
        Assert.All(group.Entries, entry => Assert.Equal(1, entry.Rank));
        Assert.True(group.Own?.IsMe);
        Assert.IsType<NotFoundResult>(await Controller(db, outsider)
            .Leaderboard(DailyGameProgressCatalog.Wordle, default, "day", hold.Id));

        var global = Ok<DailyLeaderboard>(await controller.Leaderboard(DailyGameProgressCatalog.Wordle, default));
        Assert.Equal(["Ude", "Vera", "Ægir"], global.Entries.Select(entry => entry.Name));
        Assert.Equal(2, global.Own?.Rank);
        Assert.False(Ok<DailyRankingSettings>(await controller.SetVisibility(new(false), default)).Visible);
        var hiddenAgain = Ok<DailyLeaderboard>(await controller.Leaderboard(DailyGameProgressCatalog.Wordle, default));
        Assert.Null(hiddenAgain.Own);
        Assert.DoesNotContain(hiddenAgain.Entries, entry => entry.Name is "Vera" or "Skjult");
    }

    [Fact]
    public async Task BothDailyGamesRetainTheirOwnScoresAndOnlyStoredDisplayNamesArePublished()
    {
        await using var db = Open();
        var me = Guid.NewGuid();
        var controller = Controller(db, me);
        Assert.IsType<UnprocessableEntityObjectResult>(await controller.SetVisibility(new(true), default));
        var profile = new ProfileName { UserSub = me, DisplayName = "test@example.invalid" };
        db.ProfileNames.Add(profile);
        await db.SaveChangesAsync();
        Assert.IsType<UnprocessableEntityObjectResult>(await controller.SetVisibility(new(true), default));
        profile.DisplayName = "Eva";
        await db.SaveChangesAsync();
        controller.Request.Headers[ProfileNameRules.SubjectHeader] = Guid.NewGuid().ToString();
        Assert.IsType<ConflictObjectResult>(await controller.SetVisibility(new(true), default));
        controller.Request.Headers[ProfileNameRules.SubjectHeader] = me.ToString();
        await controller.SetVisibility(new(true), default);

        db.DailyGamePuzzles.AddRange(
            new DailyGamePuzzle { Date = Today, Game = DailyGameIds.Word },
            new DailyGamePuzzle { Date = Today, Game = DailyGameIds.Numbers,
                DataJson = JsonSerializer.Serialize(new DailyPuzzleData("bilbo", "Regnestykker", Target: 250)) });
        db.DailyGamePlays.AddRange(
            new DailyGamePlay { Date = Today, Game = DailyGameIds.Word, UserSub = me, Status = DailyGameStatus.Won,
                StateJson = JsonSerializer.Serialize(new DailyPlayState { HintCount = 1, Guesses = ["ord", "facit"] }) },
            new DailyGamePlay { Date = Today, Game = DailyGameIds.Numbers, UserSub = me, Status = DailyGameStatus.Lost,
                StateJson = JsonSerializer.Serialize(new DailyPlayState { Value = 248 }) });
        await db.SaveChangesAsync();
        var history = Ok<DailyHistory>(await controller.History(DailyGameIds.Word, default));
        var word = Assert.Single(history.Entries);
        Assert.Equal((35, 2, 1), (word.Score, word.Attempts, word.Hints));
        var numbers = Assert.Single(Ok<DailyHistory>(await controller.History(DailyGameIds.Numbers, default)).Entries);
        Assert.Equal(98, numbers.Score);
        Assert.Equal(2, numbers.Distance);
        Assert.False(numbers.Won);
        var board = Ok<DailyLeaderboard>(await controller.Leaderboard(DailyGameIds.Numbers, default, "month"));
        Assert.Equal(1, board.From.Day);
        Assert.Equal(30, board.To.Day);
        Assert.Equal("Eva", Assert.Single(board.Entries).Name);
        Assert.Equal(98, board.Own?.Score);
        Assert.DoesNotContain("test@example.invalid", JsonSerializer.Serialize(board));
        Assert.DoesNotContain(me.ToString(), JsonSerializer.Serialize(board));

        await db.ProfileNames.Where(name => name.UserSub == me).ExecuteDeleteAsync();
        Assert.Empty(Ok<DailyLeaderboard>(await controller.Leaderboard(DailyGameIds.Numbers, default)).Entries);
        Assert.Empty(db.DailyGameRankingPreferences);
    }

    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private static DailyGameProgressController Controller(AppDbContext db, Guid subject)
    {
        // 23:30 UTC is already Monday in Copenhagen.
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 22, 23, 30, 0, TimeSpan.Zero));
        var controller = new DailyGameProgressController(db, clock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject.ToString())], "test"))
                }
            }
        };
        controller.Request.Headers[ProfileNameRules.SubjectHeader] = subject.ToString();
        return controller;
    }

    private static T Ok<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
