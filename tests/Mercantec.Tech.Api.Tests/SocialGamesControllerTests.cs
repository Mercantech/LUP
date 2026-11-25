using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.SocialGames;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class SocialGamesControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Guid hostSub = Guid.NewGuid();
    private AppDbContext db = null!;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static SocialGameSettings Settings(bool timed = false) => new([], null, timed);

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    private SocialGamesController Player(Guid? sub = null, string? key = null)
    {
        var context = new DefaultHttpContext();
        if (sub.HasValue) context.User = new(new ClaimsIdentity([new Claim("sub", sub.ToString()!),
            new Claim("name", "Spiller")], "test"));
        if (key is not null) context.Request.Headers["X-Guest-Key"] = key;
        return new SocialGamesController(new SocialRoomStore(db), db)
            { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private async Task<(string Code, List<SocialGamesController> Players)> FullLobby(bool timed = false)
    {
        var host = Player(hostSub);
        var access = (await host.Create(Settings(timed), default)).Value!;
        var players = new List<SocialGamesController> { host };
        for (var i = 1; i < 5; i++)
        {
            var guest = await Player().Join(access.Code, new($"Gæst {i}"), default);
            players.Add(Player(key: guest.GuestKey!));
        }
        return (access.Code, players);
    }

    [Fact]
    public async Task GuestResumesOwnSeatAndLoginRetainsHostPrivilegesWithoutExposingCredentials()
    {
        var host = Player(hostSub);
        var access = (await host.Create(Settings(), default)).Value!;
        var code = access.Code;
        Assert.True(access.Room.IsHost);
        Assert.Null(access.GuestKey);
        var guestAccess = await Player().Join(code, new("Marie"), default);
        var guest = Player(key: guestAccess.GuestKey!);
        var resumed = await guest.Join(code, new("Andet navn"), default);
        Assert.Equal(guestAccess.Room.Me, resumed.Room.Me);
        Assert.Equal(2, resumed.Room.Players.Length);
        Assert.False(resumed.Room.IsHost);
        Assert.DoesNotContain(guestAccess.GuestKey!, JsonSerializer.Serialize(resumed.Room, Json));
        Assert.DoesNotContain("guestHash", JsonSerializer.Serialize(resumed.Room, Json));
        Assert.DoesNotContain(hostSub.ToString(), JsonSerializer.Serialize(resumed.Room, Json));
        var row = await db.SocialGameRooms.AsNoTracking().SingleAsync();
        Assert.DoesNotContain(guestAccess.GuestKey!, row.StateJson);
        var rev = row.Revision;
        _ = await guest.State(code, default);
        Assert.Equal(rev, (await db.SocialGameRooms.AsNoTracking().SingleAsync()).Revision);
        var outsider = Player(Guid.NewGuid());
        Assert.Equal(403, (await Assert.ThrowsAsync<SocialRoomException>(() => outsider.State(code, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<SocialRoomException>(() => guest.Settings(code, Settings(true), default))).Status);
        Assert.True((await Player(hostSub, guestAccess.GuestKey).State(code, default)).IsHost);
        Assert.Equal(403, (await Assert.ThrowsAsync<SocialRoomException>(() =>
            Player(key: SocialLobby.HashKey("unrelated")).State(code, default))).Status);
        var member = await outsider.Join(code, new(null), default);
        Assert.Equal(member.Room.Me, (await outsider.Join(code, new(null), default)).Room.Me);
        Assert.True((await host.State(code, default)).IsHost);
    }

    [Fact]
    public async Task LostAdmissionResponseCanBeReplayedWithTheSameSecret()
    {
        var access = (await Player(hostSub).Create(Settings(), default)).Value!;
        var key = System.Security.Cryptography.RandomNumberGenerator.GetHexString(64);
        var first = Player();
        first.Request.Headers["X-Guest-Join-Key"] = key;
        var joined = await first.Join(access.Code, new("Louise"), default);
        var retry = Player();
        retry.Request.Headers["X-Guest-Join-Key"] = key;
        var again = await retry.Join(access.Code, new("Louise"), default);
        Assert.Equal(key, again.GuestKey);
        Assert.Equal(joined.Room.Me, again.Room.Me);
        Assert.Equal(joined.Room.Me, (await Player(key: key).State(access.Code, default)).Me);
        Assert.Equal(2, again.Room.Players.Length);
    }

    [Fact]
    public async Task DeadlineCommitsBeforeStaleCommandAndOldChoicesCannotBeReplayed()
    {
        var (code, players) = await FullLobby(timed: true);
        await players[0].Start(code, default);
        var views = new List<SocialRoomView>();
        foreach (var player in players) views.Add(await player.State(code, default));
        var index = views.FindIndex(v => v.Game!.Role == "wolf");
        var wolf = views[index].Game!;
        var target = Assert.Single(wolf.Actions).Options[0].Value;
        Assert.All(views.Where(v => v.Game!.Role == "villager"), v => Assert.Empty(v.Game!.Actions));
        Assert.All(views, v => Assert.False(v.CanChat));
        Assert.Equal(403, (await Assert.ThrowsAsync<SocialRoomException>(() =>
            players[index].Chat(code, new("hemmelig"), default))).Status);
        var row = await db.SocialGameRooms.SingleAsync();
        var state = JsonSerializer.Deserialize<SocialLobby>(row.StateJson, Json)!;
        state.Game!.DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        row.NextDeadlineUtc = state.Game.DeadlineUtc.Value.UtcDateTime;
        row.StateJson = JsonSerializer.Serialize(state, Json);
        await db.SaveChangesAsync();
        var revision = row.Revision;
        var expiredAction = new SocialGameActionRequest(wolf.PhaseId, "hunt", [target]);
        Assert.Equal(409, (await Assert.ThrowsAsync<WerewolfException>(() => players[index].Act(code, expiredAction, default))).Status);
        var publicDay = await players[index].State(code, default);
        Assert.Equal("day", publicDay.Game!.Phase);
        Assert.Equal(4, publicDay.Game.Alive.Length);
        Assert.Equal(revision + 1, (await db.SocialGameRooms.AsNoTracking().SingleAsync()).Revision);
        Assert.Equal(409, (await Assert.ThrowsAsync<WerewolfException>(() => players[index].Act(code, expiredAction, default))).Status);
        var hostView = await players[0].State(code, default);
        var voting = await players[0].Act(code, new(hostView.Game!.PhaseId, "open-vote", []), default);
        Assert.Equal("vote", voting.Game!.Phase);
    }

    [Fact]
    public async Task DuplicateChatRetransmissionProducesOnlyOneLine()
    {
        var host = Player(hostSub);
        var access = (await host.Create(Settings(), default)).Value!;
        host.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();
        var first = await host.Chat(access.Code, new("Klar til spil!"), default);
        var second = await host.Chat(access.Code, new("Klar til spil!"), default);
        Assert.Equal(Assert.Single(first.Chat), Assert.Single(second.Chat));
    }
}
